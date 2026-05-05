using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace PropertyManagement.Api.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _log;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> log)
    {
        _next = next; _log = log;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (ValidationException vex)
        {
            ctx.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            var problem = new ValidationProblemDetails(vex.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray()))
            {
                Title = "Validation failed",
                Status = (int)HttpStatusCode.BadRequest,
                Type = "https://httpstatuses.com/400"
            };
            await Write(ctx, problem);
        }
        catch (UnauthorizedAccessException uex)
        {
            ctx.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            await Write(ctx, new ProblemDetails
            {
                Title = "Forbidden",
                Detail = uex.Message,
                Status = (int)HttpStatusCode.Forbidden
            });
        }
        catch (KeyNotFoundException knfe)
        {
            ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
            await Write(ctx, new ProblemDetails
            {
                Title = "Not Found",
                Detail = knfe.Message,
                Status = (int)HttpStatusCode.NotFound
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled error processing {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
            ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await Write(ctx, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = ex.Message,
                Status = (int)HttpStatusCode.InternalServerError
            });
        }
    }

    private static async Task Write(HttpContext ctx, object payload)
    {
        ctx.Response.ContentType = "application/problem+json";
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await ctx.Response.WriteAsync(json);
    }
}
