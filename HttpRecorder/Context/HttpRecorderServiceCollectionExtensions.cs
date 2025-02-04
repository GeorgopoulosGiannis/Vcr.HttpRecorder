﻿using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;

namespace HttpRecorder.Context
{
    /// <summary>
    /// <see cref="IServiceCollection"/> extension methods.
    /// </summary>
    public static class HttpRecorderServiceCollectionExtensions
    {
        /// <summary>
        /// Enables support for the <see cref="HttpRecorderContext"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="testName">The <see cref="CallerMemberNameAttribute"/>.</param>
        /// <param name="filePath">The <see cref="CallerFilePathAttribute"/>.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddHttpRecorderContextSupport(this IServiceCollection services,
            [CallerMemberName] string testName = "",
            [CallerFilePath] string filePath = "")
        {
            var identifier = new HttpRecordedContextIdentifier(filePath, testName);
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, RecorderHttpMessageHandlerBuilderFilter>(provider =>
                new RecorderHttpMessageHandlerBuilderFilter(provider, identifier)));

            return services;
        }
    }
}
