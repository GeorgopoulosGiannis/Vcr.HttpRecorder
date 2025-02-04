using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Http;

namespace HttpRecorder.Context
{
    /// <summary>
    /// Sets a global context for the recording.
    /// </summary>
    public sealed class HttpRecorderConcurrentContext : IDisposable
    {
        private static readonly ConcurrentDictionary<string, HttpRecorderConcurrentContext> contexts
            = new ConcurrentDictionary<string, HttpRecorderConcurrentContext>();

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpRecorderContext"/> class.
        /// </summary>
        /// <param name="configurationFactory">Factory to allow customization per <see cref="HttpClient"/>.</param>
        /// <param name="testName">The <see cref="CallerMemberNameAttribute"/>.</param>
        /// <param name="filePath">The <see cref="CallerFilePathAttribute"/>.</param>
        /// <param name="lineNumber"></param>
        /// <example>
        /// <![CDATA[
        /// // In service registration.
        /// services.AddConcurrentHttpRecorderContextSupport();
        /// 
        /// // In the test case.
        /// using var context = new HttpRecorderConcurrentContext();
        /// ]]>
        /// </example>
        /// <remarks>
        /// `services.AddConcurrentHttpRecorderContextSupport();` and `using var context = new HttpRecorderConcurrentContext();`
        /// should be placed under the same test method
        /// </remarks>
        public HttpRecorderConcurrentContext(
            Func<IServiceProvider, HttpMessageHandlerBuilder, HttpRecorderConfiguration> configurationFactory = null,
            [CallerMemberName] string testName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            ConfigurationFactory = configurationFactory;
            TestName = testName;
            FilePath = filePath;
            LineNumber = lineNumber;
            Identifier = new HttpRecordedContextIdentifier(FilePath, testName);
            

            if (!contexts.TryAdd(Identifier.Value, this))
            {
                throw new HttpRecorderException(
                    $"Cannot use multiple {nameof(HttpRecorderContext)} for the same identifier: {Identifier} at the same time. Previous usage line number: {LineNumber}, current usage line number: {lineNumber}.");
            }
        }

        /// <summary>
        /// Retrieves the <see cref="HttpRecorderConcurrentContext"/> with the associated <see cref="Identifier"/>
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        /// <exception cref="HttpRecorderException"></exception>
        public static HttpRecorderConcurrentContext GetContext(HttpRecordedContextIdentifier identifier)
        {
            if (!contexts.TryGetValue(identifier.Value, out var context))
            {
                throw new HttpRecorderException($"Could not find {nameof(HttpRecorderConcurrentContext)} for input identifier {identifier}." +
                                                $"Make sure that {nameof(HttpRecorderServiceCollectionExtensions.AddHttpRecorderContextSupport)} has been called in the same function were the `using var ctx= {nameof(HttpRecorderContext)}` was called");
            }

            return context;
        }

        /// <summary>
        /// Gets the configuration factory.
        /// </summary>
        public Func<IServiceProvider, HttpMessageHandlerBuilder, HttpRecorderConfiguration> ConfigurationFactory { get; }

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
        /// <example>{FilePath}.{TestName}</example>
        /// </summary>
        public HttpRecordedContextIdentifier Identifier { get; }

        /// <inheritdoc/>
        [SuppressMessage("Design", "CA1063:Implement IDisposable Correctly", Justification = "Dispose pattern used for context here, not resource diposal.")]
        [SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize",
            Justification = "Dispose pattern used for context here, not resource diposal.")]
        public void Dispose()
        {
            contexts.TryRemove(Identifier.Value, out _);
        }
    }
}
