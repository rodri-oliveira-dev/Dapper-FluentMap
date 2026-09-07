using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Dapper.FluentMap.Compatibility;
using Dapper.FluentMap.Mapping;

namespace Dapper.FluentMap.Materialization
{
    internal sealed class NestedMaterializationPlan
    {
        private readonly MaterializationNode _rootNode;

        private NestedMaterializationPlan(MaterializationNode rootNode)
        {
            _rootNode = rootNode;
        }

        internal static NestedMaterializationPlan Create(Type entityType, Type profileType, IReadOnlyList<string> columnNames, MappingRegistry registry)
        {
            if (entityType == null)
            {
                throw new ArgumentNullException(nameof(entityType));
            }

            if (columnNames == null)
            {
                throw new ArgumentNullException(nameof(columnNames));
            }

            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            var defaultTypeMap = new DefaultTypeMap(entityType);
            var rootNode = MaterializationNode.Root(entityType);

            for (var i = 0; i < columnNames.Count; i++)
            {
                var columnName = columnNames[i];
                var fluentMap = registry.GetProfilePropertyMap(entityType, profileType, columnName);
                if (fluentMap != null)
                {
                    if (fluentMap.Ignored)
                    {
                        continue;
                    }

                    var memberPath = PropertyMapIdentity.GetMemberPath(fluentMap);
                    rootNode.AddPropertyPath(
                        memberPath,
                        i,
                        columnName,
                        entityType,
                        profileType,
                        PropertyMapConversion.GetConversion(fluentMap));
                    continue;
                }

                var defaultMember = defaultTypeMap.GetMember(columnName);
                if (defaultMember == null)
                {
                    continue;
                }

                if (defaultMember.Property != null)
                {
                    rootNode.AddRootProperty(
                        defaultMember.Property,
                        i,
                        columnName,
                        entityType,
                        profileType,
                        PropertyConversionMetadata.Default);
                }
                else if (defaultMember.Field != null)
                {
                    rootNode.AddRootField(
                        defaultMember.Field,
                        TryGetAutoPropertyForBackingField(entityType, defaultMember.Field),
                        i,
                        columnName,
                        entityType,
                        profileType);
                }
            }

            rootNode.Seal(entityType);

            return new NestedMaterializationPlan(rootNode);
        }

        internal object Materialize(IDataRecord record)
        {
            return _rootNode.MaterializeRoot(record);
        }

        private static Func<object> CreateParameterlessFactory(Type type)
        {
            var constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                return null;
            }

            var body = Expression.Convert(Expression.New(constructor), typeof(object));
            return Expression.Lambda<Func<object>>(body).Compile();
        }

        private static Func<object[], object> CreateConstructorFactory(ConstructorInfo constructor)
        {
            var args = Expression.Parameter(typeof(object[]), "args");
            var parameters = constructor.GetParameters();
            var arguments = new Expression[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                var item = Expression.ArrayIndex(args, Expression.Constant(i));
                arguments[i] = Expression.Convert(item, parameters[i].ParameterType);
            }

            var body = Expression.Convert(Expression.New(constructor, arguments), typeof(object));
            return Expression.Lambda<Func<object[], object>>(body, args).Compile();
        }

        private static Func<object, object> CreateGetter(PropertyInfo property)
        {
            var target = Expression.Parameter(typeof(object), "target");
            var body = Expression.Convert(
                Expression.Property(Expression.Convert(target, property.DeclaringType), property),
                typeof(object));

            return Expression.Lambda<Func<object, object>>(body, target).Compile();
        }

        private static Action<object, object> CreatePropertySetter(PropertyInfo property)
        {
            if (!CanWrite(property))
            {
                return null;
            }

            var target = Expression.Parameter(typeof(object), "target");
            var value = Expression.Parameter(typeof(object), "value");
            var body = Expression.Assign(
                Expression.Property(Expression.Convert(target, property.DeclaringType), property),
                Expression.Convert(value, property.PropertyType));

            return Expression.Lambda<Action<object, object>>(body, target, value).Compile();
        }

