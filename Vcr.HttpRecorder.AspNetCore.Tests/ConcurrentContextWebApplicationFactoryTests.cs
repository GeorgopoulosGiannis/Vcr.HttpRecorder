using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Vcr.HttpRecorder.Context;
using Xunit;

namespace Vcr.HttpRecorder.AspNetCore.Tests;

public class ConcurrentContextWebApplicationFactoryTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ConcurrentContextWebApplicationFactoryTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateRecorderClient_ShouldWorkWithConcurrentContext()
    {
        // Arrange
        using var client = _factory.CreateRecorderClient();

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
        using var client = _factory.CreateRecorderClient(options);

        // Act
        var response = await client.GetAsync("/test");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Hello from test server");
    }
}
