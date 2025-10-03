namespace McpCore;

public class Response
{
    private static readonly Response OkResponse = new() { Success = true };
    private static readonly Response FailedResponse = new() { Success = false };

    public bool Success { get; set; }
    public string? Message { get; set; }

    public static Response Ok()
    {
        return OkResponse;
    }

    public static Response Ok(string message)
    {
        return new Response { Success = true, Message = message };
    }

    public static Response Ok<T>(string message, T value)
    {
        return new Response<T>(true, message, value);
    }

    public static Response Ok<T>(T value)
    {
        return new Response<T>(true, null, value);
    }

    public static Response Fail()
    {
        return FailedResponse;
    }

    public static Response Fail(string message)
    {
        return new Response { Success = false, Message = message };
    }
}


public class Response<T> : Response
{
    public Response()
    {
        Value = default;
    }

    public Response(bool success, string? message, T value)
    {
        Message = message;
        Success = success;
        Value = value;
    }

    public Response(T value)
    {
        Success = true;
        Value = value;
    }

    public T? Value { get; set; }
}
