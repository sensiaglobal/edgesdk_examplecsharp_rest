using HCC2RestClient.Models;

namespace HCC2RestClient.Services;

public interface IRestClientService
{
    Task<ApiResult<(Dictionary<string, string> ConfigPoints, Dictionary<string, string> GeneralPoints)>> 
        SetupApplicationAsync(string appName, List<RestDataPoint> configPoints, List<RestDataPoint> generalPoints);

    Task<ApiResult<bool>> CheckServerStatusAsync();
    Task<ApiResult<bool>> DefineAppAsync(string appName);
    Task<ApiResult<Dictionary<string, string>>> RegisterDataPointsAsync(string appName, List<RestDataPoint> dataPoints, string endpoint);
    Task<ApiResult<bool>> RegisterAppAsync(string appName);
    Task<ApiResult<bool>> SendHeartbeatAsync(string appName, bool isUp);
    Task<ApiResult<ProvisionStatus>> CheckProvisionStatusAsync(string appName);
    Task<ApiResult<List<ReadResponse>>> ReadDataPointsAsync(List<string> topics);
    Task<ApiResult<List<ReadAdvancedResponse>>> ReadDataPointsAdvancedAsync(List<string> topics);
    Task<ApiResult<bool>> WriteDataPointsAsync(List<WriteDataPointRequest> dataPoints);
    Task<bool> WaitForServerAsync(int maxRetries = 24, int retryDelaySeconds = 5);

}