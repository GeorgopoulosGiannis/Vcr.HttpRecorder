﻿using System;
using System.Net;
using System.Net.Http;

namespace Vcr.HttpRecorder.Repositories.HAR
{
    /// <summary>
    /// Contains detailed info about the response.
    /// https://w3c.github.io/web-performance/specs/HAR/Overview.html#response.
    /// </summary>
    public class Response : Message
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Response"/> class.
        /// </summary>
        public Response()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Response"/> class from <paramref name="response"/>.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponseMessage"/> to initialize from.</param>
        public Response(HttpResponseMessage response)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            HttpVersion = $"{HTTPVERSIONPREFIX}{response.Version}";
            Status = (int)response.StatusCode;
            StatusText = response.ReasonPhrase;
            if (response.Headers.Location != null)
            {
                RedirectUrl = response.Headers.Location.ToString();
            }

            foreach (var header in response.Headers)
            {
                Headers.Add(new Header(header));
            }

            if (response.Content != null)
            {
                foreach (var header in response.Content.Headers)
                {
                    Headers.Add(new Header(header));
                }

                BodySize = response.Content.ReadAsByteArrayAsync().Result.Length;
                Content = new Content(response.Content);
            }
        }

        /// <summary>
        /// Gets or sets the response status.
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// Gets or sets the response status description.
        /// </summary>
        public string StatusText { get; set; }

        /// <summary>
        /// Gets or sets the details about the response body.
        /// </summary>
        public Content Content { get; set; } = new Content();

        /// <summary>
        /// Gets or sets the redirection target URL from the Location response header.
        /// </summary>
        /// <remarks>
        /// This property must have the <c>URL</c> part in uppercase to observe the <a href="https://w3c.github.io/web-performance/specs/HAR/Overview.html">HAR specification</a>.
        /// Renaming this property to <c>RedirectUrl</c> could break tools implementing the HAR specification.
        /// </remarks>
        public string RedirectUrl { get; set; } = string.Empty;

        /// <summary>
        /// Returns a <see cref="HttpResponseMessage"/>.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessage"/> created from this.</returns>
        public HttpResponseMessage ToHttpResponseMessage()
        {
            var response = new HttpResponseMessage
            {
                Content = Content?.ToHttpContent() ?? new ByteArrayContent(Array.Empty<byte>()),
                StatusCode = (HttpStatusCode)Status,
                ReasonPhrase = StatusText,
                Version = GetVersion(),
            };
            AddHeadersWithoutValidation(response.Headers);
            AddHeadersWithoutValidation(response.Content?.Headers);
            if (!response.Content?.Headers.TryGetValues("Content-Length", out var _) ?? false)
            {
                response.Content.Headers.ContentLength = null;
            }

            return response;
        }
    }
}