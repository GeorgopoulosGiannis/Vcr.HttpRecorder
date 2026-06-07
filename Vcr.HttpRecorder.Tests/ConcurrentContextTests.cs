using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Vcr.HttpRecorder.Context;
using Vcr.HttpRecorder.Tests.Server;
using Xunit;

namespace Vcr.HttpRecorder.Tests;

[Collection(ServerCollection.Name)]
public class ConcurrentContextTests(ServerFixture fixture)
{
    [Fact]
    public async Task ItShouldWorkWithMultipleContextsUnderDifferentTests()
    {
        var execution1 = ExecuteModeIterations("execution1", async (client, mode) =>
        {
            var response = await client.GetFromJsonAsync<SampleModel>($"{ApiController.JsonUri}?name=11");

            response.Name.Should().Be("11");
        });
        var execution2 = ExecuteModeIterations("execution2", async (client, mode) =>
        {
            var response = await client.GetFromJsonAsync<SampleModel>($"{ApiController.JsonUri}?name=12");
            response.Name.Should().Be("12");
        });
        await Task.WhenAll(execution1, execution2);
    }

    [Fact]
    public void ItShouldClearContextOnDispose()
    {
        // Act & Assert
        using (new HttpRecorderConcurrentContext())
        {
            HttpRecorderConcurrentContext.Current.Should().NotBeNull();
        }

        // After dispose, Current should be null
        var current = HttpRecorderConcurrentContext.Current;
        current.Should().BeNull();
    }

    private async Task ExecuteModeIterations(string testName, Func<HttpClient, HttpRecorderMode, Task> test)
    {
        var modes = new[]
        {
            HttpRecorderMode.Passthrough, HttpRecorderMode.Record, HttpRecorderMode.Replay, HttpRecorderMode.Auto,
        };

        foreach (var mode in modes)
        {
            var services = new ServiceCollection();
            services
                .AddHttpRecorderConcurrentContextSupport()
                .AddHttpClient(
                    "TestClient",
                    options =>
                    {
                        options.BaseAddress = fixture.ServerUri;
                    });

            using var context = new HttpRecorderConcurrentContext((_, _) =>
                new HttpRecorderConfiguration
                {
                    Mode = mode,
                    InteractionName = testName,
                });

            var client = services
                .BuildServiceProvider()
                .GetRequiredService<IHttpClientFactory>()
                .CreateClient("TestClient");

            await test(client, mode);
        }
    }
}