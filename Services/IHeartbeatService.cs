namespace HCC2RestClient.Services;

public interface IHeartbeatService
{
    void Start(string appName);
    void Stop();
    void ChangePeriod(int periodSeconds);
    void ChangeState(bool isUp);
}