using System;
using System.Collections.Generic;
using System.Data;

namespace Dapper.FluentMap
{
    /// <summary>
    /// Provides explicitly described command parameters for strict generated queries without
    /// discovering members from an arbitrary parameter object.
    /// </summary>
    /// <remarks>
    /// Each parameter requires an explicit <see cref="DbType"/>. This keeps parameter binding
    /// deterministic and suitable for the repository's supported Native AOT strict-generated path.
    /// </remarks>
    public sealed class GeneratedParameters
    {
        private readonly List<ParameterValue> _parameters = new List<ParameterValue>();
        private readonly HashSet<string> _names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Adds one input parameter.
        /// </summary>
        /// <param name="name">The parameter name, with or without a common provider prefix.</param>
        /// <param name="value">The provider parameter value. <see langword="null"/> is sent as <see cref="DBNull.Value"/>.</param>
        /// <param name="dbType">The database type used when binding the parameter.</param>
        /// <param name="size">The optional provider parameter size.</param>
        /// <returns>This parameter collection.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or duplicated, or <paramref name="size"/> is negative.</exception>
        public GeneratedParameters Add(string name, object value, DbType dbType, int? size = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Parameter name cannot be null, empty or whitespace.", nameof(name));
            }

            var normalizedName = NormalizeName(name);
            if (normalizedName.Length == 0)
            {
                throw new ArgumentException("Parameter name must contain a name after its provider prefix.", nameof(name));
            }

            if (size < 0)
            {
                throw new ArgumentException("Parameter size cannot be negative.", nameof(size));
            }

            if (!_names.Add(normalizedName))
            {
                throw new ArgumentException($"Parameter '{name}' is already registered.", nameof(name));
            }

            _parameters.Add(new ParameterValue(normalizedName, value, dbType, size));
            return this;
        }

        internal void AddParameters(IDbCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            foreach (var configured in _parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = configured.Name;
                parameter.Value = configured.Value ?? DBNull.Value;
                parameter.DbType = configured.DbType;
                parameter.Direction = ParameterDirection.Input;
                if (configured.Size.HasValue)
                {
                    parameter.Size = configured.Size.Value;
                }

                command.Parameters.Add(parameter);
            }
        }

        private static string NormalizeName(string name)
        {
            var trimmed = name.Trim();
            if (trimmed.Length > 0 && (trimmed[0] == '@' || trimmed[0] == ':' || trimmed[0] == '?'))
            {
                return trimmed.Substring(1);
            }

            return trimmed;
        }

        private sealed class ParameterValue
        {
            internal ParameterValue(string name, object value, DbType dbType, int? size)
            {
                Name = name;
                Value = value;
                DbType = dbType;
                Size = size;
            }

            internal string Name { get; }

            internal object Value { get; }

            internal DbType DbType { get; }

            internal int? Size { get; }
        }
    }
}
