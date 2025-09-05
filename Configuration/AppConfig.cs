using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace HCC2RestClient.Configuration;

public class AppConfig 
{
    private string? _webhookUrl;
    
    public string BaseUrl { get; set; } = "http://hcc2RestServer_0:7071";
    public string AppName { get; set; } = "courseNetApp";
    public int HeartbeatPeriodSeconds { get; set; } = 10;
    public int RetryPeriodSeconds { get; set; } = 5;
    public int MaxRetries { get; set; } = 24; // 2 minutes with 5s retries
    public bool WebhookEnabled { get; set; } = false;
    public WebhookConfig WebhookConfig { get; set; } = new WebhookConfig();
    public string UriPrefix { get; internal set; } = "/api/v1";
    
    public string WebhookUrl
    {
        get => _webhookUrl ?? $"http://{AppName.ToLower()}:8100/webhook/v1";
        set => _webhookUrl = value;
    }
}
