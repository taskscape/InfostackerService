using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using System.Net;

namespace ShareAPI.Controllers
{
    public class RateLimitAttribute : ActionFilterAttribute
    {
        private readonly int _limit;
        private readonly TimeSpan _resetInterval;

        public RateLimitAttribute(int limit, int resetIntervalInSeconds)
        {
            _limit = limit;
            _resetInterval = TimeSpan.FromSeconds(resetIntervalInSeconds);
        }

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

            if (requests >= _limit)
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
