using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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
    /// Builds a WebApplicationFactory for the test's main server, adding VCR support
    /// and configuring an external base URL.
    /// </summary>
    private WebApplicationFactory<Program> CreateRecorderEnabledFactory(string externalBaseUrl)
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    // Pass the external API base URL to the main application
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ExternalApiBaseUrl"] = externalBaseUrl
                    });
                });

                builder.ConfigureServices((context, services) =>
                {
                    // Register VCR concurrent context support, passing the configuration
                    // so the recorder can read its settings (e.g. to determine the proxy URL).
                    services.AddHttpRecorderConcurrentContextSupport();

                    // Register HttpClient used to call the external API
                    services.AddHttpClient("external", client =>
                    {
                        client.BaseAddress = new Uri(externalBaseUrl);
                    });
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
    /// Creates a simple external API server that always returns "external".
    /// Uses HostBuilder with TestServer to avoid deprecated WebHostBuilder.
    /// </summary>
    private TestServer CreateExternalApiServer()
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

        return host.GetTestServer();
    }

    [Fact]
    public async Task PassthroughMode_ShouldCallLiveExternalApi()
    {
        // Arrange
        using var externalServer = CreateExternalApiServer();
        var externalUrl = externalServer.BaseAddress.ToString().TrimEnd('/');

        using var context = new HttpRecorderConcurrentContext((_, _) =>
            new HttpRecorderConfiguration
            {
                Mode = HttpRecorderMode.Passthrough,
                InteractionName = Guid.NewGuid().ToString(),
                Repository = new InMemoryInteractionRepository()
            });

        using var factory = CreateRecorderEnabledFactory(externalUrl);
        using var client = factory.CreateRecorderClient();

        // Act
        var response = await client.GetAsync("/call-external");
        var content = await response.Content.ReadAsStringAsync();

        // Assert – the real external API was called and returned "external"
        content.Should().Be("external");
    }

    [Fact]
    public async Task RecordThenReplay_ShouldReturnSameResponseFromExternalApi()
    {
        // Arrange – external API server
        using var externalServer = CreateExternalApiServer();
        var externalUrl = externalServer.BaseAddress.ToString().TrimEnd('/');

        var interactionName = Guid.NewGuid().ToString();
        var repository = new InMemoryInteractionRepository();

        // Record mode
        using (new HttpRecorderConcurrentContext((_, _) =>
                   new HttpRecorderConfiguration
                   {
                       Mode = HttpRecorderMode.Record,
                       InteractionName = interactionName,
                       Repository = repository
                   }))
        {
            using var recordFactory = CreateRecorderEnabledFactory(externalUrl);
            using var recordClient = recordFactory.CreateRecorderClient();

            var recordResponse = await recordClient.GetAsync("/call-external");
            var recordContent = await recordResponse.Content.ReadAsStringAsync();
            recordContent.Should().Be("external");
        }

        // Now shut down the real external API to prove replay doesn't call it
        externalServer.Dispose();

        // Act – replay mode
        using (new HttpRecorderConcurrentContext((_, _) =>
                   new HttpRecorderConfiguration
                   {
                       Mode = HttpRecorderMode.Replay,
                       InteractionName = interactionName,
                       Repository = repository
                   }))
        {
            using var replayFactory = CreateRecorderEnabledFactory("http://localhost:9999"); // dead URL
            using var replayClient = replayFactory.CreateRecorderClient();

            var replayResponse = await replayClient.GetAsync("/call-external");
            var replayContent = await replayResponse.Content.ReadAsStringAsync();

            // Assert – replayed response matches recorded one, even though external API is gone
            replayContent.Should().Be("external");
        }
    }

    [Fact]
    public async Task AutoMode_WithExistingRecording_ShouldReturnRecordedResponse()
    {
        // Arrange
        using var externalServer = CreateExternalApiServer();
        var externalUrl = externalServer.BaseAddress.ToString().TrimEnd('/');

        var interactionName = Guid.NewGuid().ToString();
        var repository = new InMemoryInteractionRepository();

        // First, record the interaction
        using (new HttpRecorderConcurrentContext((_, _) =>
                   new HttpRecorderConfiguration
                   {
                       Mode = HttpRecorderMode.Record,
                       InteractionName = interactionName,
                       Repository = repository
                   }))
        {
            using var recordFactory = CreateRecorderEnabledFactory(externalUrl);
            using var recordClient = recordFactory.CreateRecorderClient();

            var recordResponse = await recordClient.GetAsync("/call-external");
            var recordContent = await recordResponse.Content.ReadAsStringAsync();
            recordContent.Should().Be("external");
        }

        // Kill the external server to prove replay doesn't hit it
        externalServer.Dispose();

        // Act – Auto mode (recording exists, so replay)
        using (new HttpRecorderConcurrentContext((_, _) =>
                   new HttpRecorderConfiguration
                   {
                       Mode = HttpRecorderMode.Auto,
                       InteractionName = interactionName,
                       Repository = repository
                   }))
        {
            using var autoFactory = CreateRecorderEnabledFactory("http://localhost:9999");
            using var autoClient = autoFactory.CreateRecorderClient();

            var autoResponse = await autoClient.GetAsync("/call-external");
            var autoContent = await autoResponse.Content.ReadAsStringAsync();

            // Assert – response comes from the recording, not the live API
            autoContent.Should().Be("external");
        }
    }
}
