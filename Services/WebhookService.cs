using System.Collections.Concurrent;
using System.Text;
using HCC2RestClient.Configuration;
using HCC2RestClient.Models;
using HCC2RestClient.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection; // Add this for IServiceCollection
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace HCC2RestClient.Services;

public class WebhookService : IWebhookService
{
    private readonly HttpClient _httpClient;
    private readonly AppConfig _config;
    private readonly ConcurrentQueue<object> _messageQueue = new();
    private CancellationTokenSource _cts = new();
    private Task _webhookTask = Task.CompletedTask;
    private IWebHost? _webHost;
    private readonly Dictionary<string, WebhookMessage?> _subscribedTopics = new();

    public WebhookService(HttpClient httpClient, AppConfig config)
    {
        _httpClient = httpClient;
        _config = config;
        _httpClient.BaseAddress = new Uri(_config.BaseUrl);
    }

    /// <summary>
    /// Subscribes to specified topics for webhook notifications
    /// </summary>
    /// <param name="appName">Name of the application subscribing to topics</param>
    /// <param name="topics">List of topic FQNs to subscribe to</param>
    /// <param name="callbackUrl">URL where webhook notifications should be sent</param>
    /// <param name="includeOptional">Whether to include optional fields in notifications</param>
    /// <returns>True if subscription successful, false otherwise</returns>
    public async Task<bool> SubscribeAsync(string appName, List<string> topics, string callbackUrl, bool includeOptional)
    {
        var request = new WebhookSubscriptionRequest
        {
            CallbackApi = callbackUrl,
            Topics = topics,
            IncludeOptional = includeOptional
        };
        var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"/api/v1/message/subscription/{appName}", content);
        if (response.IsSuccessStatusCode && response.StatusCode == System.Net.HttpStatusCode.Created)
        {
            Logger.write(logLevel.debug, $"Webhook subscription successful for topics {string.Join(", ", topics)} on {callbackUrl}");
            return true;
        }
        Logger.write(logLevel.error, $"Failed to subscribe to topics {string.Join(", ", topics)}. Status: {response.StatusCode}");
        return false;
    }

    /// <summary>
    /// Sets up webhook handling by subscribing to required topics and starting the webhook server
    /// </summary>
    /// <param name="appName">Name of the application</param>
    /// <param name="configDataPoints">Dictionary of configuration data point mappings</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task SetupAsync(string appName, Dictionary<string, string> configDataPoints)
    {
        var topicsToSubscribe = new List<string>
        {
            configDataPoints["maxminrestartperiod"],
            configDataPoints["configrunningperiod"]
        };

        // Subscribe to topics
        await SubscribeAsync(appName, topicsToSubscribe, _config.WebhookUrl, includeOptional: false);

        // Track subscribed topics with explicit null values
        foreach (var topic in topicsToSubscribe)
        {
            _subscribedTopics.Add(topic, null);
        }

        // Start the webhook service
        Start();
        Logger.write(logLevel.info, $"Webhook subscriptions set up for topics: {string.Join(", ", topicsToSubscribe)}");
    }

    /// <summary>
    /// Starts the webhook server and begins listening for incoming webhook messages
    /// </summary>
    public void Start()
    {
        _cts = new CancellationTokenSource();

        // Extract webhook configuration
        var whConfig = _config.WebhookConfig;
        var host = whConfig.Host;
        var port = whConfig.Port;
        var suffix = whConfig.Suffix;

        // Define endpoint paths
        var testPath = $"{suffix}{whConfig.Test.Command}";
        var simpleMessagePath = $"{suffix}{whConfig.SimpleMessage.Command}";
        var setOfMessagesPath = $"{suffix}{whConfig.SetOfMessages.Command}";
        var advancedMessagesPath = $"{suffix}{whConfig.AdvancedMessages.Command}";

        // Start an ASP.NET Core web host
        _webHost = new WebHostBuilder()
            .UseKestrel()
            .ConfigureServices(services =>
            {
                // Register routing services
                services.AddRouting();
            })
            .Configure(app =>
            {
                // Enable routing
                app.UseRouting();

                // Define endpoints
                app.UseEndpoints(endpoints =>
                {
                    // Test endpoint (GET)
                    endpoints.MapGet(testPath, async context =>
                    {
                        context.Response.StatusCode = 200;
                        await context.Response.WriteAsync("OK");
                    });

                    // Simple message endpoint (POST)
                    endpoints.MapPost(simpleMessagePath, HandleSimpleMessageAsync);

                    // Set of messages endpoint (POST)
                    endpoints.MapPost(setOfMessagesPath, HandleSimpleMessageAsync);

                    // Advanced messages endpoint (POST)
                    endpoints.MapPost(advancedMessagesPath, HandleAdvancedMessageAsync);

                    // Fallback for unmatched routes
                    endpoints.MapFallback(async context =>
                    {
                        context.Response.StatusCode = 404;
                        await context.Response.WriteAsync("Not Found");
                    });
                });
            })
            .UseUrls($"{whConfig.Protocol}://{host}:{port}")
            .Build();

        _webhookTask = _webHost.StartAsync(_cts.Token);
        Logger.write(logLevel.info, $"Webhook server started at {whConfig.Protocol}://{host}:{port}{suffix}");
    }

    /// <summary>
    /// Stops the webhook server and cleans up resources
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
        _webHost?.StopAsync().Wait();
        _webhookTask?.Wait();
        Logger.write(logLevel.debug, "Webhook service stopped");
    }

    /// <summary>
    /// Handles incoming simple webhook messages
    /// </summary>
    /// <param name="context">The HTTP context containing the webhook payload</param>
    /// <returns>Task representing the asynchronous operation</returns>
    private async Task HandleSimpleMessageAsync(HttpContext context)
    {
        try
        {
            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var message = JsonConvert.DeserializeObject<WebhookMessage>(body);
            if (message != null && !string.IsNullOrEmpty(message.Topic))
            {
                _messageQueue.Enqueue(message);
                Logger.write(logLevel.debug, $"Received simple webhook message for topic {message.Topic}: {message.Value}");
            }
            context.Response.StatusCode = 200;
            await context.Response.WriteAsync("{\"status\": \"OK\"}");
        }
        catch (Exception ex)
        {
            Logger.write(logLevel.error, $"Failed to process simple webhook message: {ex.Message}");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("{\"status\": \"Error\", \"detail\": \"Internal Server Error\"}");
        }
    }

    /// <summary>
    /// Handles incoming advanced webhook messages with additional metadata
    /// </summary>
    /// <param name="context">The HTTP context containing the webhook payload</param>
    /// <returns>Task representing the asynchronous operation</returns>
    private async Task HandleAdvancedMessageAsync(HttpContext context)
    {
        try
        {
            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var message = JsonConvert.DeserializeObject<ReadAdvancedResponse>(body);
            if (message != null && !string.IsNullOrEmpty(message.Topic))
            {
                _messageQueue.Enqueue(message);
                Logger.write(logLevel.debug, $"Received advanced webhook message for topic {message.Topic}");
            }
            context.Response.StatusCode = 200;
            await context.Response.WriteAsync("{\"status\": \"OK\"}");
        }
        catch (Exception ex)
        {
            Logger.write(logLevel.error, $"Failed to process advanced webhook message: {ex.Message}");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("{\"status\": \"Error\", \"detail\": \"Internal Server Error\"}");
        }
    }

    /// <summary>
    /// Attempts to dequeue a message from the webhook message queue
    /// </summary>
    /// <param name="message">The dequeued message, if available</param>
    /// <returns>True if a message was dequeued, false if queue is empty</returns>
    public bool TryDequeue(out object message)
    {
        message = new object(); // Default value in case queue is empty
        if (_messageQueue.TryDequeue(out var result))
        {
            message = result;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Manually enqueues a message to the webhook message queue
    /// </summary>
    /// <param name="message">The message to enqueue</param>
    public void EnqueueMessage(object? message)
    {
        if (message != null)
        {
            _messageQueue.Enqueue(message);
            Logger.write(logLevel.debug, "Enqueued webhook message");
        }
    }

    /// <summary>
    /// Processes all queued webhook messages and updates configuration values
    /// </summary>
    /// <param name="period">Reference to the running period configuration value</param>
    /// <param name="restartMinutePeriod">Reference to the restart period configuration value</param>
    /// <param name="configDataPoints">Dictionary of configuration data point mappings</param>
    public void ProcessMessages(ref double period, ref double restartMinutePeriod, Dictionary<string, string> configDataPoints)
    {
        bool haveNewData = false;
        
        // Process webhook messages to update subscribed topics
        while (TryDequeue(out var message))
        {
            if (message is WebhookMessage webhookMessage)
            {
                ProcessSimpleMessage(webhookMessage, ref haveNewData);
            }
            else if (message is ReadAdvancedResponse advancedMessage)
            {
                ProcessAdvancedMessage(advancedMessage, ref haveNewData);
            }
            else
            {
                Logger.write(logLevel.warning, $"Received unknown webhook message type: {message?.GetType().Name}");
            }
        }

        if (!haveNewData) return;

        UpdateConfigValues(ref period, ref restartMinutePeriod, configDataPoints);
    }

    /// <summary>
    /// Processes a simple webhook message and updates the subscribed topics if applicable
    /// </summary>
    /// <param name="webhookMessage">The simple webhook message to process</param>
    /// <param name="haveNewData">Reference to flag indicating if new data was received</param>
    private void ProcessSimpleMessage(WebhookMessage webhookMessage, ref bool haveNewData)
    {
        if (_subscribedTopics.ContainsKey(webhookMessage.Topic))
        {
            haveNewData = true;
            _subscribedTopics[webhookMessage.Topic] = webhookMessage;
            Logger.write(logLevel.info, $"Updated config value for topic {webhookMessage.Topic}: {webhookMessage.Value}");
        }
        else
        {
            Logger.write(logLevel.warning, $"Received webhook message for unsubscribed topic {webhookMessage.Topic}");
        }
    }

    /// <summary>
    /// Processes an advanced webhook message and updates the subscribed topics if applicable
    /// </summary>
    /// <param name="advancedMessage">The advanced webhook message to process</param>
    /// <param name="haveNewData">Reference to flag indicating if new data was received</param>
    private void ProcessAdvancedMessage(ReadAdvancedResponse advancedMessage, ref bool haveNewData)
    {
        if (_subscribedTopics.ContainsKey(advancedMessage.Topic))
        {
            haveNewData = true;
            var value = advancedMessage.Datapoints?.FirstOrDefault()?.Values?.FirstOrDefault();
            var convertedMessage = new WebhookMessage
            {
                Topic = advancedMessage.Topic,
                Value = value ?? string.Empty
            };
            _subscribedTopics[advancedMessage.Topic] = convertedMessage;
            Logger.write(logLevel.info, $"Updated config value for topic {convertedMessage.Topic}: {convertedMessage.Value}");
        }
        else
        {
            Logger.write(logLevel.warning, $"Received advanced webhook message for unsubscribed topic {advancedMessage.Topic}");
        }
    }

    /// <summary>
    /// Updates configuration values based on received webhook messages
    /// </summary>
    /// <param name="period">Reference to the running period configuration value</param>
    /// <param name="restartMinutePeriod">Reference to the restart period configuration value</param>
    /// <param name="configDataPoints">Dictionary of configuration data point mappings</param>
    private void UpdateConfigValues(ref double period, ref double restartMinutePeriod, Dictionary<string, string> configDataPoints)
    {
        var configRunningPeriodTopic = configDataPoints["configrunningperiod"];
        var maxMinRestartPeriodTopic = configDataPoints["maxminrestartperiod"];

        if (_subscribedTopics.TryGetValue(configRunningPeriodTopic, out var periodMessage) && periodMessage?.Value != null)
        {
            period = Convert.ToDouble(periodMessage.Value);
            Logger.write(logLevel.debug, $"Using configrunningperiod from webhook: {period}");
        }

        if (_subscribedTopics.TryGetValue(maxMinRestartPeriodTopic, out var restartMessage) && restartMessage?.Value != null)
        {
            restartMinutePeriod = Convert.ToDouble(restartMessage.Value);
            Logger.write(logLevel.debug, $"Using maxminrestartperiod from webhook: {restartMinutePeriod}");
        }
    }
}