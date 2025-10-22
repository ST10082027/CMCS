using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace CMCS.Infrastructure
{
    public class ActionLoggingFilter : IActionFilter
    {
        private readonly ILogger<ActionLoggingFilter> _logger;
        public ActionLoggingFilter(ILogger<ActionLoggingFilter> logger) => _logger = logger;

        public void OnActionExecuting(ActionExecutingContext context)
        {
            var route = $"{context.RouteData.Values["controller"]}/{context.RouteData.Values["action"]}";
            var args = string.Join(", ", context.ActionArguments.Select(kv => $"{kv.Key}={(kv.Value ?? "(null)")}"));
            _logger.LogInformation("➡️ OnActionExecuting: {Route} | Args: {Args}", route, args);
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            var route = $"{context.RouteData.Values["controller"]}/{context.RouteData.Values["action"]}";
            var ex = context.Exception;
            if (ex != null)
                _logger.LogError(ex, "💥 OnActionExecuted (EXCEPTION): {Route}", route);
            else
                _logger.LogInformation("✅ OnActionExecuted: {Route}", route);
        }
    }
}
