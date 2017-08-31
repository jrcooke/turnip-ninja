using System;
#if !JDESKTOP
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
        public static IConfiguration AppSettings
        {
            get { return null; }
        }

        public interface IConfiguration
        {
            string this[string key] { get; set; }
        }
#endif
    }
}