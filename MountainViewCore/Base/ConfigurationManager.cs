#if !JDESKTOP
using System;
using Microsoft.Extensions.Configuration;
#endif

namespace MountainView.Base
{
    internal class ConfigurationManager
    {
#if !JDESKTOP
        private static Lazy<IConfigurationRoot> configuration = new Lazy<IConfigurationRoot>(() =>
        {
            return new ConfigurationBuilder()
                .AddJsonFile("app.config.json", optional: true)
                .Build();
        });

        public static IConfiguration AppSettings
        {
            get { return configuration.Value; }
        }
#else
        public static System.Collections.Specialized.NameValueCollection AppSettings
        {
            get
            {
                return System.Configuration.ConfigurationManager.AppSettings;
            }
        }
#endif
    }
}