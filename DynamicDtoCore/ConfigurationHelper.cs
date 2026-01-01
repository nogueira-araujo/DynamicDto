using Microsoft.Extensions.Configuration;

namespace DynamicDtoCore
{
    internal static class ConfigurationHelper
    {
        public static IConfigurationRoot Configuration { get; private set; }
        public static bool UseDbParameterName { get; private set; } = false;
        public static string ParameterPrefix { get; private set; } = "@";
        public static string ConnectionName { get; private set; }
        public static string ProviderInfo { get; private set; }
        public static string ConnectionString { get; private set; }

        static ConfigurationHelper()
        {
            try
            {
                Configuration = new ConfigurationBuilder()
                                    .SetBasePath(Directory.GetCurrentDirectory())
                                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                                    .Build();
                ConnectionName = Configuration["Connection"];
                ConnectionString = Configuration.GetConnectionString(ConnectionName);
                ProviderInfo = Configuration[$"DbProviders:{ConnectionName}"];
                UseDbParameterName = bool.TryParse(Configuration["UseDbParameterName"], out bool useParam) && useParam;
                if (UseDbParameterName)
                {
                    ParameterPrefix = Configuration["DbParameterPrefix"] ?? "@";
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Could not load appsettings.json configuration.", ex);
            }
        }
    }
}