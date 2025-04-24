using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace Infostacker.Controllers
{
    public class RateLimitAttribute(int limit, int resetIntervalInSeconds) : ActionFilterAttribute
    {
        private readonly TimeSpan _resetInterval = TimeSpan.FromSeconds(resetIntervalInSeconds);

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            IMemoryCache? cache = context.HttpContext.RequestServices.GetService<IMemoryCache>();
            if (cache == null)
            {
                context.Result = new ContentResult
                {
                    Content = "MemoryCache service is not available.",
                    StatusCode = (int)HttpStatusCode.InternalServerError
                };
                return;
            }

            string? ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString();

            if (ipAddress == null)
            {
                context.Result = new ContentResult
                {
                    Content = "IP Address cannot be determined.",
                    StatusCode = (int)HttpStatusCode.BadRequest
                };
                return;
            }

            string? cacheKey = $"{ipAddress}:{DateTime.UtcNow.Date}";
            int requests = cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _resetInterval;
                return 0;
            });

            if (requests >= limit)
            {
                context.Result = new ContentResult
                {
                    Content = "Rate limit exceeded. Try again later.",
                    StatusCode = (int)HttpStatusCode.TooManyRequests
                };
                return;
            }

            cache.Set(cacheKey, requests + 1, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _resetInterval
            });

            base.OnActionExecuting(context);
        }
    }
}
