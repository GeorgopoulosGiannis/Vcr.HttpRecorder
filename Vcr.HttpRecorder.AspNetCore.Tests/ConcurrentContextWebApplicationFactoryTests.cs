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

        using var context = new HttpRecorderConcurrentContext((_, _) =>
            new HttpRecorderConfiguration
            {
                Mode = HttpRecorderMode.Passthrough,
                InteractionName = interactionName
            });

        using var client = factory.CreateRecorderClient();

        // Act
        var response = await client.GetAsync("/test");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();
        content.Should().Be("Hello from test server");
    }

    [Fact]
    public async Task RecordThenReplay_ShouldReturnSameResponse()
    {
        // Arrange – first request in Record mode
        var interactionName = Guid.NewGuid().ToString();

        using (var recordContext = new HttpRecorderConcurrentContext((_, _) =>
            new HttpRecorderConfiguration
            {
                Mode = HttpRecorderMode.Record,
                InteractionName = interactionName
            }))
        {
            using var recordClient = factory.CreateRecorderClient();
            var recordResponse = await recordClient.GetAsync("/test");
            recordResponse.EnsureSuccessStatusCode();
            var recordContent = await recordResponse.Content.ReadAsStringAsync();
            recordContent.Should().Be("Hello from test server");
        }

        // Act – second request in Replay mode
        using (var replayContext = new HttpRecorderConcurrentContext((_, _) =>
            new HttpRecorderConfiguration
            {
                Mode = HttpRecorderMode.Replay,
                InteractionName = interactionName
            }))
        {
            using var replayClient = factory.CreateRecorderClient();
            var replayResponse = await replayClient.GetAsync("/test");
            replayResponse.EnsureSuccessStatusCode();
            var replayContent = await replayResponse.Content.ReadAsStringAsync();

            // Assert – the replayed response matches the recorded one
            replayContent.Should().Be("Hello from test server");
        }
    }

    [Fact]
    public async Task AutoMode_WithExistingRecording_ShouldReturnRecordedResponse()
    {
        // Arrange – first request in Record mode to create a recording
        var interactionName = Guid.NewGuid().ToString();

        using (var recordContext = new HttpRecorderConcurrentContext((_, _) =>
            new HttpRecorderConfiguration
            {
                Mode = HttpRecorderMode.Record,
                InteractionName = interactionName
            }))
        {
            using var recordClient = factory.CreateRecorderClient();
            var recordResponse = await recordClient.GetAsync("/test");
            recordResponse.EnsureSuccessStatusCode();
        }

        // Act – second request in Auto mode (should replay because recording exists)
        using (var autoContext = new HttpRecorderConcurrentContext((_, _) =>
            new HttpRecorderConfiguration
            {
                Mode = HttpRecorderMode.Auto,
                InteractionName = interactionName
            }))
        {
            using var autoClient = factory.CreateRecorderClient();
            var autoResponse = await autoClient.GetAsync("/test");
            autoResponse.EnsureSuccessStatusCode();
            var autoContent = await autoResponse.Content.ReadAsStringAsync();

            // Assert – the auto‑replayed response matches the recorded one
            autoContent.Should().Be("Hello from test server");
        }
    }
}
