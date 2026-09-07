using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Dapper.FluentMap.Mapping;

namespace Dapper.FluentMap.Dommel.Mapping
{
    /// <summary>
    /// Represents mapping of a property for Dommel.
    /// </summary>
    public class DommelPropertyMap : PropertyMapBase<DommelPropertyMap>, IPropertyMap
    {
        private DatabaseGeneratedOption? _generatedOption;

        /// <summary>
        /// Initializes a new instance of the <see cref="DommelPropertyMap"/> class
        /// with the specified <see cref="PropertyInfo"/> object.
        /// </summary>
        /// <param name="info">The information about the property.</param>
        public DommelPropertyMap(PropertyInfo info) : base(info)
        {
        }

        /// <summary>
        /// Gets a value indicating whether this property is a primary key.
        /// </summary>
        public bool Key { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this primary key is an identity.
        /// </summary>
        public bool Identity { get; set; }

        /// <summary>
        /// Gets a value indicating how the column is generated.
        /// </summary>
        public DatabaseGeneratedOption? GeneratedOption
        {
            get => _generatedOption;
            set
            {
                _generatedOption = value;

                if (value.HasValue)
                {
                    ApplyGeneratedOption(value.Value);
                }
            }
        }

        internal DatabaseGeneratedOption EffectiveUpdateGeneratedOption
        {
            get
            {
                if (Persistence.IsIdentity)
                {
                    return DatabaseGeneratedOption.Identity;
                }

                if (!Persistence.ParticipatesInUpdate)
                {
                    return DatabaseGeneratedOption.Computed;
                }

                return DatabaseGeneratedOption.None;
            }
        }

        internal DatabaseGeneratedOption EffectiveKeyGeneratedOption
        {
            get
            {
                if (GeneratedOption.HasValue)
                {
                    return GeneratedOption.Value;
                }

                return Key ? DatabaseGeneratedOption.Identity : DatabaseGeneratedOption.None;
            }
        }

        /// <summary>
        /// Specifies the current property as key for the entity.
        /// </summary>
        /// <returns>The current instance of <see cref="DommelPropertyMap"/>.</returns>
        public DommelPropertyMap IsKey()
        {
            Key = true;
            MarkAsKey();
            return this;
        }

        /// <summary>
        /// Specifies the current property as an identity.
        /// </summary>
        /// <returns>The current instance of <see cref="DommelPropertyMap"/>.</returns>
        public DommelPropertyMap IsIdentity()
        {
            Identity = true;
            Key = true;
            MarkAsIdentity();
            return this;
        }

        /// <summary>
        /// Specifies how the property is generated.
        /// </summary>
        public DommelPropertyMap SetGeneratedOption(DatabaseGeneratedOption option)
        {
            GeneratedOption = option;
            return this;
        }

        private void ApplyGeneratedOption(DatabaseGeneratedOption option)
        {
            switch (option)
            {
                case DatabaseGeneratedOption.None:
                    Identity = false;
                    MarkAsNotGenerated();
                    break;
                case DatabaseGeneratedOption.Identity:
                    Identity = true;
                    Key = true;
                    MarkAsIdentity();
                    break;
                case DatabaseGeneratedOption.Computed:
                    MarkAsComputed();
                    break;
                default:
                    MarkAsNotGenerated();
                    break;
            }
        }
    }
}
