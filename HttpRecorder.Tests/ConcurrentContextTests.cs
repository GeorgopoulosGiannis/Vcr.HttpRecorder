using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using HttpRecorder.Context;
using HttpRecorder.Tests.Server;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HttpRecorder.Tests;

[Collection(ServerCollection.Name)]
public class ConcurrentContextTests(ServerFixture fixture)
{
    [Fact]
    public void ItShouldThrowExceptionWhenContextIsRegisterUnderDifferentIdentifier()
    {
        var serviceCollection = CreateServiceCollection();

        using var context = new HttpRecorderConcurrentContext((_, _) => new HttpRecorderConfiguration
        {
            Mode = HttpRecorderMode.Record, InteractionName = nameof(ItShouldThrowExceptionWhenContextIsRegisterUnderDifferentIdentifier),
        });
        var act = () =>
        {
            serviceCollection.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient("TheClient");
        };
        act.Should().Throw<HttpRecorderException>().WithMessage("*Could not find*");
    }

    [Fact]
    public async Task ItShouldWorkWithMultipleContextsUnderDifferentTests()
    {
        var test1Task = Test1();
        var test2Task = Test2();
        await Task.WhenAll([test1Task, test2Task]);
        (await test1Task).Should().BeTrue();
        (await test2Task).Should().BeTrue();
    }

    private ServiceCollection CreateServiceCollection()
    {
        var services = new ServiceCollection();
        services
            .AddConcurrentHttpRecorderContextSupport()
            .AddHttpClient(
                "TheClient",
                options =>
                {
                    options.BaseAddress = fixture.ServerUri;
                });
        return services;
    }

    private async Task<bool> Test1()
    {
        var services = new ServiceCollection();
        services
            .AddConcurrentHttpRecorderContextSupport()
            .AddHttpClient(
                "TheClient",
                options =>
                {
                    options.BaseAddress = fixture.ServerUri;
                });

        using var context = new HttpRecorderConcurrentContext((_, _) => new HttpRecorderConfiguration { Mode = HttpRecorderMode.Record, });
        var client = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient("TheClient");
        var passthroughResponse = await client.GetAsync(ApiController.JsonUri);
        return passthroughResponse.IsSuccessStatusCode;
    }

    private async Task<bool> Test2()
    {
        var services = new ServiceCollection();
        services
            .AddConcurrentHttpRecorderContextSupport()
            .AddHttpClient(
                "TheClient",
                options =>
                {
                    options.BaseAddress = fixture.ServerUri;
                });

        using var context = new HttpRecorderConcurrentContext((_, _) => new HttpRecorderConfiguration { Mode = HttpRecorderMode.Record, });
        var client = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient("TheClient");
        var passthroughResponse = await client.GetAsync(ApiController.JsonUri);
        return passthroughResponse.IsSuccessStatusCode;
    }
}
