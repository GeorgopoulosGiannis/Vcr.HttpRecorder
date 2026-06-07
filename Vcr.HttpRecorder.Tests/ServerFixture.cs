using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Text;
using Vcr.HttpRecorder.Context; // Added for AddHttpRecorderConcurrentContextSupport
using Vcr.HttpRecorder.Tests.Server;

namespace Vcr.HttpRecorder.Tests
{
    /// <summary>
    /// xUnit collection fixture that starts an ASP.NET Core server listening to a random port.
    /// <seealso cref="ServerCollection" />.
    /// </summary>
    public sealed class ServerFixture : IDisposable
    {
        public ServerFixture()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Use Host.CreateDefaultBuilder() with ConfigureWebHostDefaults() - the recommended way in .NET 10
            ServerHost = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel()
                              .UseStartup<Startup>()
                              .ConfigureServices(services => services.AddHttpRecorderConcurrentContextSupport())
                              .UseUrls("http://127.0.0.1:0");
                })
                .Build();

            ServerHost.Start();
        }

        public IHost ServerHost { get; }

        public Uri ServerUri
        {
            get
            {
                var server = ServerHost.Services.GetRequiredService<IServer>();
                var serverAddressesFeature = server.Features.Get<IServerAddressesFeature>();
                return new Uri(serverAddressesFeature.Addresses.First());
            }
        }

        public void Dispose()
        {
            ServerHost?.Dispose();
        }
    }
}
