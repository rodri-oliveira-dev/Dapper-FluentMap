using System;
using System.Reflection;

namespace Dapper.FluentMap.Diagnostics
{
    /// <summary>
    /// Describes a constructor parameter that can receive a mapped column.
    /// </summary>
    public sealed class ConstructorParameterExplanation
    {
        internal ConstructorParameterExplanation(ConstructorInfo constructor, ParameterInfo parameter)
        {
            if (constructor == null)
            {
                throw new ArgumentNullException(nameof(constructor));
            }

            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            Constructor = constructor;
            Name = parameter.Name;
            ParameterType = parameter.ParameterType;
        }

        /// <summary>
        /// Gets the constructor that declares the parameter.
        /// </summary>
        public ConstructorInfo Constructor { get; }

        /// <summary>
        /// Gets the constructor parameter name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the constructor parameter type.
        /// </summary>
        public Type ParameterType { get; }
    }
}
