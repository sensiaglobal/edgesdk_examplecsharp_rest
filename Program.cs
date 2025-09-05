using HCC2RestClient;
using HCC2RestClient.Configuration;
using HCC2RestClient.Services;
using HCC2RestClient.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


var config = SetupConfig();

// Initialize logger with app name
Logger.AppName = config.AppName;
Logger.Init();
Logger.write(logLevel.info, "Application starting...");


IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Configure logging to suppress default output
        services.AddLogging(builder => 
        {
            builder.ClearProviders()  // Remove default providers
                  .SetMinimumLevel(LogLevel.None);  // Set minimum level to None
        });
        services.AddSingleton<App>();
        services.AddSingleton<IRestClientService, RestClientService>();
        services.AddSingleton<IHeartbeatService, HeartbeatService>();
        services.AddSingleton<IWebhookService, WebhookService>();
        services.AddHttpClient();        
        services.AddSingleton(config);
    })
    .Build();

var app = host.Services.GetRequiredService<App>();

// Start the App....
await app.RunAsync();

// Setup up the config, using any environment values if they exist
static AppConfig SetupConfig()
{
    var config = new AppConfig();

    // Base URL and URI Prefix handling
    var apiUrl = Environment.GetEnvironmentVariable("SDK2_API_URL") ?? config.BaseUrl;
    
    // Split the URI out
    int protocolEndIndex = apiUrl.IndexOf("://") + 3;
    int firstSlashAfterProtocolIndex = apiUrl.IndexOf('/', protocolEndIndex);
    if (firstSlashAfterProtocolIndex != -1)
    {
        config.BaseUrl = apiUrl.Substring(0, firstSlashAfterProtocolIndex);
        config.UriPrefix = apiUrl.Substring(firstSlashAfterProtocolIndex);
    }
    else
    {
        config.BaseUrl = apiUrl;
        config.UriPrefix = Environment.GetEnvironmentVariable("SDK2_URI_PREFIX") ?? config.UriPrefix;
    }

    // App name
    config.AppName = Environment.GetEnvironmentVariable("SDK2_APP_NAME") ?? config.AppName;

    // Timing configurations
    if (int.TryParse(Environment.GetEnvironmentVariable("SDK2_HEARTBEAT_PERIOD"), out int heartbeatPeriod))
        config.HeartbeatPeriodSeconds = heartbeatPeriod;
    
    if (int.TryParse(Environment.GetEnvironmentVariable("SDK2_RETRY_PERIOD"), out int retryPeriod))
        config.RetryPeriodSeconds = retryPeriod;
    
    if (int.TryParse(Environment.GetEnvironmentVariable("SDK2_MAX_RETRIES"), out int maxRetries))
        config.MaxRetries = maxRetries;

    // Basic webhook settings
    var callbackUrl = Environment.GetEnvironmentVariable("SDK2_CALLBACK_URL") ?? config.WebhookUrl;
    config.WebhookUrl = $"{callbackUrl}/simple_message";
    
    var useWebhooks = Environment.GetEnvironmentVariable("SDK2_USE_WEBHOOKS");
    if (!string.IsNullOrEmpty(useWebhooks))
    {
        config.WebhookEnabled = "true1yes".Contains(useWebhooks.ToLower());
    }
    // else keep the default value from AppConfig

    return config;
}