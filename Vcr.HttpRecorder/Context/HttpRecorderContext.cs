using Microsoft.Extensions.Http;
using System;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Vcr.HttpRecorder.Context
{
    /// <summary>
    /// Sets a global context for the recording.
    /// </summary>
    public sealed class HttpRecorderContext : IDisposable
    {
        private static HttpRecorderContext _current;

        private static volatile ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpRecorderContext"/> class.
        /// </summary>
        /// <param name="configurationFactory">Factory to allow customization per <see cref="HttpClient"/>.</param>
        /// <param name="testName">The <see cref="CallerMemberNameAttribute"/>.</param>
        /// <param name="filePath">The <see cref="CallerFilePathAttribute"/>.</param>
        /// <example>
        /// <![CDATA[
        /// // In service registration.
        /// services.AddRecorderContextSupport();
        ///
        /// // In the test case.
        /// using var context = new HttpRecorderContext();
        /// ]]>
        /// </example>
        public HttpRecorderContext(
            Func<IServiceProvider, HttpMessageHandlerBuilder, HttpRecorderConfiguration> configurationFactory = null,
            [CallerMemberName] string testName = "",
            [CallerFilePath] string filePath = "")
        {
            ConfigurationFactory = configurationFactory;
            TestName = testName;
            FilePath = filePath;
            _lock.EnterWriteLock();
            try
            {
                if (_current != null)
                {
                    throw new HttpRecorderException(
                        $"Cannot use multiple {nameof(HttpRecorderContext)} at the same time. Previous usage: {_current.FilePath}, current usage: {filePath}.");
                }

#pragma warning disable S3010 // We want to allow a single instance here. Modifying static is ok
                _current = this;
#pragma warning restore S3010
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Gets the current <see cref="HttpRecorderContext"/>.
        /// </summary>
        public static HttpRecorderContext Current
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _current;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
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

        /// <inheritdoc/>
        public void Dispose()
        {
            _lock.EnterWriteLock();
            try
            {
#pragma warning disable S2696 // We want to allow exactly one instance here. 
                _current = null;
#pragma warning restore S2696
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }
}