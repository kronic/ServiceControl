﻿// ReSharper disable MemberCanBePrivate.Global
namespace ServiceBus.Management.Infrastructure.Installers.UrlAcl.Api
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal struct HttpServiceConfigSslSet
    {
        public HttpServiceConfigSslKey KeyDesc;

        public HttpServiceConfigSslParam ParamDesc;
    }
}