        private static Action<object, object> CreateFieldSetter(FieldInfo field)
        {
            if (field.IsInitOnly || field.IsStatic)
            {
                return null;
            }

            var target = Expression.Parameter(typeof(object), "target");
            var value = Expression.Parameter(typeof(object), "value");
            var body = Expression.Assign(
                Expression.Field(Expression.Convert(target, field.DeclaringType), field),
                Expression.Convert(value, field.FieldType));

            return Expression.Lambda<Action<object, object>>(body, target, value).Compile();
        }

        private static PropertyInfo TryGetAutoPropertyForBackingField(Type entityType, FieldInfo field)
        {
            if (field == null || !field.IsInitOnly || !IsAutoPropertyBackingFieldName(field.Name, out var propertyName))
            {
                return null;
            }

            return entityType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(property =>
                    string.Equals(property.Name, propertyName, StringComparison.Ordinal) &&
                    Equals(property.DeclaringType, field.DeclaringType) &&
                    property.PropertyType == field.FieldType);
        }

        private static bool IsAutoPropertyBackingFieldName(string fieldName, out string propertyName)
        {
            propertyName = null;

            if (fieldName == null ||
                fieldName.Length <= 17 ||
                fieldName[0] != '<' ||
                !fieldName.EndsWith(">k__BackingField", StringComparison.Ordinal))
            {
                return false;
            }

            propertyName = fieldName.Substring(1, fieldName.Length - 17);
            return propertyName.Length > 0;
        }

        private static Func<object, object> CreateConverter(
            Type entityType,
            Type profileType,
            Type targetType,
            string memberPath,
            string columnName,
            PropertyConversionMetadata conversion)
        {
            if (conversion != null && conversion.HasReadConverter)
            {
                return CreatePropertyReadConverter(
                    entityType,
                    profileType,
                    targetType,
                    memberPath,
                    columnName,
                    conversion.ReadConverter);
            }

            return CreateDefaultConverter(targetType);
        }

        private static Func<object, object> CreateDefaultConverter(Type targetType)
        {
            var conversionType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (DapperTypeHandlerAdapter.HasTypeHandler(conversionType))
            {
                return DapperTypeHandlerAdapter.CreateConverter(targetType);
            }

            return value => ConvertValue(value, targetType);
        }

        private static Func<object, object> CreatePropertyReadConverter(
            Type entityType,
            Type profileType,
            Type targetType,
            string memberPath,
            string columnName,
            PropertyConverterMetadata converter)
        {
            var invokeConverter = CreateReadConverterInvoker(converter);

            return value =>
            {
                if (value == null || value == DBNull.Value)
                {
                    return GetDefaultValue(targetType);
                }

                try
                {
                    var converterInput = ConvertConverterInput(value, converter.DatabaseType);
                    var converted = invokeConverter(converterInput);
                    if (converted == null && !CanAssignNull(targetType))
                    {
                        throw new InvalidOperationException(
                            $"Read converter '{converter.ConverterType.FullName}' returned null for non-nullable target type '{targetType.FullName}'.");
                    }

                    return converted;
                }
                catch (Exception exception) when (!(exception is FluentMapConfigurationException))
                {
                    throw CreateReadConverterException(
                        entityType,
                        profileType,
                        memberPath,
                        columnName,
                        converter,
                        value,
                        targetType,
                        exception);
                }
            };
        }

        private static Func<object, object> CreateReadConverterInvoker(PropertyConverterMetadata converter)
        {
            var value = Expression.Parameter(typeof(object), "value");
            var converterValue = Expression.Constant(converter.Converter);
            var input = Expression.Convert(value, converter.DatabaseType);
            Expression call;

            if (converter.Converter is Delegate)
            {
                var delegateType = typeof(ReadPropertyConverter<,>).MakeGenericType(
                    converter.DatabaseType,
                    converter.PropertyType);
                call = Expression.Invoke(Expression.Convert(converterValue, delegateType), input);
            }
            else
            {
                var interfaceType = typeof(IReadPropertyConverter<,>).MakeGenericType(
                    converter.DatabaseType,
                    converter.PropertyType);
                call = Expression.Call(
                    Expression.Convert(converterValue, interfaceType),
                    interfaceType.GetMethod(nameof(IReadPropertyConverter<object, object>.ConvertFromDatabase)),
                    input);
            }

            return Expression.Lambda<Func<object, object>>(
                Expression.Convert(call, typeof(object)),
                value).Compile();
        }

