using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Vcr.HttpRecorder.Context;

namespace Vcr.HttpRecorder.AspNetCore;

/// <summary>
/// Provides extension methods for <see cref="WebApplicationFactory{TEntryPoint}"/> that allow
/// creating an <see cref="HttpClient"/> integrated with the HTTP recorder infrastructure.
/// </summary>
public static class WebApplicationFactoryRecorderExtensions
{
    /// <summary>
    /// Creates an <see cref="HttpClient"/> that uses the HTTP recorder propagation handler,
    /// enabling automatic recording and replay of HTTP interactions for the specified factory.
    /// </summary>
    /// <typeparam name="TEntryPoint">
    /// The type of the entry point for the web application (typically the Startup or Program class).
    /// </typeparam>
    /// <param name="factory">
    /// The <see cref="WebApplicationFactory{TEntryPoint}"/> instance that hosts the test server.
    /// </param>
    /// <param name="options">
    /// Optional <see cref="WebApplicationFactoryClientOptions"/> that control redirect handling,
    /// cookie handling, and base address. If <c>null</c>, a simple <see cref="HttpClient"/>
    /// with the recorder handler and no automatic redirects or cookies is returned.
    /// </param>
    /// <returns>
    /// An <see cref="HttpClient"/> that routes requests through the HTTP recorder propagation handler.
    /// </returns>
    public static HttpClient CreateRecorderClient<TEntryPoint>(
        this WebApplicationFactory<TEntryPoint> factory,
        WebApplicationFactoryClientOptions? options = null)
        where TEntryPoint : class
    {
        var handler = new HttpRecorderPropagationHandler { InnerHandler = factory.Server.CreateHandler() };

        if (options == null)
        {
            return new HttpClient(handler) { BaseAddress = factory.Server.BaseAddress };
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
