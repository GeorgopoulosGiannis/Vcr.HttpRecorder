using Vcr.HttpRecorder.Context;

var builder = WebApplication.CreateBuilder(args);

// Register the concurrent context support
builder.Services.AddHttpRecorderConcurrentContextSupport();

var app = builder.Build();

app.MapGet("/test", () => "Hello from test server");

app.Run();
