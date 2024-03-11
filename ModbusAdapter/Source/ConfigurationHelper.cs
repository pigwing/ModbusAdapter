using Microsoft.Extensions.Primitives;

namespace ModbusAdapter
{
    public class ConfigurationHelper(string configurationPath) : IAsyncDisposable
    {
        IDisposable? _callbackRegistration;

        public ConfigurationHelper() : this("configuration") { }
        public IConfiguration? Load(string fileName, string environmentName = "", bool reloadOnChange = false, Action<object>? changeTokenConsumer = null, IServiceProvider? serviceProvider = null)
        {
            var filePath = Path.Combine(AppContext.BaseDirectory, configurationPath);
            if (!Directory.Exists(filePath))
                return null;

            var builder = new ConfigurationBuilder()
                .SetBasePath(filePath)
                .AddJsonFile(fileName + ".json", true, reloadOnChange);

            if (!string.IsNullOrEmpty(environmentName))
            {
                builder.AddJsonFile(fileName + "." + environmentName + ".json", optional: true,
                    reloadOnChange: reloadOnChange);
            }

            IConfiguration configuration = builder.Build();
            if (changeTokenConsumer != null)
            {
                _callbackRegistration = ChangeToken.OnChange(configuration.GetReloadToken, changeTokenConsumer, new ConfigurationContext(serviceProvider, configuration));
            }

            return configuration;
        }

        public TConfiguration? Load<TConfiguration>(string fileName, string environmentName = "",
            bool reloadOnChange = false, Action<object>? changeTokenConsumer = null, IServiceProvider? serviceProvider = null)
        {
            IConfiguration? configuration = Load(fileName, environmentName, reloadOnChange, changeTokenConsumer, serviceProvider);
            if (configuration == null) return default;

            return configuration.Get<TConfiguration>();
        }

        public void Load<TConfiguration>(TConfiguration tConfiguration, string fileName,
            string environmentName = "", bool reloadOnChange = false, Action<object>? changeTokenConsumer = null, IServiceProvider? serviceProvider = null)
        {
            IConfiguration? configuration = Load(fileName, environmentName, reloadOnChange, changeTokenConsumer, serviceProvider);
            if (configuration == null) return;
            configuration.Bind(tConfiguration);
        }

        public ValueTask DisposeAsync()
        {
            _callbackRegistration?.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    public record ConfigurationContext(IServiceProvider? ServiceProvider, IConfiguration? Configuration)
    {
    }

}
