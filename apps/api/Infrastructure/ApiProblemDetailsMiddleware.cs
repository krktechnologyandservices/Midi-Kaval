using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace MidiKaval.Api.Infrastructure;

public sealed class ApiProblemDetailsMiddleware(RequestDelegate next)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        if (context.Response.HasStarted
            || context.Response.StatusCode != StatusCodes.Status404NotFound
            || !context.Request.Path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Not Found",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            Detail = $"The resource '{context.Request.Path}' was not found.",
        };

        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, JsonOptions));
    }
}
