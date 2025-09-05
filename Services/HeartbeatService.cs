using HCC2RestClient.Configuration;
using Microsoft.Extensions.Logging;

namespace HCC2RestClient.Services;

public class HeartbeatService : IHeartbeatService
{
    private readonly IRestClientService _restClient;
    private readonly ILogger<HeartbeatService> _logger;
    private readonly AppConfig _config;
    private CancellationTokenSource _cts = new();
    private bool _isUp = false;
    private int _periodSeconds;

    public HeartbeatService(IRestClientService restClient, ILogger<HeartbeatService> logger, AppConfig config)
    {
        _restClient = restClient;
        _logger = logger;
        _config = config;
        _periodSeconds = _config.HeartbeatPeriodSeconds;
    }

    public void Start(string appName)
    {
        _cts = new CancellationTokenSource();
        Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                await _restClient.SendHeartbeatAsync(appName, _isUp);
                _logger.LogDebug("Heartbeat sent: {IsUp}", _isUp);
                await Task.Delay(_periodSeconds * 1000, _cts.Token);
            }
        });
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    public void ChangePeriod(int periodSeconds)
    {
        _periodSeconds = periodSeconds;
    }

    public void ChangeState(bool isUp)
    {
        _isUp = isUp;
    }
}