using HCC2RestClient.Models;

namespace HCC2RestClient.Services;

public interface IWebhookService
{
    Task SetupAsync(string appName, Dictionary<string, string> configDataPoints);
    void ProcessMessages(ref double period, ref double restartMinutePeriod, Dictionary<string, string> configDataPoints);
    void Start();
    void Stop();
    bool TryDequeue(out object message);
}