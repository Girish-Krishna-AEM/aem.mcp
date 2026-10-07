namespace CoordinatorAgent.Services;

public class McpServerException : Exception
{
    public int StatusCode { get; }

    public McpServerException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}
