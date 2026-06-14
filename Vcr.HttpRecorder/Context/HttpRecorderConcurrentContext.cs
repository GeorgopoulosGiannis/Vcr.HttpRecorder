using Microsoft.Extensions.Http;
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Vcr.HttpRecorder.Context
{
    /// <summary>
    /// Sets a global context for the recording.
    /// </summary>
    public sealed class HttpRecorderConcurrentContext : IDisposable
    {
        private static readonly AsyncLocal<HttpRecorderConcurrentContext> _currentContext = new AsyncLocal<HttpRecorderConcurrentContext>();
        private static readonly ConcurrentDictionary<string, HttpRecorderConcurrentContext> _activeContexts = new ConcurrentDictionary<string, HttpRecorderConcurrentContext>();

        private readonly string _contextId;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpRecorderConcurrentContext"/> class.
        /// </summary>
        /// <param name="configurationFactory">Factory to allow customization per <see cref="HttpClient"/>.</param>
        /// <param name="testName">The <see cref="CallerMemberNameAttribute"/>.</param>
        /// <param name="filePath">The <see cref="CallerFilePathAttribute"/>.</param>
        /// <param name="lineNumber"></param>
        /// <example>
        /// <![CDATA[
        /// // In service registration (e.g., WebApplicationFactory.ConfigureServices).
        /// services.AddHttpRecorderConcurrentContextSupport();
        /// 
        /// // In the test case.
        /// using var context = new HttpRecorderConcurrentContext();
        /// ]]>
        /// </example>
        public HttpRecorderConcurrentContext(
            Func<IServiceProvider, HttpMessageHandlerBuilder, HttpRecorderConfiguration> configurationFactory = null,
            [CallerMemberName] string testName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            _contextId = Guid.NewGuid().ToString("N");
            ConfigurationFactory = configurationFactory;
            TestName = testName;
            FilePath = filePath;
            LineNumber = lineNumber;
            Identifier = new HttpRecordedContextIdentifier(FilePath, testName); // Still useful for debugging, but not for matching HttpClient.

            if (_currentContext.Value != null)
            {
                throw new HttpRecorderException(
                    $"Cannot use multiple {nameof(HttpRecorderConcurrentContext)} in the same asynchronous flow. " +
                    $"Previous context created at {(_currentContext.Value.FilePath, _currentContext.Value.TestName, _currentContext.Value.LineNumber)}, " +
                    $"current context creation at {(FilePath, TestName, LineNumber)}.");
            }

            _currentContext.Value = this;
            _activeContexts.TryAdd(_contextId, this);
        }

        /// <summary>
        /// Gets the unique identifier for this context instance.
        /// </summary>
        public string ContextId => _contextId;

        /// <summary>
        /// Gets the currently active <see cref="HttpRecorderConcurrentContext"/> for the current asynchronous flow.
        /// Returns null if no context is active.
        /// </summary>
        public static HttpRecorderConcurrentContext Current => _currentContext.Value;

        /// <summary>
        /// Retrieves an active context by its <see cref="ContextId"/>.
        /// Returns null if no context with that ID is currently active.
        /// </summary>
        /// <param name="contextId">The context identifier.</param>
        /// <returns>The matching <see cref="HttpRecorderConcurrentContext"/> or null.</returns>
        public static HttpRecorderConcurrentContext GetContextById(string contextId)
        {
            _activeContexts.TryGetValue(contextId, out var context);
            return context;
        }

        /// <summary>
        /// Gets the configuration factory.
        /// </summary>
        public Func<IServiceProvider, HttpMessageHandlerBuilder, HttpRecorderConfiguration> ConfigurationFactory
        {
            get;
        }

        /// <summary>
        /// Gets the TestName, which should be the <see cref="CallerMemberNameAttribute"/>.
        /// </summary>
        public string TestName { get; }

        /// <summary>
        /// Gets the Test file path, which should be the <see cref="CallerFilePathAttribute"/>.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Gets the LineNumber, which should be the <see cref="CallerLineNumberAttribute"/>
        /// </summary>
        public int LineNumber { get; }

        /// <summary>
        /// Gets the identifier for the context, which should be the <see cref="FilePath"/> combined with the <see cref="TestName"/>.
        /// This identifier is primarily used for debugging and logging purposes, not for matching HttpClients.
        /// <example>{FilePath}.{TestName}</example>
        /// </summary>
        public HttpRecordedContextIdentifier Identifier { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _currentContext.Value = null;
                _activeContexts.TryRemove(_contextId, out _);
            }
        }
    }
}
