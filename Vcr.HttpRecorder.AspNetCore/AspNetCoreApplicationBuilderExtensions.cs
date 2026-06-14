using Microsoft.AspNetCore.Builder;
using Vcr.HttpRecorder.AspNetCore;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// Extension methods for <see cref="IApplicationBuilder"/> to add Vcr.HttpRecorder middleware.
    /// </summary>
    public static class AspNetCoreApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds the <see cref="HttpRecorderContextRestorerMiddleware"/> to the ASP.NET Core pipeline.
        /// This middleware restores the <see cref="HttpRecorderConcurrentContext"/> from the propagation header
        /// set by <see cref="HttpRecorderPropagationHandler"/>.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/>.</param>
        /// <returns>The <see cref="IApplicationBuilder"/> for chaining.</returns>
        public static IApplicationBuilder UseHttpRecorderContextRestorer(this IApplicationBuilder app)
        {
            return app.UseMiddleware<HttpRecorderContextRestorerMiddleware>();
        }
    }
}
