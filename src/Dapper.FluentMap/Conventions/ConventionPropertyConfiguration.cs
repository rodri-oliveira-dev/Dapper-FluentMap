using System;
using System.ComponentModel;

namespace Dapper.FluentMap.Conventions
{
    /// <summary>
    /// Represents configuration of a property via conventions.
    /// </summary>
    public class ConventionPropertyConfiguration
    {
        /// <summary>
        /// Initializes a new instane of the <see cref="Dapper.FluentMap.Conventions.ConventionPropertyConfiguration"/>.
        /// </summary>
        public ConventionPropertyConfiguration()
        {
            CaseSensitive = true;
        }

        /// <summary>
        /// Configures the name of the database column used to store the property.
        /// </summary>
        /// <param name="columnName">The name of the database column.</param>
        /// <returns>The same instance of <see cref="T:Dapper.FluentMap.Conventions.ConventionPropertyConfiguration"/>.</returns>
        public ConventionPropertyConfiguration HasColumnName(string columnName)
        {
            if (string.IsNullOrEmpty(columnName))
            {
                throw new ArgumentException("Column name cannot be null or empty.", nameof(columnName));
            }

            ColumnName = columnName;
            return this;
        }

        /// <summary>
        /// Configures the prefix of the database column used to store the property.
        /// </summary>
        /// <param name="prefix">The prefix of the database column.</param>
        /// <returns>The same instance of <see cref="T:Dapper.FluentMap.Conventions.ConventionPropertyConfiguration"/>.</returns>
        public ConventionPropertyConfiguration HasPrefix(string prefix)
        {
            if (prefix == null)
            {
                throw new ArgumentNullException(nameof(prefix));
            }

            Prefix = prefix;
            return this;
        }

        /// <summary>
        /// Configures the current convention to be case insensitive.
        /// </summary>
        /// <returns>The same instance of <see cref="T:Dapper.FluentMap.Conventions.ConventionPropertyConfiguration"/>.</returns>
        public ConventionPropertyConfiguration IsCaseInsensitive()
        {
            CaseSensitive = false;
            return this;
        }

        /// <summary>
        /// Configures the function for transforming property names to database column names.
        /// </summary>
        /// <param name="transformer">A function which takes the property name and returns the database colum name.</param>
        /// <returns>The same instance of <see cref="T:Dapper.FluentMap.Conventions.ConventionPropertyConfiguration"/>.</returns>
        public ConventionPropertyConfiguration Transform(Func<string, string> transformer)
        {
            if (transformer == null)
            {
                throw new ArgumentNullException(nameof(transformer));
            }

            PropertyTransformer = transformer;
            return this;
        }

        internal string ColumnName { get; private set; }

        internal string Prefix { get; private set; }

        internal bool CaseSensitive { get; private set; }

        internal Func<string, string> PropertyTransformer { get; private set; }

        #region EditorBrowsableStates
        /// <inheritdoc/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string ToString()
        {
            return base.ToString();
        }

        /// <inheritdoc/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        /// <inheritdoc/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <inheritdoc/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new Type GetType()
        {
            return base.GetType();
        }
        #endregion
    }
}
