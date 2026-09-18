using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Vcr.HttpRecorder.Anonymizers;
using Vcr.HttpRecorder.Matchers;
using Vcr.HttpRecorder.Repositories;
using Vcr.HttpRecorder.Repositories.HAR;

namespace Vcr.HttpRecorder
{
    /// <summary>
    /// <see cref="DelegatingHandler" /> that records HTTP interactions for integration tests.
    /// </summary>
    public class HttpRecorderDelegatingHandler : DelegatingHandler
    {
        /// <summary>
        /// Gets the name of the environment variable that allows overriding of the <see cref="Mode"/>.
        /// </summary>
        public const string OverridingEnvironmentVariableName = "HTTP_RECORDER_MODE";

        private readonly IRequestMatcher _matcher;
        private readonly IInteractionRepository _repository;
        private readonly IInteractionAnonymizer _anonymizer;
        private readonly SemaphoreSlim _interactionLock = new SemaphoreSlim(1, 1);
        private readonly bool _disposed = false;
        private HttpRecorderMode? _executionMode;
        private Interaction _interaction;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpRecorderDelegatingHandler" /> class.
        /// </summary>
        /// <param name="interactionName">
        /// The name of the interaction.
        /// If you use the default <see cref="IInteractionRepository"/>, this will be the path to the HAR file (relative or absolute) and
        /// if no file extension is provided, .har will be used.
        /// </param>
        /// <param name="mode">The <see cref="HttpRecorderMode" />. Defaults to <see cref="HttpRecorderMode.Auto" />.</param>
        /// <param name="matcher">
        /// The <see cref="IRequestMatcher"/> to use to match interactions with incoming <see cref="HttpRequestMessage"/>.
        /// Defaults to matching Once by <see cref="HttpMethod"/> and <see cref="HttpRequestMessage.RequestUri"/>.
        /// <see cref="RulesMatcher.ByHttpMethod"/> and <see cref="RulesMatcher.ByRequestUri"/>.
        /// </param>
        /// <param name="repository">
        /// The <see cref="IInteractionRepository"/> to use to read/write the interaction.
        /// Defaults to <see cref="HttpArchiveInteractionRepository"/>.
        /// </param>
        /// <param name="anonymizer">
        /// The <see cref="IInteractionAnonymizer"/> to use to anonymize the interaction.
        /// Defaults to <see cref="RulesInteractionAnonymizer.Default"/>.
        /// </param>
        public HttpRecorderDelegatingHandler(
            string interactionName,
            HttpRecorderMode mode = HttpRecorderMode.Auto,
            IRequestMatcher matcher = null,
            IInteractionRepository repository = null,
            IInteractionAnonymizer anonymizer = null)
        {
            InteractionName = interactionName;
            Mode = mode;
            _matcher = matcher ?? RulesMatcher.MatchOnce.ByHttpMethod().ByRequestUri();
            _repository = repository ?? new HttpArchiveInteractionRepository();
            _anonymizer = anonymizer ?? RulesInteractionAnonymizer.Default;
        }

        /// <summary>
        /// Gets the name of the interaction.
        /// </summary>
        public string InteractionName { get; }

        /// <summary>
        /// Gets the <see cref="HttpRecorderMode" />.
        /// </summary>
        public HttpRecorderMode Mode { get; }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _interactionLock.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (Mode == HttpRecorderMode.Passthrough)
            {
                var response = await base.SendAsync(request, cancellationToken);
                return response;
            }

            await _interactionLock.WaitAsync();
            try
            {
                await ResolveExecutionMode(cancellationToken);

                if (_executionMode == HttpRecorderMode.Replay)
                {
                    if (_interaction == null)
                    {
                        _interaction = await _repository.LoadAsync(InteractionName, cancellationToken);
                    }

                    var interactionMessage = _matcher.Match(request, _interaction);
                    if (interactionMessage == null)
                    {
                        throw new HttpRecorderException(
                            $"Unable to find a matching interaction for request {request.Method} {request.RequestUri}.");
                    }

                    return await PostProcessResponse(interactionMessage.Response);
                }

                var start = DateTimeOffset.Now;
                var sw = Stopwatch.StartNew();
                var innerResponse = await base.SendAsync(request, cancellationToken);
                sw.Stop();

                var liveInteractionMessage = new InteractionMessage(
                    innerResponse,
                    new InteractionMessageTimings(start, sw.Elapsed));
                var recordingMessage = await CloneInteractionMessage(liveInteractionMessage, request);

                _interaction = new Interaction(
                    InteractionName,
                    _interaction == null
                        ? new[] { recordingMessage }
                        : _interaction.Messages.Append(recordingMessage));

                _interaction = await _anonymizer.Anonymize(_interaction, cancellationToken);
                _interaction = await _repository.StoreAsync(_interaction, cancellationToken);

                return await PostProcessResponse(innerResponse);
            }
            finally
            {
                _interactionLock.Release();
            }
        }

