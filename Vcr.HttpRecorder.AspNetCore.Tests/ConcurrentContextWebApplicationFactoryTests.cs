using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Vcr.HttpRecorder.AspNetCore.Tests;

public class ConcurrentContextWebApplicationFactoryTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task CreateRecorderClient_ShouldWorkWithConcurrentContext()
    {
        // Arrange
        using var client = factory.CreateRecorderClient();

        // Act
        var response = await client.GetAsync("/test");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Hello from test server");
    }

    [Fact]
    public async Task CreateRecorderClient_WithOptions_ShouldWork()
    {
        // Arrange
        var options = new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri("http://localhost")
        };
        using var client = factory.CreateRecorderClient(options);

        // Act
        var response = await client.GetAsync("/test");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Hello from test server");
    }
}
