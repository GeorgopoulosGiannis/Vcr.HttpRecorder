# HttpRecorder

.NET HttpClient integration tests made easy.

HttpRecorder is an `HttpMessageHandler` that can record and replay HTTP interactions through the standard `HttpClient` . This allows the creation of HTTP integration tests that are fast, repeatable and reliable.

Interactions are recorded using the [HTTP Archive format standard](https://en.wikipedia.org/wiki/.har), so that they are easily manipulated by your favorite tool of choice.

[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)


> 📝 **This is a maintained fork of the original [nventive/HttpRecorder](https://github.com/nventive/HttpRecorder)**.  
> It includes bug fixes, support for modern .NET versions, and new features like concurrent context support.

## Recommended Setup (ASP.NET Core Integration Testing)

If you're using WebApplicationFactory, this is the simplest and most powerful way to enable automatic recording and replaying across all HttpClients:

```csharp
[Fact]
public async Task MyApiTest()
{
    using var context = new HttpRecorderConcurrentContext((_, _) => new HttpRecorderConfiguration
    {
        Mode = HttpRecorderMode.Auto, // Automatically records or replays
    });

    var client = webAppFactory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpRecorderConcurrentContextSupport(); // Injects the handler globally
        });
    }).CreateClient();

    var response = await client.GetAsync("/api/my-endpoint");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

✅ No need to manually configure HttpClient

✅ All HttpClients use the recorder automatically

✅ .har files are stored per test method

✅ Supports parallel test execution

## Install via NuGet

```
Install-Package HttpRecorder
```

## Using the Delegating Handler Manually

If you want to apply HttpRecorder to a specific HttpClient only — without affecting the global DI container — you can use the delegating handler directly:

```csharp
var interactionPath = "fixtures/test.har";

var client = new HttpClient(new HttpRecorderDelegatingHandler(interactionPath)
{
    InnerHandler = new HttpClientHandler()
})
{
    BaseAddress = new Uri("https://reqres.in/")
};
```

This is useful if you need more granular control over which clients are recorded.

📝 Tip: You can use CallerMemberName + CallerFilePath to automatically name har files per test.

## Features

#### Modes

 - `Auto` (default): Replay if cassette exists, otherwise record

 - `Record`: Always record

 - `Replay`: Always replay (throws if file is missing)

 - `Passthrough`: Bypass recorder, make real requests

You can override the mode with the HTTP_RECORDER_MODE environment variable — useful in CI.


## Matching Rules
Use built-in matchers or create your own:

```csharp
matcher = RulesMatcher.MatchMultiple
    .ByHttpMethod()
    .ByRequestUri(UriPartial.Path)
    .ByHeader("X-API-Key");
```

## Anonymization

Mask sensitive fields before saving:

```csharp
var anonymizer = RulesInteractionAnonymizer.Default
    .AnonymizeRequestHeader("Authorization");

```

## External HAR Support
You can use .har files recorded with tools like:

 - Fiddler

 - Chrome DevTools

 - Postman

Just pass the file path into the handler.

Here is an example of an integration tests using **HttpRecorder** (the `HttpRecorderDelegatingHandler`):


## Custom Storage
You can override how and where interactions are stored via `IInteractionRepository`.


## Compared to WireMock


WireMock is a powerful tool, but sometimes it's more than you need — especially for simple, deterministic testing of external API calls.

| Feature                         | WireMock              | Vcr.HttpRecorder            |
|---------------------------------|------------------------|-----------------------------|
| Requires mock server            | ✅                     | ❌                          |
| Requires hand-written stubs     | ✅                     | ❌ (records real responses) |
| Easily test external API logic  | ⚠️ manual setup needed | ✅ out of the box           |
| Works with WebApplicationFactory | ⚠️ extra wiring        | ✅ built-in support         |
