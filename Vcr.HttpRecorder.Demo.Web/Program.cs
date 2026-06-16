using Microsoft.AspNetCore.Mvc;
using Vcr.HttpRecorder.AspNetCore;
using Vcr.HttpRecorder.Context;

var builder = WebApplication.CreateBuilder(args);

// Register the concurrent context support
builder.Services.AddHttpRecorderConcurrentContextSupport();

var app = builder.Build();

// Add the middleware that restores the HTTP recorder context for each request
app.UseHttpRecorderContextRestorer();

var counters = new Dictionary<string, int>();
app.MapGet("/increment", ([FromQuery(Name = "name")] string name) =>
{
    counters.TryAdd(name, 0);
    counters[name] += 1;

    return counters[name];
});
app.MapGet("/reset", ([FromQuery(Name = "name")] string name) =>
{
    counters.TryAdd(name, 0);
    counters[name] = 0;

    return counters[name];
});
await app.RunAsync();