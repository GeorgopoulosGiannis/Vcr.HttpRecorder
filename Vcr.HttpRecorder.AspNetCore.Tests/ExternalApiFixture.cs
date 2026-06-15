using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace Vcr.HttpRecorder.AspNetCore.Tests;

/// <summary>
/// Helper class that manages an external API test server and provides its handler and base address.
/// </summary>
public sealed class ExternalApiFixture : IDisposable
{
    public TestServer Server { get; }
    public HttpMessageHandler Handler { get; }
    public Uri BaseAddress { get; }

    public ExternalApiFixture()
    {
        var host = new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder
                    .UseTestServer()
                    .Configure(app =>
                    {
                        app.Run(async context =>
                        {
                            if (context.Request.Path == "/hello")
                            {
                                await context.Response.WriteAsync("external");
                            }
                            else
                            {
                                context.Response.StatusCode = 404;
                            }
                        });
                    });
            })
            .Build();

        host.Start();

        Server = host.GetTestServer();
        Handler = Server.CreateHandler();
        BaseAddress = Server.BaseAddress;
    }

    public void Dispose()
    {
        Server?.Dispose();
    }
}
