using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace Dapper.FluentMap.Materialization
{
    internal static class MappedRowMaterializer
    {
        internal static IEnumerable<TEntity> Materialize<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            IDataReader reader,
            Type profileType,
            FluentMapRuntime runtime)
            where TEntity : class
        {
            var results = new List<TEntity>();
            var materializer = CreateMaterializer<TEntity>(reader, profileType, runtime);

            while (reader.Read())
            {
                results.Add(materializer(reader));
            }

            return results;
        }

        internal static Func<IDataRecord, TEntity> CreateMaterializer<
            [DynamicallyAccessedMembers(QueryMappedApiAnnotations.MaterializedEntityMemberTypes)]
            TEntity>(
            IDataRecord reader,
            Type profileType,
            FluentMapRuntime runtime)
            where TEntity : class
        {
            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            var columnNames = GetColumnNames(reader);

            Func<IDataRecord, object> generatedMaterializer;
            if (runtime.Registry.TryGetGeneratedMaterializer(
                typeof(TEntity),
                profileType,
                columnNames,
                out generatedMaterializer))
            {
                return record => (TEntity)generatedMaterializer(record);
            }

            if (runtime.StrictGeneratedMaterialization)
            {
                ThrowMissingGeneratedMaterializer<TEntity>(profileType, runtime, columnNames);
            }

            var plan = runtime.Registry.GetMaterializationPlan(typeof(TEntity), profileType, columnNames);
            return record => (TEntity)plan.Materialize(record);
        }

        internal static Func<IDataRecord, TEntity> CreateGeneratedMaterializer<TEntity>(
            IDataRecord reader,
            Type profileType,
            FluentMapRuntime runtime)
            where TEntity : class
        {
            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            var columnNames = GetColumnNames(reader);

            Func<IDataRecord, object> generatedMaterializer;
            if (runtime.Registry.TryGetGeneratedMaterializer(
                typeof(TEntity),
                profileType,
                columnNames,
                out generatedMaterializer))
            {
                return record => (TEntity)generatedMaterializer(record);
            }

            ThrowMissingGeneratedMaterializer<TEntity>(profileType, runtime, columnNames);
            return null;
        }

        private static void ThrowMissingGeneratedMaterializer<TEntity>(
            Type profileType,
            FluentMapRuntime runtime,
            string[] columnNames)
            where TEntity : class
        {
            throw new FluentMapConfigurationException(
                "Strict generated materialization is enabled, but FluentMap could not resolve a compatible generated materializer. " +
                runtime.Registry.DescribeMissingGeneratedMaterializer(typeof(TEntity), profileType, columnNames) +
                " Register a generated materializer for this exact entity/profile/result-column shape, query the documented generated shape, or disable strict generated materialization for this runtime.");
        }

        private static string[] GetColumnNames(IDataRecord reader)
        {
            var columnNames = new string[reader.FieldCount];
            for (var i = 0; i < columnNames.Length; i++)
            {
                columnNames[i] = reader.GetName(i);
            }

            return columnNames;
        }
    }
}
