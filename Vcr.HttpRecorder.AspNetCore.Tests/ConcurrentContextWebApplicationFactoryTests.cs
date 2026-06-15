using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vcr.HttpRecorder.Context;
using Vcr.HttpRecorder.Repositories;
using Xunit;

namespace Vcr.HttpRecorder.AspNetCore.Tests;

/// <summary>
/// Tests that validate the VCR recorder integration with WebApplicationFactory,
/// specifically when the server itself makes outbound HTTP calls (e.g. to an external API).
/// </summary>
public class ConcurrentContextWebApplicationFactoryTests
{
    /// <summary>
    /// In-memory interaction repository used to avoid file I/O in tests.
    /// </summary>
    private sealed class InMemoryInteractionRepository : IInteractionRepository
    {
        private Interaction? _stored;

        public Task<bool> ExistsAsync(string interactionName, CancellationToken cancellationToken = default)
            => Task.FromResult(_stored != null);

        public Task<Interaction> LoadAsync(string interactionName, CancellationToken cancellationToken = default)
            => Task.FromResult(_stored ?? throw new InvalidOperationException("No interaction stored"));

        public Task<Interaction> StoreAsync(Interaction interaction, CancellationToken cancellationToken = default)
        {
            _stored = interaction;
            return Task.FromResult(interaction);
        }
    }

    /// <summary>
    /// Helper class that manages an external API test server and provides its handler and base address.
    /// </summary>
    private sealed class ExternalApiFixture : IDisposable
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

    /// <summary>
    /// Builds a WebApplicationFactory for the test's main server, adding VCR support
    /// and configuring an external API handler.
    /// </summary>
    private WebApplicationFactory<Program> CreateRecorderEnabledFactory(
        HttpMessageHandler externalApiHandler,
        Uri externalBaseAddress)
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices((context, services) =>
                {
                    // Register VCR concurrent context support
                    services.AddHttpRecorderConcurrentContextSupport();

                    // Register HttpClient used to call the external API
                    services.AddHttpClient("external", client =>
                    {
                        client.BaseAddress = externalBaseAddress;
                    })
                    .ConfigurePrimaryHttpMessageHandler(() => externalApiHandler);
                });

                builder.Configure(app =>
                {
                    // Restore VCR context on each request
                    app.UseHttpRecorderContextRestorer();

                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        // Endpoint that calls the external API using the named HttpClient
                        endpoints.MapGet("/call-external", async context =>
                        {
                            var httpClientFactory = context.RequestServices
                                .GetRequiredService<IHttpClientFactory>();
                            var client = httpClientFactory.CreateClient("external");
                            var response = await client.GetAsync("/hello");
                            var content = await response.Content.ReadAsStringAsync();
                            await context.Response.WriteAsync(content);
                        });
                    });
                });
            });

        return factory;
    }

    /// <summary>
    /// Executes a test with the given mode and interaction name, using the provided external API fixture.
    /// </summary>
    private async Task<string> ExecuteTestWithMode(
        ExternalApiFixture externalApi,
        HttpRecorderMode mode,
        string interactionName,
        IInteractionRepository repository,
        HttpMessageHandler? handlerOverride = null,
        Uri? baseAddressOverride = null)
    {
        var handler = handlerOverride ?? externalApi.Handler;
        var baseAddress = baseAddressOverride ?? externalApi.BaseAddress;

        using var context = new HttpRecorderConcurrentContext((_, _) =>
            new HttpRecorderConfiguration
            {
                Mode = mode,
                InteractionName = interactionName,
                Repository = repository
            });

        using var factory = CreateRecorderEnabledFactory(handler, baseAddress);
        using var client = factory.CreateRecorderClient();

        var response = await client.GetAsync("/call-external");
        var content = await response.Content.ReadAsStringAsync();
        return content;
    }

    [Fact]
    public async Task PassthroughMode_ShouldCallLiveExternalApi()
    {
        // Arrange
        using var externalApi = new ExternalApiFixture();
        var interactionName = Guid.NewGuid().ToString();
        var repository = new InMemoryInteractionRepository();

        // Act
        var content = await ExecuteTestWithMode(
            externalApi,
            HttpRecorderMode.Passthrough,
            interactionName,
            repository);

        // Assert – the real external API was called and returned "external"
        content.Should().Be("external");
    }

    [Fact]
    public async Task RecordThenReplay_ShouldReturnSameResponseFromExternalApi()
    {
        // Arrange – external API server
        using var externalApi = new ExternalApiFixture();
        var interactionName = Guid.NewGuid().ToString();
        var repository = new InMemoryInteractionRepository();

        // Record mode
        var recordedContent = await ExecuteTestWithMode(
            externalApi,
            HttpRecorderMode.Record,
            interactionName,
            repository);

        recordedContent.Should().Be("external");

        // Now shut down the real external API to prove replay doesn't call it
        externalApi.Dispose();

        // Act – replay mode
        // Use the same base address as during recording, but a handler that would throw
        // if the VCR did not intercept the request (safety net).
        var replayContent = await ExecuteTestWithMode(
            externalApi, // disposed, but we only use its base address and handler override
            HttpRecorderMode.Replay,
            interactionName,
            repository,
            handlerOverride: new HttpClientHandler(),
            baseAddressOverride: externalApi.BaseAddress);

        // Assert – replayed response matches recorded one, even though external API is gone
        replayContent.Should().Be("external");
    }

    [Fact]
    public async Task AutoMode_WithExistingRecording_ShouldReturnRecordedResponse()
    {
        // Arrange
        using var externalApi = new ExternalApiFixture();
        var interactionName = Guid.NewGuid().ToString();
        var repository = new InMemoryInteractionRepository();

        // First, record the interaction
        var recordedContent = await ExecuteTestWithMode(
            externalApi,
            HttpRecorderMode.Record,
            interactionName,
            repository);

        recordedContent.Should().Be("external");

        // Kill the external server to prove replay doesn't hit it
        externalApi.Dispose();

        // Act – Auto mode (recording exists, so replay)
        var autoContent = await ExecuteTestWithMode(
            externalApi, // disposed, but we only use its base address and handler override
            HttpRecorderMode.Auto,
            interactionName,
            repository,
            handlerOverride: new HttpClientHandler(),
            baseAddressOverride: externalApi.BaseAddress);

        // Assert – response comes from the recording, not the live API
        autoContent.Should().Be("external");
    }
}
