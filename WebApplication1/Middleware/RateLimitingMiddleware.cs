using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading.Tasks;

namespace WebApplication1.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IDistributedCache _cache;
        private readonly ILogger<RateLimitingMiddleware> _logger;
        private readonly int _requestLimit = 10;
        private readonly TimeSpan _timeWindow = TimeSpan.FromMinutes(1);

        public RateLimitingMiddleware(RequestDelegate next, IDistributedCache cache, ILogger<RateLimitingMiddleware> logger)
        {
            _next = next;
            _cache = cache;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower();
            if (path == null || !path.StartsWith("/api/user"))
            {
                await _next(context);
                return;
            }

            var clientIp = context.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrEmpty(clientIp))
            {
                _logger.LogWarning("Could not determine client IP.");
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = "Client IP not found" });
                return;
            }

            var cacheKey = $"RateLimit:{clientIp}:{path}";
            _logger.LogInformation("Checking Redis cache key: {CacheKey}", cacheKey);
            var cacheEntryJson = await _cache.GetStringAsync(cacheKey);
            var cacheEntry = cacheEntryJson != null
                ? JsonSerializer.Deserialize<RateLimitEntry>(cacheEntryJson)
                : new RateLimitEntry { RequestCount = 0, WindowStart = DateTime.UtcNow };

            _logger.LogInformation("Cache entry for {CacheKey}: Count={Count}, WindowStart={WindowStart}",
                cacheKey, cacheEntry.RequestCount, cacheEntry.WindowStart);

            if (DateTime.UtcNow > cacheEntry.WindowStart.Add(_timeWindow))
            {
                cacheEntry.RequestCount = 1;
                cacheEntry.WindowStart = DateTime.UtcNow;
                _logger.LogInformation("Resetting rate limit for {CacheKey}", cacheKey);
            }
            else if (cacheEntry.RequestCount >= _requestLimit)
            {
                _logger.LogWarning("Rate limit exceeded for IP: {ClientIp}, Path: {Path}", clientIp, path);
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers["Retry-After"] = ((int)_timeWindow.TotalSeconds).ToString();
                await context.Response.WriteAsJsonAsync(new { error = "Rate limit exceeded. Try again later." });
                return;
            }
            else
            {
                cacheEntry.RequestCount++;
                _logger.LogInformation("Incrementing rate limit for {CacheKey} to {Count}", cacheKey, cacheEntry.RequestCount);
            }

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(cacheEntry), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _timeWindow
            });
            _logger.LogInformation("Set Redis cache key: {CacheKey}", cacheKey);

            _logger.LogInformation("Request allowed for IP: {ClientIp}, Path: {Path}, Count: {Count}/{Limit}",
                clientIp, path, cacheEntry.RequestCount, _requestLimit);

            await _next(context);
        }

        private class RateLimitEntry
        {
            public int RequestCount { get; set; }
            public DateTime WindowStart { get; set; }
        }
    }

    public static class RateLimitingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRateLimitingMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RateLimitingMiddleware>();
        }
    }
}