using System.Text;
using HCC2RestClient.Configuration;
using HCC2RestClient.Models;
using HCC2RestClient.Utilities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace HCC2RestClient.Services;

/*
 * RestClientService provides communication with the HCC2 REST API
 * 
 * The service handles:
 * - Application definition and registration
 * - Data point configuration and management
 * - Status monitoring and heartbeat
 * - Value reading and writing
 * - Error handling and retry logic
 * 
 * Key operations:
 * 1. Define application and defaults
 * 2. Register configuration data points
 * 3. Register general data points
 * 4. Complete application registration
 * 5. Monitor provisioning status
 */

public class RestClientService : IRestClientService
{
    private readonly IHttpClientFactory clientFactory;
    private readonly AppConfig _config;
    private readonly HttpClient _httpClient;

    public RestClientService(IHttpClientFactory httpClientFactory, AppConfig config)
    {
        clientFactory = httpClientFactory;
        _config = config;
        
        // 1. Create and configure the HttpClient
        _httpClient = clientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri(_config.BaseUrl);
    }

    /// <summary>
    /// Waits for the server to become available by repeatedly checking its status
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts</param>
    /// <param name="retryDelaySeconds">Delay between retries in seconds</param>
    /// <returns>True if server becomes available, false if max retries reached</returns>
    public async Task<bool> WaitForServerAsync(int maxRetries = 24, int retryDelaySeconds = 5)
    {
        int retries = 0;
        ApiResult<bool> statusResult;
        
        Logger.write(logLevel.info, "Checking server status...");
        
        do {
            statusResult = await CheckServerStatusAsync();
            if (!statusResult.Success)
            {
                Logger.write(logLevel.warning, 
                    $"Server not responding (Status: {statusResult.StatusCode}): {statusResult.ErrorMessage}");
                await Task.Delay(retryDelaySeconds * 1000);
                retries++;
            }
        } while (!statusResult.Success && retries < maxRetries);

        if (retries >= maxRetries)
        {
            Logger.write(logLevel.critical, 
                $"Server check failed after {maxRetries} retries. Last error: {statusResult.ErrorMessage}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks the current status of the server by querying liveValue.state.this.core.0.up.
    /// </summary>
    /// <returns>ApiResult containing server status (true if up) or error details</returns>
    public async Task<ApiResult<bool>> CheckServerStatusAsync()
    {
        try
        {
            var request = new { topics = new[] { "liveValue.state.this.core.0.up." }, includeOptional = false };
            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_config.UriPrefix}/message/read", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<List<ReadResponse>>(responseContent);
                return ApiResult<bool>.Ok(result?.FirstOrDefault()?.Value as bool? ?? false);
            }

            Logger.write(logLevel.error, 
                $"Failed to check server status. Status:{response.StatusCode} Reason:{response.ReasonPhrase} Content:{responseContent}");
            return ApiResult<bool>.Fail(
                $"Server returned {response.StatusCode}: {response.ReasonPhrase}", 
                (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            Logger.write(logLevel.error, $"Network error checking server status: {ex.Message}");
            return ApiResult<bool>.Fail($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Logger.write(logLevel.error, $"Unexpected error checking server status: {ex.Message}");
            return ApiResult<bool>.Fail($"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Defines a new application with default settings in the HCC2
    /// </summary>
    /// <param name="appName">Name of the application to define</param>
    /// <returns>ApiResult indicating success or failure with error details</returns>
    public async Task<ApiResult<bool>> DefineAppAsync(string appName)
    {
        try
        {
            var response = await _httpClient.PutAsync($"{_config.UriPrefix}/app-creator/{appName}/defaults", null);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Logger.write(logLevel.info, $"App {appName} initialized");
                return ApiResult<bool>.Ok(true);
            }

            Logger.write(logLevel.error, 
                $"Failed to define app {appName}. Status:{response.StatusCode} Reason:{response.ReasonPhrase}");
            return ApiResult<bool>.Fail($"Server returned {response.StatusCode}: {response.ReasonPhrase}", 
                (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            Logger.write(logLevel.error, $"Error defining app: {ex.Message}");
            return ApiResult<bool>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Registers data points for an application in the specified endpoint category
    /// </summary>
    /// <param name="appName">Name of the application</param>
    /// <param name="dataPoints">List of data points to register</param>
    /// <param name="endpoint">Endpoint category (e.g., "config", "general")</param>
    /// <returns>ApiResult containing dictionary of topic to FQN mappings or error details</returns>
    public async Task<ApiResult<Dictionary<string, string>>> RegisterDataPointsAsync(string appName, List<RestDataPoint> dataPoints, string endpoint)
    {
        try
        {
            var request = new { tagsList = dataPoints };
            var jsonPayload = JsonConvert.SerializeObject(request);
            Logger.write(logLevel.debug, $"Request payload for {endpoint}: {jsonPayload}");

            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync($"{_config.UriPrefix}/app-creator/{appName}/datapoint/{endpoint}", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var regResponse = JsonConvert.DeserializeObject<RegistrationResponse>(responseContent);
                if (regResponse?.Content == null)
                {
                    return ApiResult<Dictionary<string, string>>.Fail("Invalid response format: missing content");
                }

                var datapointItems = JsonConvert.DeserializeObject<List<DataPointItemsResult>>(regResponse.Content);
                if (datapointItems == null)
                {
                    return ApiResult<Dictionary<string, string>>.Fail("Invalid response format: unable to parse content items");
                }

                // Create dictionary with only non-null values
                var result = datapointItems
                    .Select((ci, index) => new { DataPoint = dataPoints[index], ContentItem = ci })
                    .Where(x => x.ContentItem.FullDataPointName != null)
                    .ToDictionary(
                        x => x.DataPoint.Topic,
                        x => x.ContentItem.FullDataPointName!
                    );

                return ApiResult<Dictionary<string, string>>.Ok(result);
            }
            
            var errorMessage = new StringBuilder($"Failed to register data points for {endpoint}. Status code: {(int)response.StatusCode}");
            
            try
            {
                var errorResponse = JsonConvert.DeserializeObject<RegistrationResponse>(responseContent);
                if (errorResponse?.Content != null)
                {
                    errorMessage.AppendLine($" Error: {errorResponse.Msg}");
                    var errorItems = JsonConvert.DeserializeObject<List<DataPointItemsResult>>(errorResponse.Content);
                    
                    if (errorItems != null)
                    {
                        foreach (var item in errorItems.Where(i => i.Result == "Error"))
                        {
                            foreach (var message in item.Messages)
                            {
                                errorMessage.AppendLine($"Validation error for {item.FullDataPointName}: {message.Type} in {message.DisplayField} - {message.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage.AppendLine($"Failed to parse error response: {ex.Message}");
                Logger.write(logLevel.error, errorMessage.ToString());
            }
            
            return ApiResult<Dictionary<string, string>>.Fail(errorMessage.ToString(), (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            Logger.write(logLevel.error, $"Error registering data points: {ex.Message}");
            return ApiResult<Dictionary<string, string>>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Registers an application with the HCC2 (including any data points that have been defined))
    /// </summary>
    /// <param name="appName">Name of the application to register</param>
    /// <returns>ApiResult indicating success or failure with error details</returns>
    public async Task<ApiResult<bool>> RegisterAppAsync(string appName)
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_config.UriPrefix}/app-registration/{appName}?isComplexProvisioned=false", null);
            
            if (response.IsSuccessStatusCode)
            {
                return ApiResult<bool>.Ok(true);
            }

            return ApiResult<bool>.Fail($"Failed to register app. Status: {response.StatusCode}", 
                (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            Logger.write(logLevel.error, $"Error registering app: {ex.Message}");
            return ApiResult<bool>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Sends a heartbeat signal to indicate application health status
    /// </summary>
    /// <param name="appName">Name of the application</param>
    /// <param name="isUp">True if application is healthy, false otherwise</param>
    /// <returns>ApiResult indicating success or failure with error details</returns>
    public async Task<ApiResult<bool>> SendHeartbeatAsync(string appName, bool isUp)
    {
        try
        {
            var request = new { isUp };
            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync($"{_config.UriPrefix}/app-provision/{appName}", content);

            if (response.IsSuccessStatusCode)
            {
                return ApiResult<bool>.Ok(true);
            }

            return ApiResult<bool>.Fail($"Heartbeat failed. Status: {response.StatusCode}", 
                (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            Logger.write(logLevel.error, $"Error sending heartbeat: {ex.Message}");
            return ApiResult<bool>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Checks the current provisioning status of an application
    /// </summary>
    /// <param name="appName">Name of the application to check</param>
    /// <returns>ApiResult containing provision status or error details</returns>
    public async Task<ApiResult<ProvisionStatus>> CheckProvisionStatusAsync(string appName)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_config.UriPrefix}/app-provision/{appName}");
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var status = JsonConvert.DeserializeObject<ProvisionStatus>(content);
                if (status == null)
                {
                    return ApiResult<ProvisionStatus>.Fail("Invalid response format: unable to parse provision status");
                }
                return ApiResult<ProvisionStatus>.Ok(status);
            }

            return ApiResult<ProvisionStatus>.Fail($"Failed to check provision status. Status: {response.StatusCode}", 
                (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            Logger.write(logLevel.error, $"Error checking provision status: {ex.Message}");
            return ApiResult<ProvisionStatus>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Reads current values of specified data points
    /// </summary>
    /// <param name="topics">List of topic FQNs to read</param>
    /// <returns>ApiResult containing list of read responses or error details</returns>
    public async Task<ApiResult<List<ReadResponse>>> ReadDataPointsAsync(List<string> topics)
    {
        try
        {
            var request = new { topics };
            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_config.UriPrefix}/message/read", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<List<ReadResponse>>(responseContent);
                return ApiResult<List<ReadResponse>>.Ok(result ?? new List<ReadResponse>());
            }

            return ApiResult<List<ReadResponse>>.Fail($"Failed to read data points. Status: {response.StatusCode}", 
                (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            Logger.write(logLevel.error, $"Error reading data points: {ex.Message}");
            return ApiResult<List<ReadResponse>>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Reads data points with advanced metadata information
    /// </summary>
    /// <param name="topics">List of topic FQNs to read</param>
    /// <returns>ApiResult containing list of advanced read responses or error details</returns>
    public async Task<ApiResult<List<ReadAdvancedResponse>>> ReadDataPointsAdvancedAsync(List<string> topics)
    {
        try
        {
            var request = new { topics };
            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_config.UriPrefix}/message/read-advanced", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<List<ReadAdvancedResponse>>(responseContent);
                return ApiResult<List<ReadAdvancedResponse>>.Ok(result ?? new List<ReadAdvancedResponse>());
            }

            return ApiResult<List<ReadAdvancedResponse>>.Fail(
                $"Failed to read advanced data points. Status: {response.StatusCode}", 
                (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            Logger.write(logLevel.error, $"Error reading advanced data points: {ex.Message}");
            return ApiResult<List<ReadAdvancedResponse>>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Writes values to specified data points
    /// </summary>
    /// <param name="dataPoints">List of write requests containing topic and value pairs</param>
    /// <returns>ApiResult indicating success or failure with error details</returns>
    public async Task<ApiResult<bool>> WriteDataPointsAsync(List<WriteDataPointRequest> dataPoints)
    {
        try
        {
            var content = new StringContent(JsonConvert.SerializeObject(dataPoints), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_config.UriPrefix}/message/write", content);
            
            if (response.IsSuccessStatusCode)
            {
                Logger.write(logLevel.debug, $"Successfully wrote {dataPoints.Count} data points");
                return ApiResult<bool>.Ok(true);
            }

            Logger.write(logLevel.error, $"Failed to write data points. Status: {response.StatusCode}");
            return ApiResult<bool>.Fail($"Failed to write data points. Status: {response.StatusCode}", 
                (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            Logger.write(logLevel.error, $"Error writing data points: {ex.Message}");
            return ApiResult<bool>.Fail(ex.Message);
        }
    }

    public async Task<ApiResult<(Dictionary<string, string> ConfigPoints, Dictionary<string, string> GeneralPoints)>> 
        SetupApplicationAsync(string appName, List<RestDataPoint> configPoints, List<RestDataPoint> generalPoints)
    {
        try
        {
            // 1. Define app
            Logger.write(logLevel.info, "Defining app...");
            var defineResult = await DefineAppAsync(appName);
            if (!defineResult.Success)
            {
                return ApiResult<(Dictionary<string, string>, Dictionary<string, string>)>.Fail(
                    $"Failed to define app: {defineResult.ErrorMessage}", defineResult.StatusCode);
            }

            // 2. Register config data points
            Logger.write(logLevel.info, "Registering config data points...");
            var configResult = await RegisterDataPointsAsync(appName, configPoints, "config");
            if (!configResult.Success)
            {
                return ApiResult<(Dictionary<string, string>, Dictionary<string, string>)>.Fail(
                    $"Failed to register config points: {configResult.ErrorMessage}", configResult.StatusCode);
            }

            // 3. Register general data points
            Logger.write(logLevel.info, "Registering general data points...");
            var generalResult = await RegisterDataPointsAsync(appName, generalPoints, "general");
            if (!generalResult.Success)
            {
                return ApiResult<(Dictionary<string, string>, Dictionary<string, string>)>.Fail(
                    $"Failed to register general points: {generalResult.ErrorMessage}", generalResult.StatusCode);
            }

            // 4. Register app
            Logger.write(logLevel.info, "Registering app...");
            var registerResult = await RegisterAppAsync(appName);
            if (!registerResult.Success)
            {
                return ApiResult<(Dictionary<string, string>, Dictionary<string, string>)>.Fail(
                    $"Failed to register app: {registerResult.ErrorMessage}", registerResult.StatusCode);
            }

            return ApiResult<(Dictionary<string, string>, Dictionary<string, string>)>.Ok(
                (configResult.Data, generalResult.Data));
        }
        catch (Exception ex)
        {
            Logger.write(logLevel.error, $"Application setup failed: {ex.Message}");
            return ApiResult<(Dictionary<string, string>, Dictionary<string, string>)>.Fail(ex.Message);
        }
    }
}