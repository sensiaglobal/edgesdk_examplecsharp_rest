namespace HCC2RestClient.Models;

public class ApiResult<T>
{
    public bool Success { get; set; }
    public T Data { get; set; } = default!;
    public string ErrorMessage { get; set; } = string.Empty;
    public int StatusCode { get; set; }

    public static ApiResult<T> Ok(T data) => 
        new() { Success = true, Data = data, StatusCode = 200 };

    public static ApiResult<T> Fail(string message, int statusCode = 500) => 
        new() { Success = false, ErrorMessage = message, StatusCode = statusCode };
}