        private static object ConvertConverterInput(object value, Type databaseType)
        {
            var conversionType = Nullable.GetUnderlyingType(databaseType) ?? databaseType;
            if (conversionType == typeof(object) || conversionType.IsInstanceOfType(value))
            {
                return value;
            }

            return ConvertValue(value, databaseType);
        }

        private static FluentMapConfigurationException CreateReadConverterException(
            Type entityType,
            Type profileType,
            string memberPath,
            string columnName,
            PropertyConverterMetadata converter,
            object sourceValue,
            Type targetType,
            Exception innerException)
        {
            var profileContext = profileType == null
                ? string.Empty
                : $" Profile: '{FormatType(profileType)}'.";
            var sourceType = sourceValue == null || sourceValue == DBNull.Value
                ? null
                : sourceValue.GetType();

            return new FluentMapConfigurationException(
                $"Read converter failed for entity '{FormatType(entityType)}'.{profileContext} Member path: '{memberPath}'. Column: '{columnName}'. Converter: '{FormatType(converter.ConverterType)}'. Source type: '{FormatType(sourceType)}'. Converter database type: '{FormatType(converter.DatabaseType)}'. Converter property type: '{FormatType(converter.PropertyType)}'. Target type: '{FormatType(targetType)}'. See the inner exception for the converter failure.",
                innerException);
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null || value == DBNull.Value)
            {
                return GetDefaultValue(targetType);
            }

            var conversionType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (conversionType.IsInstanceOfType(value))
            {
                return value;
            }

            if (conversionType.GetTypeInfo().IsEnum)
            {
                return value is string text
                    ? Enum.Parse(conversionType, text)
                    : Enum.ToObject(conversionType, value);
            }

            if (conversionType == typeof(Guid) && value is string guidText)
            {
                return new Guid(guidText);
            }

