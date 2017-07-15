using Microsoft.Extensions.Configuration;

namespace MountainView
{
    internal class ConfigurationManager
    {
        private static IConfigurationRoot configuration;

        public static IConfigurationRoot AppSettings
        {
            get
            {
                if (configuration == null)
                {
                    configuration = new ConfigurationBuilder()
                        .AddJsonFile("app.config.json", optional: true)
                        .Build();
                }

                return configuration;
            }
        }
    }
}