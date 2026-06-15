using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Vcr.HttpRecorder.Context;
using Xunit;

namespace Vcr.HttpRecorder.AspNetCore.Tests;

public class ConcurrentContextWebApplicationFactoryTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task PassthroughMode_ShouldCallLiveApi()
    {
        // Arrange
        var interactionName = Guid.NewGuid().ToString();
        var name = nameof(PassthroughMode_ShouldCallLiveApi);
        using var context = new HttpRecorderConcurrentContext((_, _) =>
            new HttpRecorderConfiguration
            {
                Mode = HttpRecorderMode.Passthrough,
                InteractionName = interactionName
            });

        using var client = factory.CreateRecorderClient();

        // Act
        var response = await client.GetAsync($"/increment?name={name}");
        var content = await response.Content.ReadAsStringAsync();

        var response2 = await client.GetAsync($"/increment?name={name}");
        var content2 = await response2.Content.ReadAsStringAsync();

        // Assert
        content.Should().Be("1");
        content2.Should().Be("2");
    }

    [Fact]
    public async Task RecordThenReplay_ShouldReturnSameResponse()
    {
        var interactionName = Guid.NewGuid().ToString();
        var name = nameof(RecordThenReplay_ShouldReturnSameResponse);
        using (new HttpRecorderConcurrentContext((_, _) =>
                   new HttpRecorderConfiguration
                   {
                       Mode = HttpRecorderMode.Record,
                       InteractionName = interactionName
                   }))
        {
            using var recordClient = factory.CreateRecorderClient();
            var recordResponse = await recordClient.GetAsync($"/increment?name={name}");
            var recordContent = await recordResponse.Content.ReadAsStringAsync();
            recordContent.Should().Be("1");
        }

        // Act – second request in Replay mode
        using (new HttpRecorderConcurrentContext((_, _) =>
                   new HttpRecorderConfiguration
                   {
                       Mode = HttpRecorderMode.Replay,
                       InteractionName = interactionName
                   }))
        {
            using var replayClient = factory.CreateRecorderClient();
            var replayResponse = await replayClient.GetAsync($"/increment?name={name}");
            var replayContent = await replayResponse.Content.ReadAsStringAsync();

            // Assert – the replayed response matches the recorded one
            replayContent.Should().Be("1");
        }
    }

    [Fact]
    public async Task AutoMode_WithExistingRecording_ShouldReturnRecordedResponse()
    {
        var interactionName = Guid.NewGuid().ToString();

        using (new HttpRecorderConcurrentContext((_, _) =>
                   new HttpRecorderConfiguration
                   {
                       Mode = HttpRecorderMode.Record,
                       InteractionName = interactionName
                   }))
        {
            using var recordClient = factory.CreateRecorderClient();
            var recordResponse = await recordClient.GetAsync("/increment");
            var recordContent = await recordResponse.Content.ReadAsStringAsync();
            recordContent.Should().Be("1");
        }

        using (new HttpRecorderConcurrentContext((_, _) =>
                   new HttpRecorderConfiguration
                   {
                       Mode = HttpRecorderMode.Auto,
                       InteractionName = interactionName
                   }))
        {
            using var autoClient = factory.CreateRecorderClient();
            var autoResponse = await autoClient.GetAsync("/increment");
            var autoContent = await autoResponse.Content.ReadAsStringAsync();

            autoContent.Should().Be("1");
        }
    }
}