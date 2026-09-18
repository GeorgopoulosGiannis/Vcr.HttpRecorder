using FluentAssertions;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vcr.HttpRecorder.Anonymizers;
using Vcr.HttpRecorder.Repositories;
using Vcr.HttpRecorder.Repositories.HAR;
using Xunit;

namespace Vcr.HttpRecorder.Tests
{
    public class HttpRecorderResponseIsolationTests
    {
        [Fact]
        public async Task ItShouldPreserveLiveResponseBodyWhenRecordingAnonymizedBody()
        {
            const string OriginalJson = "{\"access_token\":\"fake-live-token\"}";
            const string MaskedJson = "{\"access_token\":\"******\"}";
            var cassettePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.har");
            var repository = new HttpArchiveInteractionRepository();
            var anonymizer = RulesInteractionAnonymizer.Default.WithRule(message =>
                message.Response.Content = new StringContent(MaskedJson, Encoding.UTF8, "application/json"));

            try
            {
                using var client = CreateClient(
                    cassettePath,
                    HttpRecorderMode.Record,
                    anonymizer,
                    new StaticResponseHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(OriginalJson, Encoding.UTF8, "application/json"),
                    }));

                using var liveResponse = await client.GetAsync("oauth/token");

                (await liveResponse.Content.ReadAsStringAsync()).Should().Be(OriginalJson);
                var recording = await repository.LoadAsync(cassettePath);
                (await recording.Messages[0].Response.Content.ReadAsStringAsync()).Should().Be(MaskedJson);
            }
            finally
            {
                if (File.Exists(cassettePath))
                {
                    File.Delete(cassettePath);
                }
            }
        }

        [Fact]
        public async Task ItShouldReplayAnonymizedBodyAfterAutoRecordsOriginalLiveBody()
        {
            const string OriginalJson = "{\"access_token\":\"fake-live-token\"}";
            const string MaskedJson = "{\"access_token\":\"******\"}";
            var cassettePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".har");
            var anonymizer = RulesInteractionAnonymizer.Default.WithRule(message =>
                message.Response.Content = new StringContent(MaskedJson, Encoding.UTF8, "application/json"));

            try
            {
                using (var recordingClient = CreateClient(
                           cassettePath,
                           HttpRecorderMode.Auto,
                           anonymizer,
                           new StaticResponseHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
                           {
                               Content = new StringContent(OriginalJson, Encoding.UTF8, "application/json"),
                           })))
                using (var liveResponse = await recordingClient.GetAsync("oauth/token"))
                {
                    (await liveResponse.Content.ReadAsStringAsync()).Should().Be(OriginalJson);
                }

                using var replayClient = CreateClient(
                    cassettePath,
                    HttpRecorderMode.Auto,
                    anonymizer,
                    new ThrowingHandler());
                using var replayResponse = await replayClient.GetAsync("oauth/token");

                (await replayResponse.Content.ReadAsStringAsync()).Should().Be(MaskedJson);
            }
            finally
            {
                if (File.Exists(cassettePath))
                {
                    File.Delete(cassettePath);
                }
            }
        }

        [Fact]
        public async Task ItShouldIsolateResponseAndRequestAnonymizationFromLiveMessages()
        {
            const string OriginalRequestBody = "original request";
            const string OriginalResponseBody = "original response";
            var repository = new CapturingRepository();
            var anonymizer = RulesInteractionAnonymizer.Default.WithRule(message =>
            {
                message.Response.Headers.Remove("X-Response-Secret");
                message.Response.Headers.TryAddWithoutValidation("X-Response-Secret", "masked");
                message.Response.RequestMessage.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", "masked");
                message.Response.RequestMessage.Content.Headers.Remove("X-Content-Secret");
                message.Response.RequestMessage.Content.Headers.TryAddWithoutValidation("X-Content-Secret", "masked");
            });
            using var client = CreateClient(
                "unused",
                HttpRecorderMode.Record,
                anonymizer,
                new StaticResponseHandler(() =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(OriginalResponseBody),
                    };
                    response.Headers.TryAddWithoutValidation("X-Response-Secret", "original");
                    return response;
                }),
                repository);
            using var request = new HttpRequestMessage(HttpMethod.Post, "resource")
            {
                Content = new StringContent(OriginalRequestBody),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "original");
            request.Content.Headers.TryAddWithoutValidation("X-Content-Secret", "original");

            using var liveResponse = await client.SendAsync(request);
            var recordedResponse = repository.StoredInteraction.Messages[0].Response;

            liveResponse.Should().NotBeSameAs(recordedResponse);
            liveResponse.Content.Should().NotBeSameAs(recordedResponse.Content);
            liveResponse.RequestMessage.Should().NotBeSameAs(recordedResponse.RequestMessage);
            liveResponse.RequestMessage.Content.Should().NotBeSameAs(recordedResponse.RequestMessage.Content);
            liveResponse.Headers.GetValues("X-Response-Secret").Should().ContainSingle().Which.Should().Be("original");
            recordedResponse.Headers.GetValues("X-Response-Secret").Should().ContainSingle().Which.Should().Be("masked");
            liveResponse.RequestMessage.Headers.Authorization.Parameter.Should().Be("original");
            recordedResponse.RequestMessage.Headers.Authorization.Parameter.Should().Be("masked");
            liveResponse.RequestMessage.Content.Headers.GetValues("X-Content-Secret")
                .Should().ContainSingle().Which.Should().Be("original");
            recordedResponse.RequestMessage.Content.Headers.GetValues("X-Content-Secret")
                .Should().ContainSingle().Which.Should().Be("masked");
            (await liveResponse.RequestMessage.Content.ReadAsStringAsync()).Should().Be(OriginalRequestBody);
            (await recordedResponse.RequestMessage.Content.ReadAsStringAsync()).Should().Be(OriginalRequestBody);
        }

        [Fact]
        public async Task ItShouldPreserveSnapshotMetadataAndFallbackRequest()
        {
            var repository = new CapturingRepository();
            var responseBytes = new byte[] { 0, 1, 2, 128, 255 };
            using var client = CreateClient(
                "unused",
                HttpRecorderMode.Record,
                CreateNoOpAnonymizer(),
                new StaticResponseHandler(() =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.Accepted)
                    {
                        Content = new ByteArrayContent(responseBytes),
                        ReasonPhrase = "Accepted for processing",
                        Version = HttpVersion.Version10,
                    };
                    response.Headers.TryAddWithoutValidation("X-Multi", new[] { "first", "second" });
                    response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    response.Content.Headers.ContentLength = null;
                    return response;
                }, setRequestMessage: false),
                repository);
            using var request = new HttpRequestMessage(HttpMethod.Get, "fallback")
            {
                Version = HttpVersion.Version11,
            };
            request.Headers.TryAddWithoutValidation("X-Request", "value");

            using var liveResponse = await client.SendAsync(request);
            var recordedResponse = repository.StoredInteraction.Messages[0].Response;

            recordedResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
            recordedResponse.ReasonPhrase.Should().Be("Accepted for processing");
            recordedResponse.Version.Should().Be(HttpVersion.Version10);
            recordedResponse.Headers.GetValues("X-Multi").Should().Equal("first", "second");
            (await recordedResponse.Content.ReadAsByteArrayAsync()).Should().Equal(responseBytes);
            recordedResponse.Content.Headers.ContentType.MediaType.Should().Be("application/octet-stream");
            recordedResponse.Content.Headers.Contains("Content-Length").Should().BeFalse();
            liveResponse.Content.Headers.Contains("Content-Length").Should().BeFalse();
            recordedResponse.RequestMessage.Method.Should().Be(HttpMethod.Get);
            recordedResponse.RequestMessage.RequestUri.Should().Be(new Uri("https://example.test/fallback"));
            recordedResponse.RequestMessage.Version.Should().Be(HttpVersion.Version11);
            recordedResponse.RequestMessage.Headers.GetValues("X-Request")
                .Should().ContainSingle().Which.Should().Be("value");
            recordedResponse.RequestMessage.Content.Should().BeNull();
        }

        [Fact]
        public async Task ItShouldPreferResponseRequestMessageOverFallbackRequest()
        {
            var repository = new CapturingRepository();
            using var redirectedRequest = new HttpRequestMessage(
                HttpMethod.Get,
                "https://redirected.example.test/final");
            redirectedRequest.Headers.TryAddWithoutValidation("X-Redirected", "true");
            using var client = CreateClient(
                "unused",
                HttpRecorderMode.Record,
                CreateNoOpAnonymizer(),
                new StaticResponseHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("body"),
                    RequestMessage = redirectedRequest,
                }, setRequestMessage: false),
                repository);
            using var originalRequest = new HttpRequestMessage(HttpMethod.Get, "original");

            using var liveResponse = await client.SendAsync(originalRequest);
            var recordedRequest = repository.StoredInteraction.Messages[0].Response.RequestMessage;

            recordedRequest.RequestUri.Should().Be(new Uri("https://redirected.example.test/final"));
            recordedRequest.Headers.GetValues("X-Redirected").Should().ContainSingle().Which.Should().Be("true");
            recordedRequest.Should().NotBeSameAs(redirectedRequest);
        }

        [Fact]
        public async Task ItShouldKeepLiveAndRecordedContentIndependentlyDisposable()
        {
            const string Body = "independent body";
            var repository = new CapturingRepository();
            using var client = CreateClient(
                "unused",
                HttpRecorderMode.Record,
                CreateNoOpAnonymizer(),
                new StaticResponseHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(Body),
                }),
                repository);

            var firstLiveResponse = await client.GetAsync("first");
            var firstRecordedResponse = repository.StoredInteraction.Messages[0].Response;
            firstLiveResponse.Dispose();

            (await firstRecordedResponse.Content.ReadAsStringAsync()).Should().Be(Body);

            using var secondLiveResponse = await client.GetAsync("second");
            var secondRecordedResponse = repository.StoredInteraction.Messages[1].Response;
            secondRecordedResponse.Dispose();

            (await secondLiveResponse.Content.ReadAsStringAsync()).Should().Be(Body);
        }

        [Fact]
        public async Task ItShouldPreserveExplicitContentLengths()
        {
            var repository = new CapturingRepository();
            var responseBytes = new byte[] { 10, 20, 30 };
            using var client = CreateClient(
                "unused",
                HttpRecorderMode.Record,
                CreateNoOpAnonymizer(),
                new StaticResponseHandler(() =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(responseBytes),
                    };
                    response.Content.Headers.ContentLength = responseBytes.Length;
                    return response;
                }),
                repository);
            var requestBytes = new byte[] { 40, 50 };
            using var request = new HttpRequestMessage(HttpMethod.Post, "lengths")
            {
                Content = new ByteArrayContent(requestBytes),
            };
            request.Content.Headers.ContentLength = requestBytes.Length;

            using var liveResponse = await client.SendAsync(request);
            var recordedResponse = repository.StoredInteraction.Messages[0].Response;

            recordedResponse.Content.Headers.ContentLength.Should().Be(responseBytes.Length);
            liveResponse.Content.Headers.ContentLength.Should().Be(responseBytes.Length);
            recordedResponse.RequestMessage.Content.Headers.ContentLength.Should().Be(requestBytes.Length);
            (await recordedResponse.RequestMessage.Content.ReadAsByteArrayAsync()).Should().Equal(requestBytes);
        }

        [Fact]
        public async Task ItShouldLeavePassthroughResponseUntouched()
        {
            const string OriginalBody = "original";
            var repository = new CapturingRepository();
            var anonymizer = RulesInteractionAnonymizer.Default.WithRule(message =>
                message.Response.Content = new StringContent("masked"));
            using var client = CreateClient(
                "unused",
                HttpRecorderMode.Passthrough,
                anonymizer,
                new StaticResponseHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(OriginalBody),
                }),
                repository);

            using var response = await client.GetAsync("passthrough");

            (await response.Content.ReadAsStringAsync()).Should().Be(OriginalBody);
            repository.StoreCount.Should().Be(0);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public async Task ItShouldAlwaysIsolateRecordedMessages(int anonymizerKind)
        {
            const string RequestBody = "original request";
            const string ResponseBody = "original response";
            var repository = new CapturingRepository();
            InteractionMessageTimings observedTimings = null;
            IInteractionAnonymizer anonymizer = anonymizerKind switch
            {
                0 => null,
                1 => RulesInteractionAnonymizer.Default,
                _ => RulesInteractionAnonymizer.Default.WithRule(message => observedTimings = message.Timings),
            };
            using var client = CreateClient(
                "unused",
                HttpRecorderMode.Record,
                anonymizer,
                new StaticResponseHandler(() =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(ResponseBody),
                    };
                    response.Headers.TryAddWithoutValidation("X-Response", "original");
                    return response;
                }),
                repository);
            using var request = new HttpRequestMessage(HttpMethod.Post, "default")
            {
                Content = new StringContent(RequestBody),
            };
            request.Headers.TryAddWithoutValidation("X-Request", "original");
            var beforeSend = DateTimeOffset.Now;

            using var liveResponse = await client.SendAsync(request);
            var afterSend = DateTimeOffset.Now;
            var recordedMessage = repository.StoredInteraction.Messages[0];
            var recordedResponse = recordedMessage.Response;
            var recordedRequest = recordedResponse.RequestMessage;

            recordedMessage.Timings.Should().NotBeNull();
            recordedMessage.Timings.StartedDateTime.Should().BeOnOrAfter(beforeSend);
            recordedMessage.Timings.StartedDateTime.Should().BeOnOrBefore(afterSend);
            recordedMessage.Timings.Time.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
            if (anonymizerKind == 2)
            {
                recordedMessage.Timings.Should().BeSameAs(observedTimings);
            }

            recordedResponse.Should().NotBeSameAs(liveResponse);
            recordedRequest.Should().NotBeSameAs(request);
            recordedResponse.Content.Should().NotBeSameAs(liveResponse.Content);
            recordedRequest.Content.Should().NotBeSameAs(request.Content);
            recordedRequest.Method.Should().Be(request.Method);
            recordedRequest.RequestUri.Should().Be(request.RequestUri);
            recordedRequest.Headers.GetValues("X-Request").Should().ContainSingle().Which.Should().Be("original");
            recordedResponse.Headers.GetValues("X-Response").Should().ContainSingle().Which.Should().Be("original");
            var recordedRequestBytes = await recordedRequest.Content.ReadAsByteArrayAsync();
            var liveRequestBytes = await request.Content.ReadAsByteArrayAsync();
            recordedRequestBytes.Should().Equal(liveRequestBytes);
            recordedRequestBytes.Should().NotBeSameAs(liveRequestBytes);
            var recordedResponseBytes = await recordedResponse.Content.ReadAsByteArrayAsync();
            var liveResponseBytes = await liveResponse.Content.ReadAsByteArrayAsync();
            recordedResponseBytes.Should().Equal(liveResponseBytes);
            recordedResponseBytes.Should().NotBeSameAs(liveResponseBytes);

            recordedRequest.Headers.Remove("X-Request");
            recordedResponse.Headers.Remove("X-Response");
            recordedRequest.Content.Dispose();
            recordedResponse.Dispose();

            request.Headers.GetValues("X-Request").Should().ContainSingle().Which.Should().Be("original");
            liveResponse.Headers.GetValues("X-Response").Should().ContainSingle().Which.Should().Be("original");
            (await request.Content.ReadAsStringAsync()).Should().Be(RequestBody);
            (await liveResponse.Content.ReadAsStringAsync()).Should().Be(ResponseBody);
        }

        private static IInteractionAnonymizer CreateNoOpAnonymizer()
            => RulesInteractionAnonymizer.Default.WithRule(_ => { });

        private static HttpClient CreateClient(
            string cassettePath,
            HttpRecorderMode mode,
            IInteractionAnonymizer anonymizer,
            HttpMessageHandler innerHandler,
            IInteractionRepository repository = null)
            => new HttpClient(new HttpRecorderDelegatingHandler(
                cassettePath,
                mode,
                repository: repository,
                anonymizer: anonymizer)
            {
                InnerHandler = innerHandler,
            })
            {
                BaseAddress = new Uri("https://example.test/"),
            };

        private sealed class StaticResponseHandler : HttpMessageHandler
        {
            private readonly Func<HttpResponseMessage> _responseFactory;
            private readonly bool _setRequestMessage;

            public StaticResponseHandler(
                Func<HttpResponseMessage> responseFactory,
                bool setRequestMessage = true)
            {
                _responseFactory = responseFactory;
                _setRequestMessage = setRequestMessage;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var response = _responseFactory();
                if (_setRequestMessage)
                {
                    response.RequestMessage = request;
                }

                return Task.FromResult(response);
            }
        }

        private sealed class ThrowingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
                => throw new InvalidOperationException("The inner handler must not be called during replay.");
        }

        private sealed class CapturingRepository : IInteractionRepository
        {
            public Interaction StoredInteraction { get; private set; }

            public int StoreCount { get; private set; }

            public Task<bool> ExistsAsync(
                string interactionName,
                CancellationToken cancellationToken = default)
                => Task.FromResult(StoredInteraction != null);

            public Task<Interaction> LoadAsync(
                string interactionName,
                CancellationToken cancellationToken = default)
                => Task.FromResult(StoredInteraction);

            public Task<Interaction> StoreAsync(
                Interaction interaction,
                CancellationToken cancellationToken = default)
            {
                StoredInteraction = interaction;
                StoreCount++;
                return Task.FromResult(interaction);
            }
        }
    }
}
