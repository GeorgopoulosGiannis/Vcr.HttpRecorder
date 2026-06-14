using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Vcr.HttpRecorder.Context;

namespace Vcr.HttpRecorder.AspNetCore
{
    /// <summary>
    /// Extension methods for <see cref="WebApplicationFactory{TEntryPoint}"/> to create an <see cref="HttpClient"/>
    /// that propagates the <see cref="HttpRecorderConcurrentContext"/> via headers.
    /// </summary>
    public static class WebApplicationFactoryRecorderExtensions
    {
        /// <summary>
        /// Creates an <see cref="HttpClient"/> that automatically propagates the current
        /// <see cref="HttpRecorderConcurrentContext"/> to the server via a custom header.
        /// </summary>
        /// <typeparam name="TEntryPoint">The entry point type of the web application.</typeparam>
        /// <param name="factory">The <see cref="WebApplicationFactory{TEntryPoint}"/>.</param>
        /// <param name="options">Optional client options.</param>
        /// <returns>An <see cref="HttpClient"/> with the propagation handler.</returns>
        public static HttpClient CreateRecorderClient<TEntryPoint>(
            this WebApplicationFactory<TEntryPoint> factory,
            WebApplicationFactoryClientOptions? options = null)
            where TEntryPoint : class
        {
            var handler = new HttpRecorderPropagationHandler
            {
                InnerHandler = factory.Server.CreateHandler()
            };

            if (options == null)
            {
                return new HttpClient(handler)
                {
                    BaseAddress = factory.Server.BaseAddress
                };
            }

            var handlers = new List<DelegatingHandler> { handler };

            if (options.AllowAutoRedirect)
            {
                handlers.Add(new RedirectHandler(options.MaxAutomaticRedirections));
            }

            if (options.HandleCookies)
            {
                handlers.Add(new CookieContainerHandler());
            }

            return factory.CreateDefaultClient(options.BaseAddress, handlers.ToArray());
        }
    }
}
