using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MidiKaval.Api.Models;

namespace MidiKaval.Api.Infrastructure;

public sealed class ApiEnvelopeFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var path = context.HttpContext.Request.Path;

        if (path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase)
            && context.Result is ObjectResult objectResult
            && objectResult.Value is not null
            && !IsAlreadyWrapped(objectResult.Value)
            && objectResult.StatusCode is null or >= 200 and < 300)
        {
            var requestId = context.HttpContext.Items[RequestIdMiddleware.RequestIdItemKey] as string
                ?? context.HttpContext.TraceIdentifier;

            objectResult.Value = WrapValue(objectResult.Value, requestId);
        }

        await next();
    }

    private static bool IsAlreadyWrapped(object value)
    {
        return value.GetType().IsGenericType
            && value.GetType().GetGenericTypeDefinition() == typeof(ApiResponse<>);
    }

    private static object WrapValue(object value, string requestId)
    {
        var method = typeof(ApiEnvelopeFilter).GetMethod(
            nameof(WrapTyped),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var generic = method.MakeGenericMethod(value.GetType());
        return generic.Invoke(null, [value, requestId])!;
    }

    private static ApiResponse<T> WrapTyped<T>(T value, string requestId) =>
        new(value, new ApiMeta { RequestId = requestId });
}
