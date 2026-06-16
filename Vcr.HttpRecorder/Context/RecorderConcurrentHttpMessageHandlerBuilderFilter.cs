using Microsoft.Extensions.Http;
using System;
using System.IO;

namespace Vcr.HttpRecorder.Context
{
    /// <summary>
    /// <see cref="IHttpMessageHandlerBuilderFilter"/> that adds <see cref="HttpRecorderDelegatingHandler"/>
    /// based on the value of the <see cref="HttpRecorderConcurrentContext.Current"/> based on the <see cref="HttpRecorderConcurrentContext.Identifier"/>.
    /// </summary>
    public class RecorderConcurrentHttpMessageHandlerBuilderFilter : IHttpMessageHandlerBuilderFilter
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="RecorderHttpMessageHandlerBuilderFilter"/> class.
        /// </summary>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
        public RecorderConcurrentHttpMessageHandlerBuilderFilter(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <inheritdoc />
        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
        {
            return builder =>
            {
                // Run other configuration first, we want to decorate.
                next(builder);

                var context = HttpRecorderConcurrentContext.Current;

                if (context is null)
                {
                    return;
                }

                var config = context.ConfigurationFactory?.Invoke(_serviceProvider, builder) ??
                             new HttpRecorderConfiguration();

                if (config.Enabled)
                {
                    var interactionName = config.InteractionName;
                    if (string.IsNullOrEmpty(interactionName))
                    {
                        interactionName = Path.Combine(
                            Path.GetDirectoryName(context.FilePath) ?? string.Empty,
                            $"{Path.GetFileNameWithoutExtension(context.FilePath)}Fixtures",
                            context.TestName,
                            builder.Name ?? string.Empty);
                    }

                    builder.AdditionalHandlers.Add(new HttpRecorderDelegatingHandler(
                        interactionName,
                        mode: config.Mode,
                        matcher: config.Matcher,
                        repository: config.Repository,
                        anonymizer: config.Anonymizer));
                }
            };
        }
    }
}