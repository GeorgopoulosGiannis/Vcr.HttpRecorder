using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;

namespace Vcr.HttpRecorder.Context
{
    /// <summary>
    /// <see cref="IServiceCollection"/> extension methods.
    /// </summary>
    public static class HttpRecorderServiceCollectionExtensions
    {
        /// <summary>
        /// Enables support for the <see cref="HttpRecorderContext"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddHttpRecorderContextSupport(this IServiceCollection services)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, RecorderHttpMessageHandlerBuilderFilter>());

            return services;
        }


        /// <summary>
        /// Enables support for concurrent use of <see cref="HttpRecorderConcurrentContext"/> in different tests. 
        /// This method should be called once during service collection configuration (e.g., in `WebApplicationFactory.ConfigureServices`).
        /// The individual test cases will then use `using var context = new HttpRecorderConcurrentContext();`
        /// to activate the context for their asynchronous flow.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddHttpRecorderConcurrentContextSupport(this IServiceCollection services)
        {
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, RecorderConcurrentHttpMessageHandlerBuilderFilter>());

            // Middleware registration removed – it belongs to Vcr.HttpRecorder.AspNetCore.

            return services;
        }
    }
}
