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
