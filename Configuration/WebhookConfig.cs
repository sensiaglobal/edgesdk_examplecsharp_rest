namespace HCC2RestClient.Configuration;

public class WebhookConfig
{
    public readonly string Host = "0.0.0.0";
    public readonly string Protocol = "http";
    public readonly string Suffix = "/webhook/v1/";
    public readonly string GroupTag = "Subscriptions";
    public readonly int Port = 8100;
    public WebhookOperation Test { get; set; } = new WebhookOperation { Command = "test", HttpMethod = "GET" };
    public WebhookOperation SimpleMessage { get; set; } = new WebhookOperation { Command = "simple_message", HttpMethod = "POST" };
    public WebhookOperation SetOfMessages { get; set; } = new WebhookOperation { Command = "set_of_messages", HttpMethod = "POST" };
    public WebhookOperation AdvancedMessages { get; set; } = new WebhookOperation { Command = "advanced_messages", HttpMethod = "POST" };
}
