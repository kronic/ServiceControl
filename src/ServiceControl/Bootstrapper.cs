namespace Particular.ServiceControl
{
    using System;
    using System.IO;
    using System.Net;
    using System.ServiceProcess;
    using Autofac;
    using global::ServiceControl.Infrastructure;
    using global::ServiceControl.Infrastructure.DomainEvents;
    using global::ServiceControl.Infrastructure.SignalR;
    using Microsoft.Owin.Hosting;
    using NServiceBus;
    using Raven.Client;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure;
    using ServiceBus.Management.Infrastructure.OWIN;
    using ServiceBus.Management.Infrastructure.Settings;
    using LogManager = NServiceBus.Logging.LogManager;

    public class Bootstrapper
    {
        private BusConfiguration configuration;
        private EmbeddableDocumentStore documentStore = new EmbeddableDocumentStore();
        private ServiceBase host;
        private ShutdownNotifier notifier = new ShutdownNotifier();
        private Settings settings;
        private TimeKeeper timeKeeper;
        private IContainer container;
        private IBus bus;
        public IDisposable WebApp;

        // Windows Service
        public Bootstrapper(ServiceBase host)
        {
            this.host = host;
            settings = new Settings(host.ServiceName);
            Initialize();
        }

        // Acceptance Tests
        public Bootstrapper(Settings settings, BusConfiguration configuration)
        {
            this.configuration = configuration;
            this.settings = settings;
            Initialize();
        }

        public Startup Startup { get; private set; }

        private void Initialize()
        {
            var loggingSettings = new LoggingSettings(settings.ServiceName);
            RecordStartup(loggingSettings);

            // .NET default limit is 10. RavenDB in conjunction with transports that use HTTP exceeds that limit.
            ServicePointManager.DefaultConnectionLimit = settings.HttpDefaultConnectionLimit;

            timeKeeper = new TimeKeeper();

            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterType<MessageStreamerConnection>().SingleInstance();
            containerBuilder.RegisterInstance(loggingSettings);
            containerBuilder.RegisterInstance(settings);
            containerBuilder.RegisterInstance(notifier).ExternallyOwned();
            containerBuilder.RegisterInstance(timeKeeper).ExternallyOwned();
            containerBuilder.RegisterType<SubscribeToOwnEvents>().PropertiesAutowired().SingleInstance();
            containerBuilder.RegisterInstance(documentStore).As<IDocumentStore>().ExternallyOwned();

            if (configuration == null)
            {
                configuration = new BusConfiguration();
                configuration.AssembliesToScan(AllAssemblies.Except("ServiceControl.Plugin"));
            }

            container = containerBuilder.Build();
            Startup = new Startup(container);
            DomainEvents.Container = container;
        }

        public IBus Start(bool isRunningAcceptanceTests = false)
        {
            var logger = LogManager.GetLogger(typeof(Bootstrapper));

            if (!isRunningAcceptanceTests)
            {
                var startOptions = new StartOptions(settings.RootUrl);

                WebApp = Microsoft.Owin.Hosting.WebApp.Start(startOptions, b => Startup.Configuration(b));
            }

            bus = NServiceBusFactory.CreateAndStart(settings, container, host, documentStore, configuration);

            logger.InfoFormat("Api is now accepting requests on {0}", settings.ApiUrl);

            return bus;
        }

        public void Stop()
        {
            notifier.Dispose();
            bus?.Dispose();
            timeKeeper.Dispose();
            documentStore.Dispose();
            WebApp?.Dispose();
            container.Dispose();
        }

        private long DataSize()
        {
            var datafilePath = Path.Combine(settings.DbPath, "data");

            try
            {
                var info = new FileInfo(datafilePath);

                return info.Length;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private void RecordStartup(LoggingSettings loggingSettings)
        {
            var version = typeof(Bootstrapper).Assembly.GetName().Version;
            var startupMessage = $@"
-------------------------------------------------------------
ServiceControl Version:       {version}
Selected Transport:           {settings.TransportType}
Audit Retention Period:       {settings.AuditRetentionPeriod}
Error Retention Period:       {settings.ErrorRetentionPeriod}
Forwarding Error Messages:    {settings.ForwardErrorMessages}
Forwarding Audit Messages:    {settings.ForwardAuditMessages}
Database Size:                {DataSize()}bytes
ServiceControl Logging Level: {loggingSettings.LoggingLevel}
RavenDB Logging Level:        {loggingSettings.RavenDBLogLevel}
-------------------------------------------------------------";

            var logger = LogManager.GetLogger(typeof(Bootstrapper));
            logger.Info(startupMessage);
        }
    }
}