        /// <summary>
        /// Resolves the current <see cref="_executionMode"/>.
        /// Handles <see cref="OverridingEnvironmentVariableName"/> and <see cref="HttpRecorderMode.Auto"/>, if they are set (in that priority order),
        /// otherwise uses the current <see cref="Mode"/>.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to cancel operation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task ResolveExecutionMode(CancellationToken cancellationToken)
        {
            if (!_executionMode.HasValue)
            {
                var overridingEnvVarValue = Environment.GetEnvironmentVariable(OverridingEnvironmentVariableName);
                if (!string.IsNullOrWhiteSpace(overridingEnvVarValue) &&
                    Enum.TryParse<HttpRecorderMode>(overridingEnvVarValue, out var parsedOverridingEnvVarValue))
                {
                    _executionMode = parsedOverridingEnvVarValue;
                    return;
                }

                if (Mode == HttpRecorderMode.Auto)
                {
                    _executionMode = (await _repository.ExistsAsync(InteractionName, cancellationToken))
                        ? HttpRecorderMode.Replay
                        : HttpRecorderMode.Record;

                    return;
                }

                _executionMode = Mode;
            }
        }

        /// <summary>
        /// Custom processing on <see cref="HttpResponseMessage"/> to better simulate a real response from the network
        /// and allow replayability.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponseMessage"/>.</param>
        /// <returns>The <see cref="HttpResponseMessage"/> returned as convenience.</returns>
        private static async Task<HttpResponseMessage> PostProcessResponse(HttpResponseMessage response)
        {
            response.Content = await CloneContent(response.Content);

            return response;
        }

        /// <summary>
        /// Creates an independent interaction message for anonymization and storage.
        /// </summary>
        /// <param name="message">The live interaction message.</param>
        /// <param name="fallbackRequest">The request to use when the response does not reference one.</param>
        /// <returns>An interaction message whose mutable messages, content, and headers are not shared with the live response.</returns>
        private static async Task<InteractionMessage> CloneInteractionMessage(
            InteractionMessage message,
            HttpRequestMessage fallbackRequest)
        {
            HttpRequestMessage requestSnapshot = null;
            HttpResponseMessage responseSnapshot = null;
            try
            {
                var response = message.Response;
                requestSnapshot = await CloneRequest(response.RequestMessage ?? fallbackRequest);
                responseSnapshot = new HttpResponseMessage(response.StatusCode);
                responseSnapshot.Content = await CloneContent(response.Content);
                responseSnapshot.ReasonPhrase = response.ReasonPhrase;
                responseSnapshot.RequestMessage = requestSnapshot;
                responseSnapshot.Version = response.Version;

                foreach (var header in response.Headers)
                {
                    responseSnapshot.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                return new InteractionMessage(responseSnapshot, message.Timings);
            }
            catch
            {
                responseSnapshot?.Dispose();
                requestSnapshot?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Creates an independent request snapshot for anonymization and storage.
        /// </summary>
        /// <param name="request">The request to clone.</param>
        /// <returns>A cloned request, or null when <paramref name="request"/> is null.</returns>
        private static async Task<HttpRequestMessage> CloneRequest(HttpRequestMessage request)
        {
            if (request == null)
            {
                return null;
            }

            var requestSnapshot = new HttpRequestMessage();
            try
            {
                requestSnapshot.Method = request.Method;
                requestSnapshot.RequestUri = request.RequestUri;
                requestSnapshot.Version = request.Version;
                requestSnapshot.Content = await CloneContent(request.Content);
                foreach (var header in request.Headers)
                {
                    requestSnapshot.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                return requestSnapshot;
            }
            catch
            {
                requestSnapshot.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Creates independently owned content while preserving its headers.
        /// </summary>
        /// <param name="content">The content to clone.</param>
        /// <returns>Cloned content, or null when <paramref name="content"/> is null.</returns>
        private static async Task<HttpContent> CloneContent(HttpContent content)
        {
            if (content == null)
            {
                return null;
            }

            var hasContentLength = content.Headers.Contains("Content-Length");
            var headers = content.Headers
                .Select(header => new
                {
                    header.Key,
                    Values = header.Value.ToArray(),
                })
                .ToArray();
            var contentSnapshot = new ByteArrayContent(
                (byte[])(await content.ReadAsByteArrayAsync()).Clone());
            try
            {
                foreach (var header in headers)
                {
                    contentSnapshot.Headers.TryAddWithoutValidation(header.Key, header.Values);
                }

                if (!hasContentLength)
                {
                    // Avoid adding a computed Content-Length when the source did not contain one.
                    // Explicitly assigning null opts out of the ByteArrayContent header getter's calculation.
                    contentSnapshot.Headers.ContentLength = null;
                }

                return contentSnapshot;
            }
            catch
            {
                contentSnapshot.Dispose();
                throw;
            }
        }

    }
}
