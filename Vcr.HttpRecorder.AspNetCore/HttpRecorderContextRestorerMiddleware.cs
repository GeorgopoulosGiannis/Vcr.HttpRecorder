using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Vcr.HttpRecorder.Context;

namespace Vcr.HttpRecorder.AspNetCore
{
    /// <summary>
    /// Middleware that restores the <see cref="HttpRecorderConcurrentContext"/> on the server side
    /// by reading the propagation header set by <see cref="HttpRecorderPropagationHandler"/>.
    /// </summary>
    public class HttpRecorderContextRestorerMiddleware
    {
        private const string RecorderContextIdHeader = "X-Vcr-HttpRecorder-ContextId";

        private readonly RequestDelegate _next;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpRecorderContextRestorerMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        public HttpRecorderContextRestorerMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// Invokes the middleware.
        /// </summary>
        /// <param name="httpContext">The current HTTP context.</param>
        /// <returns>A task that represents the completion of request processing.</returns>
        public async Task InvokeAsync(HttpContext httpContext)
        {
            if (httpContext.Request.Headers.TryGetValue(RecorderContextIdHeader, out var contextId))
            {
                var originalContext = HttpRecorderConcurrentContext.GetContextById(contextId);

                if (originalContext != null)
                {
                    // Create a new context that shares the same configuration
                    using var restoredContext = new HttpRecorderConcurrentContext(
                        originalContext.ConfigurationFactory,
                        originalContext.TestName,
                        originalContext.FilePath,
                        originalContext.LineNumber);

                    await _next(httpContext);
                    return;
                }
            }

            await _next(httpContext);
        }
    }
}
