using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace Dapper.FluentMap.Conventions
{
    /// <summary>
    /// Represents the configuration for a convention.
    /// </summary>
    public class PropertyConventionConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyConventionConfiguration"/> class,
        /// allowing configuration for a convention.
        /// </summary>
        public PropertyConventionConfiguration()
        {
            PropertyPredicates = new List<Func<PropertyInfo, bool>>();
        }

        internal IList<Func<PropertyInfo, bool>> PropertyPredicates { get; }

        internal ConventionPropertyConfiguration PropertyConfiguration { get; private set; }

        /// <summary>
        /// Filters the properties that this convention applies to based on a predicate.
        /// </summary>
        /// <param name="predicate">A function to test each property for a condition.</param>
        /// <returns>The same instance of <see cref="T:Dapper.FluentMap.Conventions.PropertyConventionConfiguration"/>.</returns>
        public PropertyConventionConfiguration Where(Func<PropertyInfo, bool> predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            PropertyPredicates.Add(predicate);
            return this;
        }

        /// <summary>
        /// Configures the properties that this convention applies to.
        /// </summary>
        /// <param name="configure">
        /// An action that performs configuration against
        /// <see cref="T:Dapper.FluentMap.Conventions.ConventionPropertyConfiguration"/>.
        /// </param>
        public void Configure(Action<ConventionPropertyConfiguration> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var config = new ConventionPropertyConfiguration();
            PropertyConfiguration = config;
            configure(config);
        }

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
