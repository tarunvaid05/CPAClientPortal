namespace JyotiIyerCPA.Filters;

/// <summary>
/// Endpoint filter that validates API key authentication for the warmup endpoint.
/// Expects X-Warmup-Key header to match configured Warmup:ApiKey value.
/// </summary>
public class WarmupAuthenticationFilter : IEndpointFilter
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<WarmupAuthenticationFilter> _logger;

    public WarmupAuthenticationFilter(IConfiguration configuration, ILogger<WarmupAuthenticationFilter> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // Extract X-Warmup-Key header
        if (!httpContext.Request.Headers.TryGetValue("X-Warmup-Key", out var providedKey))
        {
            _logger.LogWarning("Warmup endpoint accessed without X-Warmup-Key header from {IpAddress}",
                httpContext.Connection.RemoteIpAddress);
            return Results.Unauthorized();
        }

        // Get configured API key
        var configuredKey = _configuration["Warmup:ApiKey"];

        if (string.IsNullOrEmpty(configuredKey))
        {
            _logger.LogError("Warmup:ApiKey is not configured. Rejecting request.");
            return Results.StatusCode(500);
        }

        // Validate API key
        if (!string.Equals(providedKey.ToString(), configuredKey, StringComparison.Ordinal))
        {
            _logger.LogWarning("Warmup endpoint accessed with invalid API key from {IpAddress}",
                httpContext.Connection.RemoteIpAddress);
            return Results.StatusCode(403);
        }

        // Valid authentication - proceed to endpoint
        return await next(context);
    }
}
