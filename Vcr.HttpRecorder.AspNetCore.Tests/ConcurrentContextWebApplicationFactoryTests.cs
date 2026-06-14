using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Vcr.HttpRecorder;
using Vcr.HttpRecorder.AspNetCore;
using Vcr.HttpRecorder.Context;
using Xunit;

namespace Vcr.HttpRecorder.AspNetCore.Tests;

public class ConcurrentContextWebApplicationFactoryTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task CreateRecorderClient_ShouldWorkWithConcurrentContext()
    {
        await ExecuteModeIterations(
            nameof(CreateRecorderClient_ShouldWorkWithConcurrentContext),
            async (client, mode) =>
            {
                var response = await client.GetAsync("/test");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                content.Should().Be("Hello from test server");
            });
    }

    [Fact]
    public async Task CreateRecorderClient_WithOptions_ShouldWork()
    {
        var options = new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri("http://localhost")
        };

        await ExecuteModeIterations(
            nameof(CreateRecorderClient_WithOptions_ShouldWork),
            async (client, mode) =>
            {
                var response = await client.GetAsync("/test");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                content.Should().Be("Hello from test server");
            },
            options);
    }

    private async Task ExecuteModeIterations(
        string testName,
        Func<HttpClient, HttpRecorderMode, Task> test,
        WebApplicationFactoryClientOptions? options = null)
    {
        var modes = new[]
        {
            HttpRecorderMode.Passthrough,
            HttpRecorderMode.Record,
            HttpRecorderMode.Replay,
            HttpRecorderMode.Auto,
        };

        foreach (var mode in modes)
        {
            using var context = new HttpRecorderConcurrentContext((_, _) =>
                new HttpRecorderConfiguration
                {
                    Mode = mode,
                    InteractionName = testName,
                });

            using var client = factory.CreateRecorderClient(options);

            await test(client, mode);
        }
    }
}
