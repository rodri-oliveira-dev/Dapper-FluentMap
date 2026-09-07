using System;
using Dapper.FluentMap;
using Dapper.FluentMap.Configuration;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides dependency injection registration methods for Dapper.FluentMap.
    /// </summary>
    public static class FluentMapServiceCollectionExtensions
    {
        /// <summary>
        /// Builds and validates a FluentMap configuration, then registers the immutable configuration
        /// and its runtime as singleton services.
        /// </summary>
        /// <param name="services">The service collection to add FluentMap services to.</param>
        /// <param name="configure">The startup registration callback used to configure FluentMap maps, profiles, conventions and generated materializers.</param>
        /// <returns>The same service collection so calls can be chained.</returns>
        public static IServiceCollection AddFluentMap(
            this IServiceCollection services,
            Action<FluentMapConfigurationBuilder> configure)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var builder = new FluentMapConfigurationBuilder();
            configure(builder);

            var configuration = builder.Build();
            var runtime = configuration.CreateRuntime();
            runtime.Validate();

            services.AddSingleton(configuration);
            services.AddSingleton(runtime);

            return services;
        }
    }
}
