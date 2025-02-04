using System;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using HttpRecorder.Context;
using HttpRecorder.Tests.Server;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HttpRecorder.Tests
{
    [Collection(ServerCollection.Name)]
    public class ContextTests(ServerFixture fixture)
    {
        [Fact]
        public async Task ItShouldWorkWithHttpRecorderContext()
        {
            var services = new ServiceCollection();
            services
                .AddHttpRecorderContextSupport()
                .AddHttpClient(
                    "TheClient",
                    options =>
                    {
                        options.BaseAddress = fixture.ServerUri;
                    });

            HttpResponseMessage passthroughResponse;
            using (new HttpRecorderContext((_, _) => new HttpRecorderConfiguration
                   {
                       Mode = HttpRecorderMode.Record, InteractionName = nameof(ItShouldWorkWithHttpRecorderContext),
                   }))
            {
                var client = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient("TheClient");
                passthroughResponse = await client.GetAsync(ApiController.JsonUri);
                passthroughResponse.EnsureSuccessStatusCode();
            }

            using (new HttpRecorderContext((_, _) => new HttpRecorderConfiguration
                   {
                       Mode = HttpRecorderMode.Replay, InteractionName = nameof(ItShouldWorkWithHttpRecorderContext),
                   }))
            {
                var client = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient("TheClient");
                var response = await client.GetAsync(ApiController.JsonUri);
                response.EnsureSuccessStatusCode();
                response.Should().BeEquivalentTo(passthroughResponse);
            }
        }

        [Fact]
        public async Task ItShouldWorkWithHttpRecorderContextWhenNotRecording()
        {
            var services = new ServiceCollection();
            services
                .AddHttpRecorderContextSupport()
                .AddHttpClient(
                    "TheClient",
                    options =>
                    {
                        options.BaseAddress = fixture.ServerUri;
                    });

            HttpResponseMessage passthroughResponse;
            using (new HttpRecorderContext((_, _) => new HttpRecorderConfiguration
                   {
                       Enabled = false, Mode = HttpRecorderMode.Record, InteractionName = nameof(ItShouldWorkWithHttpRecorderContextWhenNotRecording),
                   }))
            {
                var client = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient("TheClient");
                passthroughResponse = await client.GetAsync(ApiController.JsonUri);
                passthroughResponse.EnsureSuccessStatusCode();
            }

            using (new HttpRecorderContext((_, _) => new HttpRecorderConfiguration
                   {
                       Mode = HttpRecorderMode.Replay, InteractionName = nameof(ItShouldWorkWithHttpRecorderContextWhenNotRecording),
                   }))
            {
                var client = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient("TheClient");
                Func<Task> act = async () => await client.GetAsync(ApiController.JsonUri);
                await act.Should().ThrowAsync<HttpRecorderException>();
            }
        }

        [Fact]
        public void ItShouldNotAllowMultipleContextsUnderTheSameTest()
        {
            using var context = new HttpRecorderContext();
            var act = () =>
            {
                var ctx2 = new HttpRecorderContext();
            };
            act.Should().Throw<HttpRecorderException>().WithMessage("*multiple*");
        }

        [Fact]
        public void ItShouldThrowExceptionWhenContextIsRegisterUnderDifferentIdentifier()
        {
            var serviceCollection = CreateServiceCollection();

            using var context = new HttpRecorderContext((_, _) => new HttpRecorderConfiguration
            {
                Mode = HttpRecorderMode.Record, InteractionName = nameof(ItShouldWorkWithHttpRecorderContext),
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
                .AddHttpRecorderContextSupport()
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
                .AddHttpRecorderContextSupport()
                .AddHttpClient(
                    "TheClient",
                    options =>
                    {
                        options.BaseAddress = fixture.ServerUri;
                    });

            using var context = new HttpRecorderContext((_, _) => new HttpRecorderConfiguration
            {
                Mode = HttpRecorderMode.Record, InteractionName = nameof(ItShouldWorkWithHttpRecorderContext),
            });
            var client = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient("TheClient");
            var passthroughResponse = await client.GetAsync(ApiController.JsonUri);
            return passthroughResponse.IsSuccessStatusCode;
        }

        private async Task<bool> Test2()
        {
            var services = new ServiceCollection();
            services
                .AddHttpRecorderContextSupport()
                .AddHttpClient(
                    "TheClient",
                    options =>
                    {
                        options.BaseAddress = fixture.ServerUri;
                    });

            using var context = new HttpRecorderContext((_, _) => new HttpRecorderConfiguration
            {
                Mode = HttpRecorderMode.Record, InteractionName = nameof(ItShouldWorkWithHttpRecorderContext),
            });
            var client = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient("TheClient");
            var passthroughResponse = await client.GetAsync(ApiController.JsonUri);
            return passthroughResponse.IsSuccessStatusCode;
        }
    }
}
