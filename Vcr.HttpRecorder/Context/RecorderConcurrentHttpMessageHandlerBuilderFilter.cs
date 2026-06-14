using Microsoft.Extensions.Http;
using System;

namespace Vcr.HttpRecorder.Context
{
    /// <summary>
    /// <see cref="IHttpMessageHandlerBuilderFilter"/> that adds <see cref="HttpRecorderPropagationHandler"/>
    /// to inject the current <see cref="HttpRecorderConcurrentContext"/> as a header on outgoing requests.
    /// </summary>
    public class RecorderConcurrentHttpMessageHandlerBuilderFilter : IHttpMessageHandlerBuilderFilter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RecorderConcurrentHttpMessageHandlerBuilderFilter"/> class.
        /// </summary>
        public RecorderConcurrentHttpMessageHandlerBuilderFilter()
        {
        }

        /// <inheritdoc />
        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
        {
            return builder =>
            {
                // Run other configuration first, we want to decorate.
                next(builder);

                // Add the propagation handler to inject context ID as a header
                builder.AdditionalHandlers.Add(new HttpRecorderPropagationHandler());
            };
        }
    }
}
