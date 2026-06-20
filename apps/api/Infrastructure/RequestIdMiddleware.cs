namespace MidiKaval.Api.Infrastructure;

public sealed class RequestIdMiddleware(RequestDelegate next)
{
    public const string RequestIdItemKey = "RequestId";

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = Guid.NewGuid().ToString();
        context.Items[RequestIdItemKey] = requestId;
        context.Response.Headers["X-Request-Id"] = requestId;
        await next(context);
    }
}