            return Convert.ChangeType(value, conversionType, CultureInfo.InvariantCulture);
        }

        private static object GetDefaultValue(Type type)
        {
            if (!type.GetTypeInfo().IsValueType || Nullable.GetUnderlyingType(type) != null)
            {
                return null;
            }

            return Activator.CreateInstance(type);
        }

        private static bool CanAssignNull(Type type)
        {
            return !type.GetTypeInfo().IsValueType || Nullable.GetUnderlyingType(type) != null;
        }

        private static bool CanRead(PropertyInfo property)
        {
            var getter = property.GetGetMethod();
            return getter != null && !getter.IsStatic;
        }

        private static bool CanWrite(PropertyInfo property)
        {
            var setter = property.GetSetMethod();
            return setter != null && !setter.IsStatic;
        }

        private static bool HasNonNullValue(IDataRecord record, IEnumerable<int> columnIndexes)
        {
            return columnIndexes.Any(index => !record.IsDBNull(index));
        }

        private static bool IsParameterCompatible(Type parameterType, Type sourceType)
        {
            var parameter = Nullable.GetUnderlyingType(parameterType) ?? parameterType;
            var source = Nullable.GetUnderlyingType(sourceType) ?? sourceType;
            return parameter.GetTypeInfo().IsAssignableFrom(source.GetTypeInfo()) ||
                   source.GetTypeInfo().IsAssignableFrom(parameter.GetTypeInfo());
        }

        private static int GetCompatibilityScore(Type parameterType, Type sourceType)
        {
            var parameter = Nullable.GetUnderlyingType(parameterType) ?? parameterType;
            var source = Nullable.GetUnderlyingType(sourceType) ?? sourceType;
            return parameter == source ? 2 : 1;
        }

        private static string FormatType(Type type)
        {
            return type == null ? "<unknown>" : type.FullName;
        }

        private static string FormatConstructor(ConstructorInfo constructor)
        {
            var parameters = constructor.GetParameters()
                .Select(parameter => FormatType(parameter.ParameterType) + " " + parameter.Name);

            return FormatType(constructor.DeclaringType) + "(" + string.Join(", ", parameters) + ")";
        }

        private sealed class MaterializationNode
        {
            private readonly List<NestedLeaf> _leaves = new List<NestedLeaf>();
            private readonly List<MaterializationNode> _children = new List<MaterializationNode>();
            private readonly bool _isRoot;
            private int[] _subtreeColumnIndexes;
            private Func<object> _parameterlessFactory;
            private ConstructorPlan _constructorPlan;
            private NestedLeaf[] _postConstructorLeaves;
            private MaterializationNode[] _postConstructorChildren;

            private MaterializationNode(Type type, PropertyInfo parentProperty, string memberPath, bool isRoot)
            {
                Type = type;
                ParentProperty = parentProperty;
                MemberPath = memberPath;
                _isRoot = isRoot;

                if (parentProperty != null)
                {
                    Getter = CanRead(parentProperty) ? CreateGetter(parentProperty) : null;
                    Setter = CreatePropertySetter(parentProperty);
                }
            }

            internal Type Type { get; }

            internal PropertyInfo ParentProperty { get; }

            internal string MemberPath { get; }

            internal Func<object, object> Getter { get; }

            internal Action<object, object> Setter { get; }

            internal bool CanAssignToParent => _isRoot || Setter != null;

            internal static MaterializationNode Root(Type type)
            {
                return new MaterializationNode(type, null, type.Name, isRoot: true);
            }

            internal void AddPropertyPath(
                MemberPath memberPath,
                int columnIndex,
                string columnName,
                Type entityType,
                Type profileType,
                PropertyConversionMetadata conversion)
            {
                var properties = memberPath.Properties;
                if (!memberPath.IsNested)
                {
                    AddRootProperty(properties[0], columnIndex, columnName, entityType, profileType, conversion);
                    return;
                }

                var node = this;
                for (var i = 0; i < properties.Count - 1; i++)
                {
                    node = node.FindOrAddChild(properties[i]);
                }

                node._leaves.Add(NestedLeaf.ForProperty(
                    properties[properties.Count - 1],
                    columnIndex,
                    columnName,
                    memberPath.ToString(),
                    entityType,
                    profileType,
                    conversion));
            }

            internal void AddRootProperty(
                PropertyInfo property,
                int columnIndex,
                string columnName,
                Type entityType,
                Type profileType,
                PropertyConversionMetadata conversion)
            {
                _leaves.Add(NestedLeaf.ForProperty(
                    property,
                    columnIndex,
                    columnName,
                    property.Name,
                    entityType,
                    profileType,
                    conversion));
            }

            internal void AddRootField(
                FieldInfo field,
                PropertyInfo constructorProperty,
                int columnIndex,
                string columnName,
                Type entityType,
                Type profileType)
            {
                _leaves.Add(NestedLeaf.ForField(field, constructorProperty, columnIndex, columnName, entityType, profileType));
            }

            internal void Seal(Type entityType)
            {
                foreach (var child in _children)
                {
                    child.Seal(entityType);
                }

                _subtreeColumnIndexes = _leaves
                    .Select(leaf => leaf.ColumnIndex)
                    .Concat(_children.SelectMany(child => child._subtreeColumnIndexes))
                    .Distinct()
                    .ToArray();

                SelectConstructionPlan(entityType);
            }

            internal object MaterializeRoot(IDataRecord record)
            {
                return Materialize(record, existing: null, forceNew: true);
            }

            internal object MaterializeValue(IDataRecord record)
            {
                if (!HasNonNullValue(record, _subtreeColumnIndexes))
                {
                    return null;
                }

                return Materialize(record, existing: null, forceNew: true);
            }

            internal void Apply(object parent, IDataRecord record)
            {
                if (!HasNonNullValue(record, _subtreeColumnIndexes))
                {
                    if (Setter != null && CanAssignNull(ParentProperty.PropertyType))
                    {
                        Setter(parent, null);
                    }

                    return;
                }

                var existing = _constructorPlan == null && Getter != null
                    ? Getter(parent)
                    : null;
                var value = Materialize(record, existing, forceNew: false);

                if (Setter != null && (!ReferenceEquals(existing, value) || _constructorPlan != null))
                {
                    Setter(parent, value);
                }
            }

            private MaterializationNode FindOrAddChild(PropertyInfo property)
            {
                var child = _children.FirstOrDefault(node => Equals(node.ParentProperty, property));
                if (child != null)
                {
                    return child;
                }

                var memberPath = _isRoot
                    ? property.Name
                    : MemberPath + "." + property.Name;
                child = new MaterializationNode(property.PropertyType, property, memberPath, isRoot: false);
                _children.Add(child);
                return child;
            }

            private void SelectConstructionPlan(Type entityType)
            {
                _parameterlessFactory = CreateParameterlessFactory(Type);

                var requiresConstructor = _parameterlessFactory == null ||
                                          _leaves.Any(leaf => !leaf.CanAssign) ||
                                          _children.Any(child => !child.CanAssignToParent);

                if (!requiresConstructor)
                {
                    _postConstructorLeaves = _leaves.ToArray();
                    _postConstructorChildren = _children.ToArray();
                    return;
                }

                _constructorPlan = SelectConstructor(entityType);
                if (_constructorPlan == null)
                {
                    throw new FluentMapConfigurationException(
                        $"Type '{FormatType(Type)}' at member path '{MemberPath}' on entity '{FormatType(entityType)}' cannot be materialized. No public constructor matches the mapped properties or nested value objects. Columns: {FormatColumns()}.");
                }

                _postConstructorLeaves = _leaves
                    .Where(leaf => !_constructorPlan.Uses(leaf))
                    .ToArray();
                _postConstructorChildren = _children
                    .Where(child => !_constructorPlan.Uses(child))
                    .ToArray();

                var unsupportedLeaf = _postConstructorLeaves.FirstOrDefault(leaf => !leaf.CanAssign);
                if (unsupportedLeaf != null)
                {
                    throw new FluentMapConfigurationException(
                        $"Type '{FormatType(Type)}' at member path '{MemberPath}' on entity '{FormatType(entityType)}' cannot assign mapped property '{unsupportedLeaf.MemberPath}'. It has no public setter and is not bound to constructor '{FormatConstructor(_constructorPlan.Constructor)}'. Column: '{unsupportedLeaf.ColumnName}'.");
                }

                var unsupportedChild = _postConstructorChildren.FirstOrDefault(child => !child.CanAssignToParent);
                if (unsupportedChild != null)
                {
                    throw new FluentMapConfigurationException(
                        $"Type '{FormatType(Type)}' at member path '{MemberPath}' on entity '{FormatType(entityType)}' cannot assign nested value object '{unsupportedChild.MemberPath}'. It has no public setter and is not bound to constructor '{FormatConstructor(_constructorPlan.Constructor)}'.");
                }
            }

            private ConstructorPlan SelectConstructor(Type entityType)
            {
                var candidates = Type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                    .Select(constructor => TryCreateConstructorPlan(entityType, constructor))
                    .Where(plan => plan != null)
                    .ToList();

                if (candidates.Count == 0)
                {
                    return null;
                }

                var bestScore = candidates.Max(candidate => candidate.Score);
                var best = candidates
                    .Where(candidate => candidate.Score == bestScore)
                    .ToList();

                if (best.Count > 1)
                {
                    throw new FluentMapConfigurationException(
                        $"Type '{FormatType(Type)}' at member path '{MemberPath}' on entity '{FormatType(entityType)}' has multiple public constructors that match the mapped columns: {string.Join("; ", best.Select(plan => FormatConstructor(plan.Constructor)))}.");
                }

                return best[0];
            }

            private ConstructorPlan TryCreateConstructorPlan(Type entityType, ConstructorInfo constructor)
            {
                var bindings = new List<ParameterBinding>();
                var score = 0;

                foreach (var parameter in constructor.GetParameters())
                {
                    var binding = TryBindParameter(parameter);
                    if (binding == null)
                    {
                        return null;
                    }

                    bindings.Add(binding);
                    score += binding.Score;
                }

                foreach (var leaf in _leaves.Where(leaf => !leaf.CanAssign))
                {
                    if (!bindings.Any(binding => binding.Leaf == leaf))
                    {
                        return null;
                    }
                }

                foreach (var child in _children.Where(child => !child.CanAssignToParent))
                {
                    if (!bindings.Any(binding => binding.Child == child))
                    {
                        return null;
                    }
                }

                return new ConstructorPlan(
                    entityType,
                    MemberPath,
                    constructor,
                    CreateConstructorFactory(constructor),
                    bindings,
                    score);
            }

            private ParameterBinding TryBindParameter(ParameterInfo parameter)
            {
                var leafMatches = _leaves
                    .Where(leaf => leaf.ConstructorProperty != null &&
                                   string.Equals(leaf.ConstructorProperty.Name, parameter.Name, StringComparison.OrdinalIgnoreCase) &&
                                   IsParameterCompatible(parameter.ParameterType, leaf.TargetType))
                    .Select(leaf => ParameterBinding.ForLeaf(parameter, leaf, GetCompatibilityScore(parameter.ParameterType, leaf.TargetType)));

                var childMatches = _children
                    .Where(child => string.Equals(child.ParentProperty.Name, parameter.Name, StringComparison.OrdinalIgnoreCase) &&
                                    IsParameterCompatible(parameter.ParameterType, child.ParentProperty.PropertyType))
                    .Select(child => ParameterBinding.ForChild(parameter, child, GetCompatibilityScore(parameter.ParameterType, child.ParentProperty.PropertyType)));

                var matches = leafMatches
                    .Concat(childMatches)
                    .OrderByDescending(binding => binding.Score)
                    .ToList();

                if (matches.Count == 0)
                {
                    return null;
                }

                var bestScore = matches[0].Score;
                var best = matches.Where(match => match.Score == bestScore).ToList();
                return best.Count == 1 ? best[0] : null;
            }

            private object Materialize(IDataRecord record, object existing, bool forceNew)
            {
                var current = _constructorPlan != null
                    ? _constructorPlan.Create(record)
                    : forceNew || existing == null
                        ? _parameterlessFactory()
                        : existing;

                foreach (var child in _postConstructorChildren)
                {
                    child.Apply(current, record);
                }

                foreach (var leaf in _postConstructorLeaves)
                {
                    leaf.Assign(current, record);
                }

                return current;
            }

            private string FormatColumns()
            {
                return string.Join(", ", _leaves.Select(leaf => "'" + leaf.ColumnName + "'")
                    .Concat(_children.SelectMany(child => child.GetColumnNames().Select(column => "'" + column + "'"))));
            }

            internal IEnumerable<string> GetColumnNames()
            {
                return _leaves.Select(leaf => leaf.ColumnName)
                    .Concat(_children.SelectMany(child => child.GetColumnNames()));
            }
        }

        private sealed class NestedLeaf
        {
            private readonly Action<object, object> _setter;
            private readonly Func<object, object> _converter;

            private NestedLeaf(
                PropertyInfo property,
                PropertyInfo constructorProperty,
                FieldInfo field,
                int columnIndex,
                string columnName,
                string memberPath,
                Type targetType,
                Action<object, object> setter,
                Type entityType,
                Type profileType,
                PropertyConversionMetadata conversion)
            {
                Property = property;
                ConstructorProperty = constructorProperty;
                Field = field;
                ColumnIndex = columnIndex;
                ColumnName = columnName;
                MemberPath = memberPath;
                TargetType = targetType;
                _setter = setter;
                _converter = CreateConverter(entityType, profileType, targetType, memberPath, columnName, conversion);
            }

            internal PropertyInfo Property { get; }

            internal PropertyInfo ConstructorProperty { get; }

            internal FieldInfo Field { get; }

            internal int ColumnIndex { get; }

            internal string ColumnName { get; }

            internal string MemberPath { get; }

            internal Type TargetType { get; }

            internal bool CanAssign => _setter != null;

            internal static NestedLeaf ForProperty(
                PropertyInfo property,
                int columnIndex,
                string columnName,
                string memberPath,
                Type entityType,
                Type profileType,
                PropertyConversionMetadata conversion)
            {
                return new NestedLeaf(
                    property,
                    property,
                    null,
                    columnIndex,
                    columnName,
                    memberPath,
                    property.PropertyType,
                    CreatePropertySetter(property),
                    entityType,
                    profileType,
                    conversion ?? PropertyConversionMetadata.Default);
            }

            internal static NestedLeaf ForField(
                FieldInfo field,
                PropertyInfo constructorProperty,
                int columnIndex,
                string columnName,
                Type entityType,
                Type profileType)
            {
                return new NestedLeaf(
                    null,
                    constructorProperty,
                    field,
                    columnIndex,
                    columnName,
                    constructorProperty == null ? field.Name : constructorProperty.Name,
                    constructorProperty == null ? field.FieldType : constructorProperty.PropertyType,
                    CreateFieldSetter(field),
                    entityType,
                    profileType,
                    PropertyConversionMetadata.Default);
            }

            internal object GetValue(IDataRecord record)
            {
                return _converter(record.GetValue(ColumnIndex));
            }

            internal void Assign(object target, IDataRecord record)
            {
                _setter(target, GetValue(record));
            }
        }

        private sealed class ConstructorPlan
        {
            private readonly Type _entityType;
            private readonly string _memberPath;
            private readonly Func<object[], object> _factory;
            private readonly ParameterBinding[] _bindings;

            internal ConstructorPlan(
                Type entityType,
                string memberPath,
                ConstructorInfo constructor,
                Func<object[], object> factory,
                IEnumerable<ParameterBinding> bindings,
                int score)
            {
                _entityType = entityType;
                _memberPath = memberPath;
                Constructor = constructor;
                _factory = factory;
                _bindings = bindings.ToArray();
                Score = score;
            }

            internal ConstructorInfo Constructor { get; }

            internal int Score { get; }

            internal bool Uses(NestedLeaf leaf)
            {
                return _bindings.Any(binding => binding.Leaf == leaf);
            }

            internal bool Uses(MaterializationNode child)
            {
                return _bindings.Any(binding => binding.Child == child);
            }

            internal object Create(IDataRecord record)
            {
                var args = new object[_bindings.Length];
                for (var i = 0; i < _bindings.Length; i++)
                {
                    args[i] = _bindings[i].GetValue(record);
                }

                try
                {
                    return _factory(args);
                }
                catch (Exception exception)
                {
                    throw new FluentMapConfigurationException(
                        $"Failed to materialize type '{FormatType(Constructor.DeclaringType)}' at member path '{_memberPath}' on entity '{FormatType(_entityType)}' using constructor '{FormatConstructor(Constructor)}'. Columns: {FormatColumns()}. See the inner exception for the domain failure.",
                        exception);
                }
            }

            private string FormatColumns()
            {
                return string.Join(", ", _bindings
                    .SelectMany(binding => binding.GetColumnNames())
                    .Distinct()
                    .Select(column => "'" + column + "'"));
            }
        }

        private sealed class ParameterBinding
        {
            private ParameterBinding(ParameterInfo parameter, NestedLeaf leaf, MaterializationNode child, int score)
            {
                Parameter = parameter;
                Leaf = leaf;
                Child = child;
                Score = score;
            }

            internal ParameterInfo Parameter { get; }

            internal NestedLeaf Leaf { get; }

            internal MaterializationNode Child { get; }

            internal int Score { get; }

            internal static ParameterBinding ForLeaf(ParameterInfo parameter, NestedLeaf leaf, int score)
            {
                return new ParameterBinding(parameter, leaf, null, score);
            }

            internal static ParameterBinding ForChild(ParameterInfo parameter, MaterializationNode child, int score)
            {
                return new ParameterBinding(parameter, null, child, score);
            }

            internal object GetValue(IDataRecord record)
            {
                if (Leaf != null)
                {
                    return Leaf.GetValue(record);
                }

                return Child.MaterializeValue(record);
            }

            internal IEnumerable<string> GetColumnNames()
            {
                if (Leaf != null)
                {
                    yield return Leaf.ColumnName;
                    yield break;
                }

                foreach (var columnName in Child.GetColumnNames())
                {
                    yield return columnName;
                }
            }
        }
    }
}
