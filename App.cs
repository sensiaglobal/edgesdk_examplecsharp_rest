using HCC2RestClient.Configuration;
using HCC2RestClient.Models;
using HCC2RestClient.Services;
using HCC2RestClient.Utilities;

namespace HCC2RestClient;

/// <summary>
/// Main application class that handles integration with HCC2 REST server.
/// Manages data point configuration, webhooks, heartbeat monitoring, and business logic execution.
/// </summary>
public class App
{
    // Service dependencies injected through constructor
    private readonly IRestClientService _restClient;      // Handles REST API communication
    private readonly IHeartbeatService _heartbeat;        // Manages application health monitoring
    private readonly IWebhookService _webhookService;     // Handles webhook subscriptions and processing
    private readonly AppConfig _config;                   // Application configuration settings
    
    // Data storage for application state
    private Dictionary<string, string> _configDataPoints = new();    // Maps config point names to FQNs
    private Dictionary<string, string> _generalDataPoints = new();   // Maps general point names to FQNs
    private Dictionary<string, WebhookMessage> _subscribedTopics;    // Tracks subscribed webhook topics

    /// <summary>
    /// Initializes a new instance of the App class with required services.
    /// </summary>
    /// <param name="restClient">Service for REST API communication</param>
    /// <param name="heartbeat">Service for health monitoring</param>
    /// <param name="webhookService">Service for webhook handling</param>
    /// <param name="config">Application configuration</param>
    public App(IRestClientService restClient, IHeartbeatService heartbeat, IWebhookService webhookService, AppConfig config)
    {
        _restClient = restClient;
        _heartbeat = heartbeat;
        _webhookService = webhookService;
        _config = config;
        _subscribedTopics = new Dictionary<string, WebhookMessage>();
    }

#region Application Setup
    /// <summary>
    /// Main entry point for the application. Handles initialization, setup, and starts the main business logic loop.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task RunAsync()
    {
        try
        {
            // Test REST server connectivity with configured retry policy
            if (!await _restClient.WaitForServerAsync(_config.MaxRetries, _config.RetryPeriodSeconds))
            {
                return;
            }

            // Define configuration data points for runtime parameters
            var configPoints = new List<RestDataPoint>
            {
                CreateDataPoint("configrunningperiod", "Running Period", "Double"),       // Controls main loop interval
                CreateDataPoint("maxminrestartperiod", "Restart Period", "Double")       // Controls statistics reset interval
            };

            // Define general data points for system metrics
            var generalPoints = new List<RestDataPoint>
            {
                CreateDataPoint("runcounter",  "Run Counter", "Double"),                  // Counts iterations since last reset
                CreateDataPoint("lastruntime", "Last Runtime", "String"),                 // Timestamp of last execution
                CreateDataPoint("cpuusagecurrent", "CPU Usage Current", "Double", "PRCNT"), // Current CPU utilization
                CreateDataPoint("cpuusagemax", "CPU Usage Max", "Double", "PRCNT"),      // Peak CPU usage since reset
                CreateDataPoint("cpuusagemin", "CPU Usage Min", "Double", "PRCNT"),      // Minimum CPU usage since reset
                CreateDataPoint("memoryusagecurrent", "Memory Usage Current", "Double", "BYTE"), // Current memory usage
                CreateDataPoint("memoryusagemax", "Memory Usage Max", "Double", "BYTE"),  // Peak memory usage since reset
                CreateDataPoint("memoryusagemin", "Memory Usage Min", "Double", "BYTE"),  // Minimum memory usage since reset
                CreateDataPoint("temperature", "Temperature", "Double", "TEMP"),          // Current CPU temperature
                CreateDataPoint("failcount", "Fail Count", "Uint32")                     // Number of execution failures
            };
            
            // Register application and data points with HCC2
            var setupResult = await _restClient.SetupApplicationAsync(_config.AppName, configPoints, generalPoints);
            if (!setupResult.Success)
            {
                Logger.write(logLevel.critical, 
                    $"Failed to setup application: {setupResult.ErrorMessage} (Status: {setupResult.StatusCode})");
                return;
            }
            // Store FQN mappings for future data access
            _configDataPoints = setupResult.Data.ConfigPoints;
            _generalDataPoints = setupResult.Data.GeneralPoints;

            if (_configDataPoints.Count == 0 && _generalDataPoints.Count == 0)
            {
                Logger.write(logLevel.info, "No data points to process, exiting");
                return;
            }

            // Initialize webhook handling if enabled
            if (_config.WebhookEnabled && _configDataPoints.Count > 0)
            {
                Logger.write(logLevel.info, "Webhook enabled, setting up webhook handling...");
                await _webhookService.SetupAsync(_config.AppName, _configDataPoints);
            }
            else
                Logger.write(logLevel.info, "REST read enabled...");

            // Allow time for core registration to complete
            Logger.write(logLevel.info, "Delaying 5 Seconds : Core Application Registration");
            Thread.Sleep(5000);

            // Start health monitoring
            Logger.write(logLevel.info, "Starting heartbeat...");
            _heartbeat.Start(_config.AppName);

            // Wait for provisioning completion
            while (true)
            {
                var provisionResult = await _restClient.CheckProvisionStatusAsync(_config.AppName);
                if (!provisionResult.Success)
                {
                    Logger.write(logLevel.error, 
                        $"Failed to check provision status: {provisionResult.ErrorMessage}");
                    await Task.Delay(_config.RetryPeriodSeconds * 1000);
                    continue;
                }

                if (provisionResult.Data.HasNewConfig || _configDataPoints.Count == 0) break;
                Logger.write(logLevel.info, "Waiting for configuration...");
                await Task.Delay(_config.RetryPeriodSeconds * 1000);
            }
            
            // Initialize with current data point values
            Logger.write(logLevel.info, "Reading initial data points...");
            var topics = _configDataPoints.Values.Concat(_generalDataPoints.Values).ToList();
            var readResult = await _restClient.ReadDataPointsAsync(topics);
            if (!readResult.Success)
            {
                Logger.write(logLevel.critical, 
                    $"Failed to read initial data points: {readResult.ErrorMessage} (Status: {readResult.StatusCode})");
                return;
            }

            if (readResult.Data == null || readResult.Data.Count == 0)
            {
                Logger.write(logLevel.critical, "No data points returned from initial read");
                return;
            }

            // Update heartbeat settings and start main logic
            Logger.write(logLevel.info, "Initial data points read successfully, asserting a healthy heartbeat...");
            _heartbeat.ChangeState(true);
            _heartbeat.ChangePeriod(30);        // Switch to longer heartbeat interval

            await RunBusinessLogicAsync();      // Begin main execution loop
        }
        catch (Exception ex)
        {
            Logger.write(logLevel.critical, $"Fatal error in application: {ex.Message}");
            Logger.write(logLevel.debug, $"Stack trace: {ex.StackTrace}");
            
            // Cleanup on error
            _heartbeat?.Stop();
            if (_config.WebhookEnabled)
            {
                _webhookService?.Stop();
            }            
        }
    }
#endregion Application Setup

#region Application Logic
    /// <summary>
    /// Main business logic loop that monitors system metrics and publishes them to HCC2.
    /// Handles configuration updates through webhooks or REST polling.
    /// </summary>
    private async Task RunBusinessLogicAsync()
    {
        // Initialize metric tracking variables
        double runCounter = 1;
        uint failCount = 0;
        double cpuUsage = 0, cpuUsageMax = 0, cpuUsageMin = 0;
        double memoryUsage = 0, memoryUsageMax = 0, memoryUsageMin = 0;
        double temperature = 0;

        DateTime lastReset = DateTime.Now;

        // Default configuration values
        double period = 10;              // Main loop interval in seconds
        double restartMinutePeriod = 5;  // Statistics reset interval in minutes

        Logger.write(logLevel.info, "Starting business logic...");
        while (true)
        {
            try
            {
                // Update configuration via webhook or REST
                if (_config.WebhookEnabled)
                {
                    _webhookService.ProcessMessages(ref period, ref restartMinutePeriod, _configDataPoints);
                }
                else
                {
                    var configResult = await _restClient.ReadDataPointsAsync(_configDataPoints.Values.ToList());
                    if (!configResult.Success || configResult.Data == null)
                    {
                        Logger.write(logLevel.error, 
                            $"Failed to read config values: {configResult.ErrorMessage}");
                        continue;
                    }

                    // Extract configuration values from response
                    var periodValue = configResult.Data.FirstOrDefault(v => v?.Topic?.Contains("configrunningperiod") == true);
                    var restartValue = configResult.Data.FirstOrDefault(v => v?.Topic?.Contains("maxminrestartperiod") == true);

                    if (periodValue?.Value != null)
                    {
                        period = Convert.ToDouble(periodValue.Value);
                    }
                    if (restartValue?.Value != null)
                    {
                        restartMinutePeriod = Convert.ToDouble(restartValue.Value);
                    }
                    Logger.write(logLevel.debug, $"Read config values directly over REST: period={period}, restartPeriod={restartMinutePeriod}");
                }

                // Enforce configuration value limits
                period = Math.Clamp(period, 1, 60);                   // 1-60 seconds
                restartMinutePeriod = Math.Clamp(restartMinutePeriod, 1, 24*60);  // 1 minute to 24 hours

                // Check for statistics reset interval
                DateTime now = DateTime.Now;
                if ((now - lastReset).TotalMinutes >= restartMinutePeriod)
                {
                    runCounter = 1;
                    lastReset = now;
                    Logger.write(logLevel.info, $"Resetting stats {now}");
                }

                // Read current system metrics
                var readings = await _restClient.ReadDataPointsAdvancedAsync(new List<string>
                {
                    "liveValue.diagnostics.this.core.0.cpuUsage|",
                    "liveValue.diagnostics.this.core.0.memoryUsage|",
                    "liveValue.diagnostics.this.io.0.temperature.cpu."
                });

                // Extract metric values from response data (making assumptions about structure and data quality)
                memoryUsage = readings.Data
                    .Where(r => r.Topic.Contains("memoryUsage") && r.Datapoints != null && r.Datapoints.Any(d => d.DataPointName == "memoryTotal."))
                    .SelectMany(r => r.Datapoints)
                    .Where(d => d.DataPointName == "memoryTotal.")
                    .Select(d => Convert.ToDouble(d.Values.FirstOrDefault()))
                    .FirstOrDefault();

                cpuUsage = readings.Data
                    .Where(r => r.Topic.Contains("cpuUsage") && r.Datapoints != null && r.Datapoints.Any(d => d.DataPointName == "total."))
                    .SelectMany(r => r.Datapoints)
                    .Where(d => d.DataPointName == "total.")
                    .Select(d => Convert.ToDouble(d.Values.FirstOrDefault()))
                    .FirstOrDefault();

                temperature = readings.Data
                    .Where(r => r.Topic.Contains("temperature") && r.Datapoints != null && r.Datapoints.Any(d => d.DataPointName == ""))
                    .SelectMany(r => r.Datapoints)
                    .Where(d => d.DataPointName == "")
                    .Select(d => Convert.ToDouble(d.Values.FirstOrDefault()))
                    .FirstOrDefault();

                // Update min/max tracking
                if (runCounter == 1)        // Reset statistics
                {
                    cpuUsageMax = cpuUsageMin = cpuUsage;
                    memoryUsageMax = memoryUsageMin = memoryUsage;
                    failCount = 0;
                }
                else                        // Update running statistics
                {
                    cpuUsageMax = Math.Max(cpuUsage, cpuUsageMax);
                    cpuUsageMin = Math.Min(cpuUsage, cpuUsageMin);
                    memoryUsageMax = Math.Max(memoryUsage, memoryUsageMax);
                    memoryUsageMin = Math.Min(memoryUsage, memoryUsageMin);
                }

                // Prepare metrics for publishing
                var valuesToWrite = new Dictionary<string, object>
                {
                    { _generalDataPoints["runcounter"], runCounter },
                    { _generalDataPoints["lastruntime"], DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                    { _generalDataPoints["cpuusagecurrent"], cpuUsage },
                    { _generalDataPoints["cpuusagemax"], cpuUsageMax },
                    { _generalDataPoints["cpuusagemin"], cpuUsageMin },
                    { _generalDataPoints["memoryusagecurrent"], memoryUsage },
                    { _generalDataPoints["memoryusagemax"], memoryUsageMax },
                    { _generalDataPoints["memoryusagemin"], memoryUsageMin },
                    { _generalDataPoints["temperature"], temperature },
                    { _generalDataPoints["failcount"], failCount }
                };

                // Convert to write requests
                var dataPoints = valuesToWrite.Select(kv => new WriteDataPointRequest
                {
                    Topic = kv.Key,
                    Value = kv.Value,
                    MsgSource = "REST",
                    Quality = Quality.Good,
                    TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
                }).ToList();

                // Publish metrics to HCC2
                var writeResult = await _restClient.WriteDataPointsAsync(dataPoints);
                if (!writeResult.Success)
                {
                    Logger.write(logLevel.error, 
                        $"Failed to write data points: {writeResult.ErrorMessage} (Status: {writeResult.StatusCode})");
                    _heartbeat.ChangeState(false);
                    continue;
                }

                Logger.write(logLevel.info, $"Run {runCounter}: CPU={cpuUsage}, Memory={memoryUsage}, Temp={temperature}");

                runCounter++;
                _heartbeat.ChangeState(true);       // Signal successful execution
            }
            catch (Exception e)
            {
                failCount++;
                _heartbeat.ChangeState(false);      // Signal execution failure
                Logger.write(logLevel.error, $"Business logic fail count {failCount}, reason {e.Message} {e.StackTrace}");
            }

            // Wait for next execution cycle
            await Task.Delay((int)period * 1000);
        }
    }
#endregion Application Logic

#region Datapoint Creator Helper
    /// <summary>
    /// Creates a new DataPoint instance with the specified parameters.
    /// </summary>
    /// <param name="topic">The topic name for the data point</param>
    /// <param name="displayName">The display name shown in the UI</param>
    /// <param name="dataType">The data type (must be one of the supported types)</param>
    /// <param name="units">The unit of measurement (defaults to "NONE")</param>
    /// <returns>A configured DataPoint instance</returns>
    /// <exception cref="ArgumentException">Thrown if dataType is not supported</exception>
    private RestDataPoint CreateDataPoint(string topic, string displayName, string dataType, string units = "NONE")
    {
        // List of supported data types
        var validTypes = new[] { "Bool", "Int8", "Int16", "Int32", "Int64", "Uint8", 
                                "Uint16", "Uint32", "Uint64", "Float", "Double", 
                                "Enum", "String", "JSON", "Tag" };
                                
        if (!validTypes.Contains(dataType))
        {
            throw new ArgumentException($"Invalid data type: {dataType}. Must be one of: {string.Join(", ", validTypes)}");
        }

        // Create unique identifier for the data point
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var uniqueId = (timestamp % 100000).ToString("D5");
        var identifier = $"dp_{uniqueId}";

        return new RestDataPoint
        {
            Topic = topic,
            Metadata = new RestTagMetaData 
            { 
                DataType = dataType,
                IsOutput = "true",
                Unit = units,
            },
            UnityUI = new RestTagUnityUI 
            { 
                DisplayName = displayName,
                ShortDisplayName = identifier
            }
        };
    }
#endregion Datapoint Creator Helper
}