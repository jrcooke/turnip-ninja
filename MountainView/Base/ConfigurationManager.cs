using System;
using Microsoft.Extensions.Configuration;

namespace MountainView.Base
{
    internal class ConfigurationManager
    {
        private static Lazy<IConfigurationRoot> configuration = new Lazy<IConfigurationRoot>(() =>
        {
            return new ConfigurationBuilder()
                .AddJsonFile("app.config.json", optional: true)
                .Build();
        });

        public static IConfigurationRoot AppSettings
        {
            get { return configuration.Value; }
        }
    }
}