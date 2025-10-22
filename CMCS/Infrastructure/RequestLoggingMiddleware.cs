using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CMCS.Infrastructure
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var sw = Stopwatch.StartNew();
            var method = context.Request?.Method;
            var path = context.Request?.Path.Value ?? "";
            _logger.LogInformation("➡️ {Method} {Path} START", method, path);
            await _next(context);
            sw.Stop();
            _logger.LogInformation("⬅️ {Method} {Path} END {StatusCode} ({Elapsed} ms)", method, path, context.Response?.StatusCode, sw.ElapsedMilliseconds);
        }
    }
}
