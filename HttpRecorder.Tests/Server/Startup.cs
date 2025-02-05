using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace HttpRecorder.Tests.Server
{
    [SuppressMessage("Minor Code Smell", "S2325:Methods and properties that don\'t access instance data should be static")]
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc(o =>
            {
                o.EnableEndpointRouting = false;
            }).AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseMvc();
        }
    }
}