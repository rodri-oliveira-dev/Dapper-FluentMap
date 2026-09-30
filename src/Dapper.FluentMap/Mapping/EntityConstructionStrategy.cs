using System;
using System.Collections.Generic;
using System.Linq;

namespace Dapper.FluentMap.Mapping
{
    internal interface IEntityMapWithConstructionStrategy
    {
        EntityConstructionStrategy ConstructionStrategy { get; }
    }

    internal sealed class EntityConstructionStrategy
    {
        internal EntityConstructionStrategy(
            Type entityType,
            IEnumerable<ConstructionValueBinding> bindings,
            Func<object[], object> factory)
        {
            EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
            Bindings = (bindings ?? throw new ArgumentNullException(nameof(bindings))).ToArray();
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));

            if (Bindings.Count == 0)
            {
                throw new FluentMapConfigurationException(
                    $"Explicit construction for entity '{entityType.FullName}' requires at least one mapped property binding.");
            }

            var duplicate = Bindings
                .GroupBy(binding => binding.MemberPath)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
            {
                throw new FluentMapConfigurationException(
                    $"Explicit construction for entity '{entityType.FullName}' binds property path '{duplicate.Key}' more than once.");
            }
        }

        internal Type EntityType { get; }

        internal IReadOnlyList<ConstructionValueBinding> Bindings { get; }

        internal Func<object[], object> Factory { get; }
    }

    internal sealed class ConstructionValueBinding
    {
        internal ConstructionValueBinding(MemberPath memberPath, Type valueType)
        {
            MemberPath = memberPath ?? throw new ArgumentNullException(nameof(memberPath));
            ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
        }

        internal MemberPath MemberPath { get; }

        internal Type ValueType { get; }
    }
}
