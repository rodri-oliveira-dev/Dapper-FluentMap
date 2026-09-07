using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Mapping;

namespace Dapper.FluentMap.Utils
{
    /// <summary>
    /// Extension methods for <see cref="FluentMapConfiguration"/>.
    /// </summary>
    public static class FluentMapConfigurationExtensions
    {
        private const string AssemblyScanningRequiresUnreferencedCodeMessage =
            "Assembly scanning discovers entity maps by reflection. Register maps explicitly with AddMap<TMap>() when publishing trimmed or Native AOT applications.";

        /// <summary>
        /// Finds all types, from provided assemblies, implementing <see cref="EntityMap{TEntity}"/>
        /// and applies them to <see cref="FluentMapConfiguration"/>,
        /// by calling AddMap and passing an instance of found type.
        /// </summary>
        /// <param name="configuration">The <see cref="FluentMapConfiguration"/> instance.</param>
        /// <param name="assemblies">The assemblies to scan for entity maps.</param>
        [RequiresUnreferencedCode(AssemblyScanningRequiresUnreferencedCodeMessage)]
        public static void ApplyMapsFromAssemblies(this FluentMapConfiguration configuration,  params Assembly[] assemblies)
        {
            if (assemblies == null)
            {
                throw new ArgumentNullException(nameof(assemblies));
            }

            var entityMapTypes = FindTypesImplementingIEntityMap(assemblies);

            if (!entityMapTypes.Any())
            {
                return;
            }

            EnsureNoDuplicateMapping(entityMapTypes);

            AddMaps(configuration, entityMapTypes);
        }


        private static List<(Type Type, Type EntityMapInterface)> FindTypesImplementingIEntityMap(Assembly[] assemblies)
        {
            return assemblies
                .SelectMany(a => a.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface)
                    .Select<Type, (Type Type, Type EntityMapInterface)>(t =>
                    (
                        t,
                        t.GetInterfaces()
                            .Where(i => i.IsGenericType)
                            .FirstOrDefault(i => i.GetGenericTypeDefinition() == typeof(IEntityMap<>))
                    )))
                .Where(t => t.EntityMapInterface != null)
                .ToList();
        }

        private static void EnsureNoDuplicateMapping(List<(Type Type, Type EntityMapInterface)> entityMapTypes)
        {
            var typesWithMultipleMappings = entityMapTypes.GroupBy(t => t.EntityMapInterface)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key.GetGenericArguments().First())
                .ToList();

            if (typesWithMultipleMappings.Any())
                throw new InvalidOperationException(
                    $"Multiple mappings defined for types: '{PrintTypeNames(typesWithMultipleMappings)}'");
        }

        private static string PrintTypeNames(List<Type> multipleMappings)
            => multipleMappings.Aggregate(string.Empty, (previous, next) => $"{previous}, {next.Name}");

        private static void AddMaps(FluentMapConfiguration configuration,
            List<(Type Type, Type EntityMapInterface)> entityMapTypes)
        {
            var addMapMethod = configuration.GetType().GetMethod(nameof(configuration.AddMap))
                               ?? throw new InvalidOperationException(
                                   $"Cannot find {nameof(configuration.AddMap)} method on {configuration.GetType().Name}");

            foreach (var entityMapType in entityMapTypes)
            {
                addMapMethod
                    .MakeGenericMethod(entityMapType.EntityMapInterface.GetGenericArguments())
                    .Invoke(configuration, new[] {Activator.CreateInstance(entityMapType.Type)});
            }
        }
    }
}
