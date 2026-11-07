// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.DependencyInjection;
using NezamMonitorPro.Security.Services;

namespace NezamMonitorPro.Security.Extensions
{
    /// <summary>
    /// Extension methods for registering NezamMonitorPro.Security services in the DI container.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds NezamMonitorPro.Security services to the DI container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="credentialsFilePath">Path to the credentials JSON file.</param>
        /// <param name="databasePath">Path to the encrypted database file.</param>
        /// <returns>The service collection for chaining.</param>
        public static IServiceCollection AddNezamSecurity(
            this IServiceCollection services,
            string credentialsFilePath,
            string databasePath)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrEmpty(credentialsFilePath))
                throw new ArgumentException("Credentials file path cannot be null or empty", nameof(credentialsFilePath));
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Database path cannot be null or empty", nameof(databasePath));

            services.AddSingleton<CredentialStore>(sp => new CredentialStore(credentialsFilePath));
            services.AddSingleton<DatabaseEncryptionService>(sp =>
                new DatabaseEncryptionService(
                    sp.GetRequiredService<CredentialStore>(),
                    databasePath));

            return services;
        }

        /// <summary>
        /// Adds NezamMonitorPro.Security services with default paths.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="appDataDirectory">Base directory for app data (credentials and database).</param>
        /// <returns>The service collection for chaining.</param>
        public static IServiceCollection AddNezamSecurity(
            this IServiceCollection services,
            string appDataDirectory)
        {
            if (string.IsNullOrEmpty(appDataDirectory))
                throw new ArgumentException("App data directory cannot be null or empty", nameof(appDataDirectory));

            var credentialsPath = System.IO.Path.Combine(appDataDirectory, "data", "credentials.json");
            var databasePath = System.IO.Path.Combine(appDataDirectory, "data", "nezam_monitor.db");

            return services.AddNezamSecurity(credentialsPath, databasePath);
        }
    }
}