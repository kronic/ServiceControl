namespace ServiceControlInstaller.Engine.Configuration.Monitoring
{
    public static class SettingsList
    {
        //TODO : FIX UP Names

        public static SettingInfo Port = new SettingInfo { Name =  "Monitoring/Port" };
        public static SettingInfo HostName = new SettingInfo { Name = "Monitoring/HostName" };
        public static SettingInfo LogPath = new SettingInfo { Name = "Monitoring/LogPath" };
        public static SettingInfo TransportType = new SettingInfo { Name = "Monitoring/TransportType" };
        public static SettingInfo ErrorQueue = new SettingInfo { Name = "Monitoring/ErrorQueue" };
    }
}