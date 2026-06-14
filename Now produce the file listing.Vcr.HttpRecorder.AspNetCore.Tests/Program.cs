using Vcr.HttpRecorder.AspNetCore;
using Vcr.HttpRecorder.Context;

var builder = WebApplication.CreateBuilder(args);

// Register the concurrent context support
builder.Services.AddHttpRecorderConcurrentContextSupport();

var app = builder.Build();

// Add the middleware that restores the HTTP recorder context for each request
app.UseHttpRecorderContextRestorer();

app.MapGet("/test", () => "Hello from test server");

await app.RunAsync();
