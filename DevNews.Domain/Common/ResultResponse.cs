namespace DevNews.Domain.Common;
public class ResultResponse<T>(bool isSuccess, T data, string errorMessage)
{
    private bool IsSuccess { get; set; } = isSuccess;
    public T Data { get; private set; } = data;
    public string ErrorMessage { get; private set; } = errorMessage;

    public static ResultResponse<T> Success(T data)
    {
        return new ResultResponse<T>(true, data, null);
    }

    public static ResultResponse<T> Failure(string errorMessage)
    {
        return new ResultResponse<T>(false, default(T), errorMessage);
    }

    public override string ToString()
    {
        return IsSuccess ? $"Success: {Data}" : $"Failure: {ErrorMessage}";
    }
}
