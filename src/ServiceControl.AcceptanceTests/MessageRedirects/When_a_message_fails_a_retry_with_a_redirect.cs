﻿namespace ServiceBus.Management.AcceptanceTests.MessageRedirects
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;

    class When_a_message_fails_a_retry_with_a_redirect : AcceptanceTest
    {
        [Test]
        public void The_original_failed_message_record_is_updated()
        {
            FailedMessage failedMessage;
            List<FailedMessageView> failedMessages = null;

            Define<Context>()
                .WithEndpoint<OriginalEndpoint>(b =>
                        b.Given(bus => bus.SendLocal(new MessageToRetry()))
                            .When( // Failed Message Received
                                ctx => ctx.UniqueMessageId != null && TryGet($"/api/errors/{ctx.UniqueMessageId}", out failedMessage),
                                (bus, ctx) =>
                                {
                                    // Create Redirect
                                    Post("/api/redirects", new RedirectRequest
                                    {
                                        fromphysicaladdress = ctx.FromAddress,
                                        tophysicaladdress = ctx.ToAddress
                                    }, status => status != HttpStatusCode.Created);

                                    // Retry Failed Message
                                    Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                                })
                )
                .WithEndpoint<NewEndpoint>()
                .Done(ctx => ctx.ProcessedAgain
                             && TryGetMany("/api/errors",
                                 out failedMessages,
                                 msg => msg.Exception.Message.Contains("Message Failed In New Endpoint Too")
                             )
                )
                .Run();

            Assert.IsNotNull(failedMessages);
            Assert.IsNotEmpty(failedMessages);
            Assert.AreEqual(1, failedMessages.Count);

            var failedMessageView = failedMessages.Single();
            Assert.AreEqual(2, failedMessageView.NumberOfProcessingAttempts);
            Assert.AreEqual(FailedMessageStatus.Unresolved, failedMessageView.Status);
        }

        class OriginalEndpoint : EndpointConfigurationBuilder
        {
            public OriginalEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => c.DisableFeature<SecondLevelRetries>())
                    .WithConfig<TransportConfig>(c =>
                    {
                        c.MaxRetries = 1;
                    });
            }

            public class MessageToRetryHandler : IHandleMessages<MessageToRetry>
            {
                public Context Context { get; set; }
                public IBus Bus { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public void Handle(MessageToRetry message)
                {
                    Context.FromAddress = Settings.LocalAddress().ToString();
                    Context.UniqueMessageId = DeterministicGuid.MakeId(Bus.CurrentMessageContext.Id.Replace(@"\", "-"), Settings.LocalAddress().Queue).ToString();
                    throw new Exception("Message Failed");
                }
            }
        }

        class NewEndpoint : EndpointConfigurationBuilder
        {
            public NewEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => c.DisableFeature<SecondLevelRetries>())
                    .WithConfig<TransportConfig>(c =>
                    {
                        c.MaxRetries = 1;
                    });
            }

            public class EndpointDiscovery : IWantToRunWhenBusStartsAndStops
            {
                public Context Context { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public void Start()
                {
                    Context.ToAddress = Settings.LocalAddress().ToString();
                }

                public void Stop() { }
            }

            public class MessageToRetryHandler : IHandleMessages<MessageToRetry>
            {
                public Context Context { get; set; }

                public void Handle(MessageToRetry message)
                {
                    Context.ProcessedAgain = true;
                    throw new Exception("Message Failed In New Endpoint Too");
                }
            }
        }

        class Context : ScenarioContext
        {
            public string FromAddress { get; set; }
            public string ToAddress { get; set; }
            public string UniqueMessageId { get; set; }
            public bool ProcessedAgain { get; set; }
        }

        [Serializable]
        class MessageToRetry : ICommand
        {

        }
    }
}