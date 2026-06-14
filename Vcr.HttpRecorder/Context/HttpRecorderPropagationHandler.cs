using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Vcr.HttpRecorder.Context
{
    /// <summary>
    /// A <see cref="DelegatingHandler"/> that propagates the current <see cref="HttpRecorderConcurrentContext"/>
    /// to outgoing requests by adding a custom header.
    /// </summary>
    public sealed class HttpRecorderPropagationHandler : DelegatingHandler
    {
        private const string RecorderContextIdHeader = "X-Vcr-HttpRecorder-ContextId";

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpRecorderPropagationHandler"/> class.
        /// </summary>
        public HttpRecorderPropagationHandler()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpRecorderPropagationHandler"/> class
        /// with a specific inner handler.
        /// </summary>
        /// <param name="innerHandler">The inner handler.</param>
        public HttpRecorderPropagationHandler(HttpMessageHandler innerHandler) : base(innerHandler)
        {
        }

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var context = HttpRecorderConcurrentContext.Current;

            if (context != null)
            {
                request.Headers.TryAddWithoutValidation(RecorderContextIdHeader, context.ContextId);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
