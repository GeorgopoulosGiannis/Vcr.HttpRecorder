﻿using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Vcr.HttpRecorder.Tests.Server;
using Xunit;

namespace Vcr.HttpRecorder.Tests
{
    [Collection(ServerCollection.Name)]
    public class HttpClientFactoryTests
    {
        private readonly ServerFixture _fixture;

        public HttpClientFactoryTests(ServerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ItShouldWorkWithHttpClientFactory()
        {
            var services = new ServiceCollection();
            services
                .AddHttpClient(
                    "TheClient",
                    options =>
                    {
                        options.BaseAddress = _fixture.ServerUri;
                    })
                .AddHttpRecorder(nameof(ItShouldWorkWithHttpClientFactory), HttpRecorderMode.Record);

            var client = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient("TheClient");
            var response = await client.GetAsync(ApiController.JsonUri);
            response.IsSuccessStatusCode.Should().BeTrue();
        }

        [Fact]
        public async Task ItShouldWorkWithComplexInteractionsInvolvingDisposedContent()
        {
            var services = new ServiceCollection();
            services
                .AddHttpClient(
                    "TheClient",
                    options =>
                    {
                        options.BaseAddress = _fixture.ServerUri;
                    })
                .AddHttpRecorder(nameof(ItShouldWorkWithHttpClientFactory), HttpRecorderMode.Record);

            var client = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient("TheClient");

            var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("name", "TheName"),
                });

            var response = await client.PostAsync(ApiController.FormDataUri, formContent);
            response.IsSuccessStatusCode.Should().BeTrue();
            response.Dispose();

            response = await client.PostAsync(ApiController.FormDataUri, formContent);
            response.IsSuccessStatusCode.Should().BeTrue();
        }
    }
}
