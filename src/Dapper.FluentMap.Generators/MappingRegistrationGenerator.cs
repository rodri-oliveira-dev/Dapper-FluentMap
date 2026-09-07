using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Dapper.FluentMap.Generators
{
    [Generator(LanguageNames.CSharp)]
    public sealed class MappingRegistrationGenerator : IIncrementalGenerator
    {
        public const string InvalidGenericMapRegistrationDiagnosticId = "DFM005";
        public const string SkippedGeneratedMapDiagnosticId = "DFM006";
        public const string DuplicateGeneratedEntityMapDiagnosticId = "DFM007";
        public const string DuplicateGeneratedProfileMapDiagnosticId = "DFM008";
        public const string SkippedGeneratedMaterializerDiagnosticId = "DFM011";
        public const string InvalidGeneratedReadConverterDiagnosticId = "DFM012";

        private const string Category = "Dapper.FluentMap.Configuration";
        private const string MappingNamespace = "Dapper.FluentMap.Mapping";
        private const string GeneratedCodeHintName = "DapperFluentMapGeneratedRegistration.g.cs";

        private static readonly DiagnosticDescriptor InvalidGenericMapRegistrationRule = new DiagnosticDescriptor(
            InvalidGenericMapRegistrationDiagnosticId,
            "Generic map registration type is invalid",
            "Entity map type '{0}' must implement exactly one closed IEntityMap<TEntity> interface targeting a class type",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Generated registration can only register map types that implement exactly one closed IEntityMap<TEntity> interface whose entity type is a class.");

        private static readonly DiagnosticDescriptor SkippedGeneratedMapRule = new DiagnosticDescriptor(
            SkippedGeneratedMapDiagnosticId,
            "Entity map type is skipped by generated registration",
            "Entity map type '{0}' is not included in generated registration: {1}",
            Category,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "Only concrete, closed and accessible entity map types with a public parameterless constructor can be included in generated registration.");

        private static readonly DiagnosticDescriptor DuplicateGeneratedEntityMapRule = new DiagnosticDescriptor(
            DuplicateGeneratedEntityMapDiagnosticId,
            "Multiple generated entity maps target the same entity",
            "Entity '{0}' has multiple generated entity maps: '{1}' and '{2}'",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Generated registration must not register more than one entity map for the same entity.");

        private static readonly DiagnosticDescriptor DuplicateGeneratedProfileMapRule = new DiagnosticDescriptor(
            DuplicateGeneratedProfileMapDiagnosticId,
            "Multiple generated profile maps target the same entity and profile",
            "Entity '{0}' has multiple generated maps for profile '{1}': '{2}' and '{3}'",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Generated registration must not register more than one map for the same entity and mapping profile.");

        private static readonly DiagnosticDescriptor SkippedGeneratedMaterializerRule = new DiagnosticDescriptor(
            SkippedGeneratedMaterializerDiagnosticId,
            "Generated materializer fallback will be used",
            "Entity map type '{0}' is registered, but no generated materializer was emitted: {1}",
            Category,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "Generated materializers are emitted only for statically known explicit mappings with supported object construction. Unsupported mappings continue to use the runtime fallback.");

        private static readonly DiagnosticDescriptor InvalidGeneratedReadConverterRule = new DiagnosticDescriptor(
            InvalidGeneratedReadConverterDiagnosticId,
            "Generated read converter is invalid",
            "Read converter '{1}' on entity map type '{0}' cannot be emitted by the generated materializer: {2}",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Generated materializers require statically known read converters to implement a compatible IReadPropertyConverter<TDatabase, TProperty> contract.");

        private static readonly SymbolDisplayFormat FullyQualifiedTypeFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                                  SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var mapCandidates = context.SyntaxProvider
                .CreateSyntaxProvider(
                    (node, _) => IsCandidateClassDeclaration(node),
                    (syntaxContext, cancellationToken) => CreateMapCandidate(syntaxContext, cancellationToken))
                .Where(candidate => candidate != null)
                .Collect();

            context.RegisterSourceOutput(
                mapCandidates,
                (sourceProductionContext, candidates) => Execute(sourceProductionContext, candidates));
        }

        private static bool IsCandidateClassDeclaration(SyntaxNode node)
        {
            var classDeclaration = node as ClassDeclarationSyntax;
            return classDeclaration?.BaseList != null;
        }

        private static MapCandidate CreateMapCandidate(
            GeneratorSyntaxContext context,
            System.Threading.CancellationToken cancellationToken)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;
            var mapType = context.SemanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken);
            if (mapType == null)
            {
                return null;
            }

            var entityMapInterfaces = mapType.AllInterfaces
                .Where(type => IsEntityMapInterface(type))
                .ToList();
            var profileMapInterfaces = mapType.AllInterfaces
                .Where(type => IsProfileMapInterface(type))
                .ToList();

            if (entityMapInterfaces.Count == 0)
            {
                return null;
            }

            var location = classDeclaration.Identifier.GetLocation();
            var mapDisplayName = FormatSymbol(mapType);
            var mapTypeName = mapType.ToDisplayString(FullyQualifiedTypeFormat);

            if (entityMapInterfaces.Count != 1 ||
                entityMapInterfaces[0].TypeArguments[0].TypeKind != TypeKind.Class ||
                profileMapInterfaces.Count > 1)
            {
                return MapCandidate.InvalidRegistration(mapDisplayName, location);
            }

            var entityType = (INamedTypeSymbol)entityMapInterfaces[0].TypeArguments[0];
            var profileTypeName = profileMapInterfaces.Count == 0
                ? null
                : profileMapInterfaces[0].TypeArguments[0].ToDisplayString(FullyQualifiedTypeFormat);
            if (mapType.IsAbstract)
            {
                return MapCandidate.Skipped(mapDisplayName, location, "the map type is abstract");
            }

            if (mapType.TypeParameters.Length != 0 || ContainsGenericParameters(mapType))
            {
                return MapCandidate.Skipped(mapDisplayName, location, "the map type is an open generic type");
            }

            if (!IsAccessibleFromGeneratedCode(mapType))
            {
                return MapCandidate.Skipped(mapDisplayName, location, "the map type is not accessible from generated code");
            }

            if (!HasPublicParameterlessConstructor(mapType))
            {
                return MapCandidate.Skipped(mapDisplayName, location, "the map type does not have a public parameterless constructor");
            }

            var materializer = TryCreateGeneratedMaterializer(
                classDeclaration,
                mapType,
                entityType,
                profileTypeName,
                context.SemanticModel,
                cancellationToken,
                out var materializerSkipReason,
                out var materializerDiagnostic);

            return MapCandidate.Valid(
                mapDisplayName,
                mapTypeName,
                entityType.ToDisplayString(FullyQualifiedTypeFormat),
                profileTypeName,
                GetInheritanceDepth(entityType),
                location,
                materializer,
                materializerSkipReason,
                materializerDiagnostic);
        }

        private static void Execute(
            SourceProductionContext context,
            ImmutableArray<MapCandidate> candidates)
        {
            var distinctCandidates = candidates
                .GroupBy(candidate => candidate.MapDisplayName, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();

            foreach (var candidate in distinctCandidates)
            {
                ReportCandidateDiagnostic(context, candidate);
            }

            var validMaps = distinctCandidates
                .Where(candidate => candidate.Kind == MapCandidateKind.Valid)
                .OrderBy(candidate => candidate.EntityInheritanceDepth)
                .ThenBy(candidate => candidate.EntityTypeName, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.MapTypeName, StringComparer.Ordinal)
                .ToList();

            var duplicateEntityTypeNames = ReportDuplicateEntityMaps(context, validMaps);
            var duplicateProfileKeys = ReportDuplicateProfileMaps(context, validMaps);
            var generatedMaps = validMaps
                .Where(candidate =>
                    candidate.ProfileTypeName == null
                        ? !duplicateEntityTypeNames.Contains(candidate.EntityTypeName)
                        : !duplicateProfileKeys.Contains(candidate.ProfileKey))
                .ToList();

            context.AddSource(GeneratedCodeHintName, SourceText.From(CreateGeneratedSource(generatedMaps), Encoding.UTF8));
        }

        private static void ReportCandidateDiagnostic(SourceProductionContext context, MapCandidate candidate)
        {
            if (candidate.Kind == MapCandidateKind.InvalidRegistration)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidGenericMapRegistrationRule,
                    candidate.Location,
                    candidate.MapDisplayName));
                return;
            }

            if (candidate.Kind == MapCandidateKind.Skipped)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    SkippedGeneratedMapRule,
                    candidate.Location,
                    candidate.MapDisplayName,
                    candidate.SkipReason));
                return;
            }

            if (candidate.MaterializerSkipReason != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    SkippedGeneratedMaterializerRule,
                    candidate.Location,
                    candidate.MapDisplayName,
                    candidate.MaterializerSkipReason));
            }

            if (candidate.MaterializerDiagnostic != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    candidate.MaterializerDiagnostic.Descriptor,
                    candidate.MaterializerDiagnostic.Location,
                    candidate.MaterializerDiagnostic.Arguments));
            }
        }

        private static ISet<string> ReportDuplicateEntityMaps(
            SourceProductionContext context,
            IList<MapCandidate> validMaps)
        {
            var duplicateEntityTypeNames = new HashSet<string>(StringComparer.Ordinal);
            var groups = validMaps
                .Where(candidate => candidate.ProfileTypeName == null)
                .GroupBy(candidate => candidate.EntityTypeName, StringComparer.Ordinal)
                .Where(group => group.Count() > 1);

            foreach (var group in groups)
            {
                var orderedGroup = group
                    .OrderBy(candidate => candidate.MapTypeName, StringComparer.Ordinal)
                    .ToList();
                var first = orderedGroup[0];
                duplicateEntityTypeNames.Add(first.EntityTypeName);

                foreach (var duplicate in orderedGroup.Skip(1))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DuplicateGeneratedEntityMapRule,
                        duplicate.Location,
                        duplicate.EntityTypeName,
                        first.MapTypeName,
                        duplicate.MapTypeName));
                }
            }

            return duplicateEntityTypeNames;
        }

        private static ISet<string> ReportDuplicateProfileMaps(
            SourceProductionContext context,
            IList<MapCandidate> validMaps)
        {
            var duplicateProfileKeys = new HashSet<string>(StringComparer.Ordinal);
            var groups = validMaps
                .Where(candidate => candidate.ProfileTypeName != null)
                .GroupBy(candidate => candidate.ProfileKey, StringComparer.Ordinal)
                .Where(group => group.Count() > 1);

            foreach (var group in groups)
            {
                var orderedGroup = group
                    .OrderBy(candidate => candidate.MapTypeName, StringComparer.Ordinal)
                    .ToList();
                var first = orderedGroup[0];
                duplicateProfileKeys.Add(first.ProfileKey);

                foreach (var duplicate in orderedGroup.Skip(1))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DuplicateGeneratedProfileMapRule,
                        duplicate.Location,
                        duplicate.EntityTypeName,
                        duplicate.ProfileTypeName,
                        first.MapTypeName,
                        duplicate.MapTypeName));
                }
            }

            return duplicateProfileKeys;
        }

        private static string CreateGeneratedSource(IList<MapCandidate> maps)
        {
            var materializers = maps
                .Where(map => map.Materializer != null)
                .Select((map, index) => map.Materializer.WithMethodName("Read" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .ToList();
            var materializerByMap = materializers.ToDictionary(materializer => materializer.MapTypeName, StringComparer.Ordinal);

            var builder = new StringBuilder();
            builder.AppendLine("// <auto-generated/>");
            builder.AppendLine("namespace Dapper.FluentMap");
            builder.AppendLine("{");
            builder.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCode(\"Dapper.FluentMap.Generators\", \"2.0.0\")]");
            builder.AppendLine("    internal static class DapperFluentMapGeneratedRegistration");
            builder.AppendLine("    {");
            builder.AppendLine("        public static global::Dapper.FluentMap.Configuration.FluentMapConfiguration AddGeneratedMappings(");
            builder.AppendLine("            this global::Dapper.FluentMap.Configuration.FluentMapConfiguration configuration)");
            builder.AppendLine("        {");
            builder.AppendLine("            if (configuration == null)");
            builder.AppendLine("            {");
            builder.AppendLine("                throw new global::System.ArgumentNullException(nameof(configuration));");
            builder.AppendLine("            }");
            builder.AppendLine();

            if (maps.Count == 0)
            {
                builder.AppendLine("            return configuration;");
            }
            else
            {
                builder.AppendLine("            return configuration");
                for (var index = 0; index < maps.Count; index++)
                {
                    var map = maps[index];
                    var materializerByThisMap = default(GeneratedMaterializerInfo);
                    materializerByMap.TryGetValue(map.MapTypeName, out materializerByThisMap);

                    builder.Append(map.ProfileTypeName == null
                        ? "                .AddMap<"
                        : "                .AddProfile<");
                    builder.Append(map.MapTypeName);
                    builder.AppendLine(">()");

                    if (materializerByThisMap != null)
                    {
                        AppendGeneratedMaterializerRegistration(builder, materializerByThisMap);
                    }

                    if (index == maps.Count - 1)
                    {
                        builder.AppendLine("                ;");
                    }
                }
            }

            builder.AppendLine("        }");
            builder.AppendLine("    }");

            if (materializers.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCode(\"Dapper.FluentMap.Generators\", \"2.0.0\")]");
                builder.AppendLine("    internal static class DapperFluentMapGeneratedMaterializers");
                builder.AppendLine("    {");

                foreach (var materializer in materializers)
                {
                    AppendMaterializerConverterFields(builder, materializer);
                }

                foreach (var materializer in materializers)
                {
                    AppendMaterializerMethod(builder, materializer);
                }

                AppendReadHelper(builder);
                builder.AppendLine("    }");
            }

            builder.AppendLine("}");

            return builder.ToString();
        }

        private static void AppendGeneratedMaterializerRegistration(StringBuilder builder, GeneratedMaterializerInfo materializer)
        {
            builder.Append("                .AddGeneratedMaterializer<");
            builder.Append(materializer.EntityTypeName);
            if (materializer.ProfileTypeName != null)
            {
                builder.Append(", ");
                builder.Append(materializer.ProfileTypeName);
            }

            builder.AppendLine(">(");
            builder.AppendLine("                    new global::Dapper.FluentMap.Materialization.GeneratedMaterializerColumn[]");
            builder.AppendLine("                    {");

            for (var index = 0; index < materializer.Columns.Count; index++)
            {
                var column = materializer.Columns[index];
                builder.Append("                        global::Dapper.FluentMap.Materialization.GeneratedMaterializerColumn.");
                builder.Append(column.Ignored ? "Ignore(" : "Map(");
                builder.Append(EscapeStringLiteral(column.ColumnName));
                if (!column.Ignored)
                {
                    builder.Append(", ");
                    builder.Append(EscapeStringLiteral(column.MemberPath));
                    if (column.ReadConverter != null)
                    {
                        builder.Append(", typeof(");
                        builder.Append(column.ReadConverter.ConverterTypeName);
                        builder.Append("), typeof(");
                        builder.Append(column.ReadConverter.DatabaseTypeName);
                        builder.Append("), typeof(");
                        builder.Append(column.ReadConverter.PropertyTypeName);
                        builder.Append(')');
                    }
                }

                builder.Append(index == materializer.Columns.Count - 1 ? ")" : "),");
                builder.AppendLine();
            }

            builder.AppendLine("                    },");
            builder.Append("                    global::Dapper.FluentMap.DapperFluentMapGeneratedMaterializers.");
            builder.Append(materializer.MethodName);
            builder.AppendLine(")");
        }

        private static void AppendMaterializerConverterFields(StringBuilder builder, GeneratedMaterializerInfo materializer)
        {
            foreach (var leaf in materializer.Root.GetLeaves().Where(leaf => leaf.ReadConverter != null))
            {
                builder.Append("        private static readonly ");
                builder.Append(leaf.ReadConverter.ConverterTypeName);
                builder.Append(' ');
                builder.Append(GetConverterFieldName(materializer, leaf));
                builder.Append(" = new ");
                builder.Append(leaf.ReadConverter.ConverterTypeName);
                builder.AppendLine("();");
            }
        }

        private static void AppendMaterializerMethod(StringBuilder builder, GeneratedMaterializerInfo materializer)
        {
            builder.AppendLine();
            builder.Append("        internal static ");
            builder.Append(materializer.EntityTypeName);
            builder.Append(' ');
            builder.Append(materializer.MethodName);
            builder.AppendLine("(global::System.Data.IDataRecord record)");
            builder.AppendLine("        {");
            builder.AppendLine("            if (record == null)");
            builder.AppendLine("            {");
            builder.AppendLine("                throw new global::System.ArgumentNullException(nameof(record));");
            builder.AppendLine("            }");
            builder.AppendLine();

            AppendMaterializeNode(builder, materializer, materializer.Root, "entity", "            ", null, materializer.EntityTypeName, localAlreadyDeclared: false);
            builder.AppendLine();
            builder.AppendLine("            return entity;");

            builder.AppendLine("        }");
        }

        private static void AppendMaterializeNode(
            StringBuilder builder,
            GeneratedMaterializerInfo materializer,
            GeneratedMaterializationNode node,
            string localName,
            string indent,
            string parentLocalName,
            string entityTypeName,
            bool localAlreadyDeclared)
        {
            if (node.Constructor == null)
            {
                AppendCreateParameterlessNode(builder, node, localName, indent, parentLocalName, localAlreadyDeclared);
            }
            else
            {
                AppendCreateConstructorNode(builder, materializer, node, localName, indent, entityTypeName, localAlreadyDeclared);
            }

            foreach (var child in node.PostConstructorChildren)
            {
                AppendApplyChild(builder, materializer, child, localName, indent, entityTypeName);
            }

            foreach (var leaf in node.PostConstructorLeaves)
            {
                AppendAssignLeaf(builder, materializer, leaf, localName, indent);
            }
        }

        private static void AppendCreateParameterlessNode(
            StringBuilder builder,
            GeneratedMaterializationNode node,
            string localName,
            string indent,
            string parentLocalName,
            bool localAlreadyDeclared)
        {
            if (node.IsRoot || parentLocalName == null || !node.HasPublicGetter)
            {
                builder.Append(indent);
                if (!localAlreadyDeclared)
                {
                    builder.Append("var ");
                }

                builder.Append(localName);
                builder.Append(" = new ");
                builder.Append(node.TypeName);
                builder.AppendLine("();");
                return;
            }

            builder.Append(indent);
            builder.Append("var ");
            builder.Append(localName);
            builder.Append(" = ");
            builder.Append(parentLocalName);
            builder.Append('.');
            builder.Append(EscapeIdentifier(node.PropertyName));
            builder.AppendLine(";");
            builder.Append(indent);
            builder.Append("if (");
            builder.Append(localName);
            builder.AppendLine(" == null)");
            builder.Append(indent);
            builder.AppendLine("{");
            builder.Append(indent);
            builder.Append("    ");
            builder.Append(localName);
            builder.Append(" = new ");
            builder.Append(node.TypeName);
            builder.AppendLine("();");
            if (node.HasPublicSetter)
            {
                builder.Append(indent);
                builder.Append("    ");
                builder.Append(parentLocalName);
                builder.Append('.');
                builder.Append(EscapeIdentifier(node.PropertyName));
                builder.Append(" = ");
                builder.Append(localName);
                builder.AppendLine(";");
            }

            builder.Append(indent);
            builder.AppendLine("}");
        }

        private static void AppendCreateConstructorNode(
            StringBuilder builder,
            GeneratedMaterializerInfo materializer,
            GeneratedMaterializationNode node,
            string localName,
            string indent,
            string entityTypeName,
            bool localAlreadyDeclared)
        {
            foreach (var parameter in node.Constructor.Parameters)
            {
                if (parameter.Leaf != null)
                {
                    builder.Append(indent);
                    builder.Append("var ");
                    builder.Append(parameter.LocalName);
                    builder.Append(" = ");
                    AppendReadExpression(builder, materializer, parameter.Leaf);
                    builder.AppendLine(";");
                    continue;
                }

                AppendCreateChildValue(builder, materializer, parameter.Child, parameter.LocalName, indent, entityTypeName);
            }

            builder.AppendLine();
            if (!localAlreadyDeclared)
            {
                builder.Append(indent);
                builder.Append(node.TypeName);
                builder.Append(' ');
                builder.Append(localName);
                builder.AppendLine(";");
            }

            builder.Append(indent);
            builder.AppendLine("try");
            builder.Append(indent);
            builder.AppendLine("{");
            builder.Append(indent);
            builder.Append("    ");
            builder.Append(localName);
            builder.Append(" = new ");
            builder.Append(node.TypeName);
            builder.Append('(');
            builder.Append(string.Join(", ", node.Constructor.Parameters.Select(parameter => parameter.LocalName)));
            builder.AppendLine(");");
            builder.Append(indent);
            builder.AppendLine("}");
            builder.Append(indent);
            builder.AppendLine("catch (global::System.Exception exception)");
            builder.Append(indent);
            builder.AppendLine("{");
            builder.Append(indent);
            builder.Append("    throw new global::Dapper.FluentMap.FluentMapConfigurationException(");
            builder.Append(EscapeStringLiteral(
                "Failed to materialize type '" + node.TypeName + "' at member path '" + node.MemberPath + "' on entity '" + entityTypeName + "' using generated constructor materializer. Columns: " + FormatGeneratedColumns(node) + ". See the inner exception for the domain failure."));
            builder.AppendLine(", exception);");
            builder.Append(indent);
            builder.AppendLine("}");
        }

        private static void AppendApplyChild(
            StringBuilder builder,
            GeneratedMaterializerInfo materializer,
            GeneratedMaterializationNode child,
            string parentLocalName,
            string indent,
            string entityTypeName)
        {
            builder.Append(indent);
            builder.Append("if (");
            AppendHasAnyValueExpression(builder, child);
            builder.AppendLine(")");
            builder.Append(indent);
            builder.AppendLine("{");
            var childLocalName = "node" + child.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            AppendMaterializeNode(builder, materializer, child, childLocalName, indent + "    ", parentLocalName, entityTypeName, localAlreadyDeclared: false);
            if (child.HasPublicSetter && (child.Constructor != null || !child.HasPublicGetter))
            {
                builder.Append(indent);
                builder.Append("    ");
                builder.Append(parentLocalName);
                builder.Append('.');
                builder.Append(EscapeIdentifier(child.PropertyName));
                builder.Append(" = ");
                builder.Append(childLocalName);
                builder.AppendLine(";");
            }

            builder.Append(indent);
            builder.AppendLine("}");
            if (child.HasPublicSetter && child.CanAssignNull)
            {
                builder.Append(indent);
                builder.AppendLine("else");
                builder.Append(indent);
                builder.AppendLine("{");
                builder.Append(indent);
                builder.Append("    ");
                builder.Append(parentLocalName);
                builder.Append('.');
                builder.Append(EscapeIdentifier(child.PropertyName));
                builder.AppendLine(" = null;");
                builder.Append(indent);
                builder.AppendLine("}");
            }
        }

        private static void AppendCreateChildValue(
            StringBuilder builder,
            GeneratedMaterializerInfo materializer,
            GeneratedMaterializationNode child,
            string localName,
            string indent,
            string entityTypeName)
        {
            builder.Append(indent);
            builder.Append(child.TypeName);
            builder.Append(' ');
            builder.Append(localName);
            builder.AppendLine(" = null;");
            builder.Append(indent);
            builder.Append("if (");
            AppendHasAnyValueExpression(builder, child);
            builder.AppendLine(")");
            builder.Append(indent);
            builder.AppendLine("{");
            AppendMaterializeNode(builder, materializer, child, localName, indent + "    ", null, entityTypeName, localAlreadyDeclared: true);
            builder.Append(indent);
            builder.AppendLine("}");
        }

        private static void AppendAssignLeaf(
            StringBuilder builder,
            GeneratedMaterializerInfo materializer,
            GeneratedPropertyBinding leaf,
            string targetLocalName,
            string indent)
        {
            builder.Append(indent);
            builder.Append(targetLocalName);
            builder.Append('.');
            builder.Append(EscapeIdentifier(leaf.PropertyName));
            builder.Append(" = ");
            AppendReadExpression(builder, materializer, leaf);
            builder.AppendLine(";");
        }

        private static void AppendReadExpression(
            StringBuilder builder,
            GeneratedMaterializerInfo materializer,
            GeneratedPropertyBinding leaf)
        {
            if (leaf.ReadConverter == null)
            {
                builder.Append("Read<");
                builder.Append(leaf.TypeName);
                builder.Append(">(record, ");
                builder.Append(leaf.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.Append(')');
                return;
            }

            builder.Append("ReadConverted<");
            builder.Append(leaf.ReadConverter.DatabaseTypeName);
            builder.Append(", ");
            builder.Append(leaf.ReadConverter.PropertyTypeName);
            builder.Append(", ");
            builder.Append(leaf.TypeName);
            builder.Append(">(record, ");
            builder.Append(leaf.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(GetConverterFieldName(materializer, leaf));
            builder.Append(", ");
            builder.Append(EscapeStringLiteral(materializer.EntityTypeName));
            builder.Append(", ");
            builder.Append(materializer.ProfileTypeName == null
                ? "null"
                : EscapeStringLiteral(materializer.ProfileTypeName));
            builder.Append(", ");
            builder.Append(EscapeStringLiteral(leaf.MemberPath));
            builder.Append(", ");
            builder.Append(EscapeStringLiteral(leaf.ColumnName));
            builder.Append(", ");
            builder.Append(EscapeStringLiteral(leaf.ReadConverter.ConverterTypeName));
            builder.Append(", ");
            builder.Append(EscapeStringLiteral(leaf.ReadConverter.DatabaseTypeName));
            builder.Append(", ");
            builder.Append(EscapeStringLiteral(leaf.ReadConverter.PropertyTypeName));
            builder.Append(", ");
            builder.Append(EscapeStringLiteral(leaf.TypeName));
            builder.Append(')');
        }

        private static string GetConverterFieldName(GeneratedMaterializerInfo materializer, GeneratedPropertyBinding leaf)
        {
            return materializer.MethodName + "Converter" + leaf.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void AppendHasAnyValueExpression(StringBuilder builder, GeneratedMaterializationNode node)
        {
            var ordinals = node.SubtreeOrdinals.ToList();
            if (ordinals.Count == 0)
            {
                builder.Append("false");
                return;
            }

            for (var index = 0; index < ordinals.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(" || ");
                }

                builder.Append("!record.IsDBNull(");
                builder.Append(ordinals[index].ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.Append(')');
            }
        }

        private static string FormatGeneratedColumns(GeneratedMaterializationNode node)
        {
            return string.Join(", ", node.GetColumnNames().Select(column => "'" + column + "'"));
        }

        private static void AppendReadHelper(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("        private static T Read<T>(global::System.Data.IDataRecord record, int ordinal)");
            builder.AppendLine("        {");
            builder.AppendLine("            if (record.IsDBNull(ordinal))");
            builder.AppendLine("            {");
            builder.AppendLine("                return default(T);");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            var value = record.GetValue(ordinal);");
            builder.AppendLine("            return ConvertValue<T>(value);");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        private static TTarget ReadConverted<TDatabase, TProperty, TTarget>(");
            builder.AppendLine("            global::System.Data.IDataRecord record,");
            builder.AppendLine("            int ordinal,");
            builder.AppendLine("            global::Dapper.FluentMap.Mapping.IReadPropertyConverter<TDatabase, TProperty> converter,");
            builder.AppendLine("            string entityTypeName,");
            builder.AppendLine("            string profileTypeName,");
            builder.AppendLine("            string memberPath,");
            builder.AppendLine("            string columnName,");
            builder.AppendLine("            string converterTypeName,");
            builder.AppendLine("            string converterDatabaseTypeName,");
            builder.AppendLine("            string converterPropertyTypeName,");
            builder.AppendLine("            string targetTypeName)");
            builder.AppendLine("        {");
            builder.AppendLine("            var value = record.GetValue(ordinal);");
            builder.AppendLine("            if (value == null || value == global::System.DBNull.Value)");
            builder.AppendLine("            {");
            builder.AppendLine("                return default(TTarget);");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            try");
            builder.AppendLine("            {");
            builder.AppendLine("                var converted = converter.ConvertFromDatabase(ConvertValue<TDatabase>(value));");
            builder.AppendLine("                if ((object)converted == null && (object)default(TTarget) != null)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.InvalidOperationException(");
            builder.AppendLine("                        \"Read converter '\" + converterTypeName + \"' returned null for non-nullable target type '\" + targetTypeName + \"'.\");");
            builder.AppendLine("                }");
            builder.AppendLine();
            builder.AppendLine("                return (TTarget)(object)converted;");
            builder.AppendLine("            }");
            builder.AppendLine("            catch (global::System.Exception exception) when (!(exception is global::Dapper.FluentMap.FluentMapConfigurationException))");
            builder.AppendLine("            {");
            builder.AppendLine("                var profileContext = profileTypeName == null");
            builder.AppendLine("                    ? string.Empty");
            builder.AppendLine("                    : \" Profile: '\" + profileTypeName + \"'.\";");
            builder.AppendLine("                throw new global::Dapper.FluentMap.FluentMapConfigurationException(");
            builder.AppendLine("                    \"Read converter failed for entity '\" + entityTypeName + \"'.\" + profileContext +");
            builder.AppendLine("                    \" Member path: '\" + memberPath + \"'. Column: '\" + columnName + \"'. Converter: '\" + converterTypeName +");
            builder.AppendLine("                    \"'. Source type: '\" + value.GetType().FullName + \"'. Converter database type: '\" + converterDatabaseTypeName +");
            builder.AppendLine("                    \"'. Converter property type: '\" + converterPropertyTypeName + \"'. Target type: '\" + targetTypeName +");
            builder.AppendLine("                    \"'. See the inner exception for the converter failure.\",");
            builder.AppendLine("                    exception);");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        private static T ConvertValue<T>(object value)");
            builder.AppendLine("        {");
            builder.AppendLine("            if (value is T typedValue)");
            builder.AppendLine("            {");
            builder.AppendLine("                return typedValue;");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            var targetType = typeof(T);");
            builder.AppendLine("            var conversionType = global::System.Nullable.GetUnderlyingType(targetType) ?? targetType;");
            builder.AppendLine("            if (conversionType.IsEnum)");
            builder.AppendLine("            {");
            builder.AppendLine("                var enumValue = value is string text");
            builder.AppendLine("                    ? global::System.Enum.Parse(conversionType, text)");
            builder.AppendLine("                    : global::System.Enum.ToObject(conversionType, value);");
            builder.AppendLine("                return (T)enumValue;");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            if (conversionType == typeof(global::System.Guid) && value is string guidText)");
            builder.AppendLine("            {");
            builder.AppendLine("                return (T)(object)new global::System.Guid(guidText);");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            return (T)global::System.Convert.ChangeType(value, conversionType, global::System.Globalization.CultureInfo.InvariantCulture);");
            builder.AppendLine("        }");
        }

        private static GeneratedMaterializerInfo TryCreateGeneratedMaterializer(
            ClassDeclarationSyntax classDeclaration,
            INamedTypeSymbol mapType,
            INamedTypeSymbol entityType,
            string profileTypeName,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken,
            out string skipReason,
            out GeneratedDiagnostic diagnostic)
        {
            skipReason = null;
            diagnostic = null;

            var constructor = GetPublicParameterlessConstructorDeclaration(classDeclaration, mapType, semanticModel, cancellationToken);
            if (constructor == null || constructor.Body == null)
            {
                return null;
            }

            if (ContainsIncludeBaseInvocation(constructor, semanticModel, cancellationToken))
            {
                skipReason = "IncludeBase<TBase>() is not supported by generated materializers in this phase";
                return null;
            }

            var mapInvocations = new List<GeneratedMapInvocation>();
            foreach (var invocation in constructor.Body.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
                if (!IsMapInvocation(method))
                {
                    continue;
                }

                if (!TryCreateDirectMapInvocation(
                    invocation,
                    mapType,
                    semanticModel,
                    cancellationToken,
                    out var mapInvocation,
                    out skipReason,
                    out diagnostic))
                {
                    return null;
                }

                mapInvocations.Add(mapInvocation);
            }

            if (mapInvocations.Count == 0)
            {
                return null;
            }

            var columns = new List<GeneratedColumnBinding>();
            var root = new GeneratedMaterializationNode(
                id: 0,
                type: entityType,
                parentProperty: null,
                memberPath: entityType.Name,
                isRoot: true);

            var nextNodeId = 1;
            for (var index = 0; index < mapInvocations.Count; index++)
            {
                var invocation = mapInvocations[index];
                columns.Add(new GeneratedColumnBinding(
                    invocation.ColumnName,
                    invocation.MemberPath.Display,
                    invocation.Ignored,
                    invocation.ReadConverter));

                if (invocation.Ignored)
                {
                    continue;
                }

                if (!TryAddMaterializedPath(root, invocation, index, ref nextNodeId, out skipReason))
                {
                    return null;
                }
            }

            if (!root.Seal(entityType, out skipReason))
            {
                return null;
            }

            return new GeneratedMaterializerInfo(
                mapType.ToDisplayString(FullyQualifiedTypeFormat),
                entityType.ToDisplayString(FullyQualifiedTypeFormat),
                profileTypeName,
                columns,
                root,
                methodName: null);
        }

        private static bool TryAddMaterializedPath(
            GeneratedMaterializationNode root,
            GeneratedMapInvocation invocation,
            int ordinal,
            ref int nextNodeId,
            out string skipReason)
        {
            skipReason = null;

            var properties = invocation.MemberPath.Properties;
            for (var index = 0; index < properties.Count - 1; index++)
            {
                var property = properties[index];
                if (!IsSupportedComplexType(property.Type))
                {
                    skipReason = $"nested property '{property.Name}' has type '{FormatSymbol(property.Type)}', which is not supported by generated materializers";
                    return false;
                }
            }

            var leaf = properties[properties.Count - 1];
            if (invocation.ReadConverter == null && !IsSupportedScalarType(leaf.Type))
            {
                skipReason = $"property '{invocation.MemberPath.Display}' has type '{FormatSymbol(leaf.Type)}', which is not supported by generated materializers";
                return false;
            }

            if (invocation.ReadConverter != null && !IsSupportedConvertedPropertyType(leaf.Type))
            {
                skipReason = $"property '{invocation.MemberPath.Display}' has converted type '{FormatSymbol(leaf.Type)}', which is not accessible from generated materializers";
                return false;
            }

            var node = root;
            for (var index = 0; index < properties.Count - 1; index++)
            {
                node = node.FindOrAddChild(properties[index], ref nextNodeId);
            }

            node.AddLeaf(new GeneratedPropertyBinding(
                ordinal,
                invocation.ColumnName,
                invocation.MemberPath.Display,
                leaf.Name,
                leaf.Type.ToDisplayString(FullyQualifiedTypeFormat),
                HasPublicSetter(leaf),
                leaf.Type,
                invocation.ReadConverter));

            return true;
        }

        private static ConstructorDeclarationSyntax GetPublicParameterlessConstructorDeclaration(
            ClassDeclarationSyntax classDeclaration,
            INamedTypeSymbol mapType,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken)
        {
            foreach (var constructor in classDeclaration.Members.OfType<ConstructorDeclarationSyntax>())
            {
                var symbol = semanticModel.GetDeclaredSymbol(constructor, cancellationToken);
                if (symbol != null &&
                    SymbolEqualityComparer.Default.Equals(symbol.ContainingType, mapType) &&
                    symbol.DeclaredAccessibility == Accessibility.Public &&
                    symbol.Parameters.Length == 0)
                {
                    return constructor;
                }
            }

            return null;
        }

        private static bool ContainsIncludeBaseInvocation(
            ConstructorDeclarationSyntax constructor,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken)
        {
            foreach (var invocation in constructor.Body.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
                if (IsIncludeBaseInvocation(method))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryCreateDirectMapInvocation(
            InvocationExpressionSyntax mapInvocation,
            INamedTypeSymbol mapType,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken,
            out GeneratedMapInvocation result,
            out string skipReason,
            out GeneratedDiagnostic diagnostic)
        {
            result = null;
            skipReason = null;
            diagnostic = null;

            if (mapInvocation.ArgumentList.Arguments.Count != 1 ||
                !TryGetLambda(mapInvocation.ArgumentList.Arguments[0].Expression, out var lambda))
            {
                skipReason = "Map(...) invocation is not a statically analyzable lambda expression";
                return false;
            }

            if (!TryCreateMemberPath(lambda.Body, semanticModel, cancellationToken, out var memberPath, out skipReason))
            {
                return false;
            }

            var statement = mapInvocation.FirstAncestorOrSelf<ExpressionStatementSyntax>();
            if (statement == null)
            {
                skipReason = "Map(...) invocation is not a direct constructor statement";
                return false;
            }

            var column = memberPath.TerminalName;
            var ignored = false;
            var readConverter = default(GeneratedReadConverterBinding);
            SyntaxNode current = mapInvocation;
            while (current.Parent is MemberAccessExpressionSyntax memberAccess &&
                   memberAccess.Expression == current &&
                   memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
            {
                var chainedMethod = semanticModel.GetSymbolInfo(chainedInvocation, cancellationToken).Symbol as IMethodSymbol;
                if (IsToColumnInvocation(chainedMethod))
                {
                    if (!TryGetColumn(chainedInvocation, semanticModel, cancellationToken, out column))
                    {
                        skipReason = "ToColumn(...) must use a literal string column name";
                        return false;
                    }
                }
                else if (IsIgnoreInvocation(chainedMethod))
                {
                    ignored = true;
                }
                else if (IsGeneratedReadConverterInvocation(chainedMethod))
                {
                    if (chainedInvocation.ArgumentList.Arguments.Count != 0)
                    {
                        skipReason = "read converter instances and delegates are not statically supported by generated materializers";
                        return false;
                    }

                    if (readConverter != null)
                    {
                        skipReason = "multiple read converters in the same map chain are not supported by generated materializers";
                        return false;
                    }

                    if (!TryCreateReadConverterBinding(
                        chainedMethod,
                        memberPath.Properties[memberPath.Properties.Count - 1].Type,
                        out readConverter,
                        out var converterReason))
                    {
                        if (IsGeneratedReadConverterFallbackReason(converterReason))
                        {
                            skipReason = converterReason;
                            return false;
                        }

                        diagnostic = GeneratedDiagnostic.InvalidReadConverter(
                            chainedInvocation.GetLocation(),
                            mapType.ToDisplayString(FullyQualifiedTypeFormat),
                            chainedMethod.TypeArguments.Length > 0
                                ? chainedMethod.TypeArguments[0].ToDisplayString(FullyQualifiedTypeFormat)
                                : chainedMethod.Name,
                            converterReason);
                        return false;
                    }
                }
                else if (IsWriteOnlyConverterInvocation(chainedMethod))
                {
                    // Write-only conversion metadata does not change the generated read materializer.
                }
                else if (IsReadNeutralPersistenceInvocation(chainedMethod))
                {
                    // Write-only metadata does not change the generated read materializer.
                }
                else
                {
                    skipReason = "the map chain uses an unsupported mapping method";
                    return false;
                }

                current = chainedInvocation;
            }

            if (current != statement.Expression)
            {
                skipReason = "Map(...) invocation is not a direct constructor statement";
                return false;
            }

            result = new GeneratedMapInvocation(memberPath, column, ignored, readConverter);
            return true;
        }

        private static bool IsGeneratedReadConverterFallbackReason(string reason)
        {
            return string.Equals(reason, "the converter type is not accessible from generated code", StringComparison.Ordinal) ||
                   string.Equals(reason, "the converter type does not have a public parameterless constructor", StringComparison.Ordinal);
        }

        private static bool TryGetColumn(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken,
            out string column)
        {
            column = null;

            if (invocation.ArgumentList.Arguments.Count == 0)
            {
                return false;
            }

            var columnConstant = semanticModel.GetConstantValue(
                invocation.ArgumentList.Arguments[0].Expression,
                cancellationToken);
            if (!columnConstant.HasValue || !(columnConstant.Value is string columnValue))
            {
                return false;
            }

            column = columnValue;
            return true;
        }

        private static bool TryGetLambda(ExpressionSyntax expression, out LambdaExpressionSyntax lambda)
        {
            expression = StripCastsAndParentheses(expression);
            lambda = expression as LambdaExpressionSyntax;
            return lambda != null;
        }

        private static bool TryCreateMemberPath(
            CSharpSyntaxNode body,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken,
            out GeneratedMemberPath memberPath,
            out string reason)
        {
            memberPath = null;
            reason = null;

            var expression = StripCastsAndParentheses(body as ExpressionSyntax);
            var properties = new Stack<IPropertySymbol>();

            while (expression != null)
            {
                if (expression is MemberAccessExpressionSyntax memberAccess)
                {
                    var symbol = semanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol;
                    var property = symbol as IPropertySymbol;
                    if (property == null)
                    {
                        reason = symbol == null
                            ? "the member could not be resolved statically"
                            : $"member '{symbol.Name}' is not a property";
                        return false;
                    }

                    if (property.IsIndexer || property.Parameters.Length > 0)
                    {
                        reason = $"indexed property '{property.Name}' is not supported";
                        return false;
                    }

                    properties.Push(property);
                    expression = StripCastsAndParentheses(memberAccess.Expression);
                    continue;
                }

                if (expression is IdentifierNameSyntax identifier)
                {
                    var symbol = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
                    if (symbol is IParameterSymbol && properties.Count > 0)
                    {
                        memberPath = GeneratedMemberPath.Create(properties);
                        return true;
                    }

                    reason = "the expression must resolve to a property path rooted in the entity parameter";
                    return false;
                }

                reason = "the expression must resolve to a property path";
                return false;
            }

            reason = "the expression must resolve to a property path";
            return false;
        }

        private static ExpressionSyntax StripCastsAndParentheses(ExpressionSyntax expression)
        {
            while (true)
            {
                if (expression is ParenthesizedExpressionSyntax parenthesized)
                {
                    expression = parenthesized.Expression;
                    continue;
                }

                if (expression is CastExpressionSyntax cast)
                {
                    expression = cast.Expression;
                    continue;
                }

                return expression;
            }
        }

        private static bool TryCreateConstructorBinding(
            INamedTypeSymbol entityType,
            GeneratedMaterializationNode node,
            out GeneratedConstructorBinding constructorBinding,
            out string skipReason)
        {
            constructorBinding = null;
            skipReason = null;

            var candidates = new List<GeneratedConstructorBinding>();
            var nodeType = (INamedTypeSymbol)node.Type;
            foreach (var constructor in nodeType.InstanceConstructors
                .Where(constructor => constructor.DeclaredAccessibility == Accessibility.Public && !constructor.IsStatic))
            {
                var parameters = new List<GeneratedConstructorParameter>();
                var failed = false;
                var score = 0;

                foreach (var parameter in constructor.Parameters)
                {
                    var parameterBinding = TryBindConstructorParameter(node, parameter);
                    if (parameterBinding == null)
                    {
                        failed = true;
                        break;
                    }

                    var localName = node.IsRoot
                        ? CreateUniqueLocalName(parameter.Name, parameters.Count)
                        : "arg" + node.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_" + parameters.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    parameters.Add(new GeneratedConstructorParameter(
                        localName,
                        parameter.Type.ToDisplayString(FullyQualifiedTypeFormat),
                        parameterBinding.Leaf,
                        parameterBinding.Child));
                    score += parameterBinding.Score;
                }

                if (!failed &&
                    node.Leaves.Where(leaf => !leaf.CanAssign).All(leaf => parameters.Any(parameter => parameter.Leaf == leaf)) &&
                    node.Children.Where(child => !child.CanAssignToParent).All(child => parameters.Any(parameter => parameter.Child == child)))
                {
                    candidates.Add(new GeneratedConstructorBinding(constructor, parameters, score));
                }
            }

            if (candidates.Count == 1)
            {
                constructorBinding = candidates[0];
                return true;
            }

            if (candidates.Count > 1)
            {
                var bestScore = candidates.Max(candidate => candidate.Score);
                var best = candidates.Where(candidate => candidate.Score == bestScore).ToList();
                if (best.Count == 1)
                {
                    constructorBinding = best[0];
                    return true;
                }
            }

            skipReason = candidates.Count == 0
                ? $"type '{FormatSymbol(node.Type)}' at member path '{node.MemberPath}' does not have a supported public constructor for generated materialization"
                : $"type '{FormatSymbol(node.Type)}' at member path '{node.MemberPath}' has multiple public constructors that match generated materialization";
            return false;
        }

        private static GeneratedParameterMatch TryBindConstructorParameter(
            GeneratedMaterializationNode node,
            IParameterSymbol parameter)
        {
            var matches = node.Leaves
                .Where(leaf => string.Equals(leaf.PropertyName, parameter.Name, StringComparison.OrdinalIgnoreCase) &&
                               IsParameterCompatible(parameter.Type, leaf.PropertyTypeSymbol))
                .Select(leaf => GeneratedParameterMatch.ForLeaf(
                    leaf,
                    GetCompatibilityScore(parameter.Type, leaf.PropertyTypeSymbol)))
                .Concat(node.Children
                    .Where(child => string.Equals(child.PropertyName, parameter.Name, StringComparison.OrdinalIgnoreCase) &&
                                    IsParameterCompatible(parameter.Type, child.PropertyTypeSymbol))
                    .Select(child => GeneratedParameterMatch.ForChild(
                        child,
                        GetCompatibilityScore(parameter.Type, child.PropertyTypeSymbol))))
                .OrderByDescending(match => match.Score)
                .ToList();

            if (matches.Count == 0)
            {
                return null;
            }

            var bestScore = matches[0].Score;
            var best = matches.Where(match => match.Score == bestScore).ToList();
            return best.Count == 1 ? best[0] : null;
        }

        private static bool IsParameterCompatible(ITypeSymbol parameterType, ITypeSymbol sourceType)
        {
            var parameter = UnwrapNullable(parameterType);
            var source = UnwrapNullable(sourceType);
            return IsAssignableFrom(parameter, source) || IsAssignableFrom(source, parameter);
        }

        private static int GetCompatibilityScore(ITypeSymbol parameterType, ITypeSymbol sourceType)
        {
            var parameter = UnwrapNullable(parameterType);
            var source = UnwrapNullable(sourceType);
            return SymbolEqualityComparer.Default.Equals(parameter, source) ? 2 : 1;
        }

        private static bool IsAssignableFrom(ITypeSymbol targetType, ITypeSymbol sourceType)
        {
            if (SymbolEqualityComparer.Default.Equals(targetType, sourceType))
            {
                return true;
            }

            for (var current = sourceType; current != null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(targetType, current))
                {
                    return true;
                }
            }

            foreach (var interfaceType in sourceType.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(targetType, interfaceType))
                {
                    return true;
                }
            }

            return false;
        }

        private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
        {
            var namedType = type as INamedTypeSymbol;
            if (namedType != null &&
                namedType.ConstructedFrom != null &&
                namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T &&
                namedType.TypeArguments.Length == 1)
            {
                return namedType.TypeArguments[0];
            }

            return type;
        }

        private static bool IsSupportedScalarType(ITypeSymbol type)
        {
            var unwrapped = UnwrapNullable(type);
            if (unwrapped.TypeKind == TypeKind.Enum)
            {
                return true;
            }

            switch (unwrapped.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_Byte:
                case SpecialType.System_SByte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_Decimal:
                case SpecialType.System_Char:
                case SpecialType.System_String:
                case SpecialType.System_DateTime:
                    return true;
            }

            return IsType(unwrapped as INamedTypeSymbol, "System", "Guid");
        }

        private static bool IsSupportedComplexType(ITypeSymbol type)
        {
            var unwrapped = UnwrapNullable(type);
            var namedType = unwrapped as INamedTypeSymbol;
            return namedType != null &&
                   namedType.TypeKind == TypeKind.Class &&
                   IsAccessibleFromGeneratedCode(namedType);
        }

        private static bool IsSupportedConvertedPropertyType(ITypeSymbol type)
        {
            var unwrapped = UnwrapNullable(type);
            if (IsSupportedScalarType(unwrapped))
            {
                return true;
            }

            var namedType = unwrapped as INamedTypeSymbol;
            return namedType != null && IsAccessibleFromGeneratedCode(namedType);
        }

        private static bool CanAssignNull(ITypeSymbol type)
        {
            var namedType = type as INamedTypeSymbol;
            return type.IsReferenceType ||
                   namedType != null &&
                   namedType.ConstructedFrom != null &&
                   namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T;
        }

        private static bool HasPublicGetter(IPropertySymbol property)
        {
            return property.GetMethod != null &&
                   property.GetMethod.DeclaredAccessibility == Accessibility.Public &&
                   !property.GetMethod.IsStatic;
        }

        private static bool HasPublicSetter(IPropertySymbol property)
        {
            return property.SetMethod != null &&
                   property.SetMethod.DeclaredAccessibility == Accessibility.Public &&
                   !property.SetMethod.IsStatic;
        }

        private static bool IsMapInvocation(IMethodSymbol method)
        {
            return method != null &&
                   method.Name == "Map" &&
                   method.Parameters.Length == 1 &&
                   IsType(method.ContainingType.OriginalDefinition, MappingNamespace, "EntityMapBase`2");
        }

        private static bool IsIncludeBaseInvocation(IMethodSymbol method)
        {
            return method != null &&
                   method.Name == "IncludeBase" &&
                   method.IsGenericMethod &&
                   method.TypeArguments.Length == 1 &&
                   method.Parameters.Length == 0 &&
                   IsType(method.ContainingType.OriginalDefinition, MappingNamespace, "EntityMapBase`2");
        }

        private static bool IsToColumnInvocation(IMethodSymbol method)
        {
            return method != null &&
                   method.Name == "ToColumn" &&
                   method.Parameters.Length >= 1 &&
                   method.Parameters[0].Type.SpecialType == SpecialType.System_String;
        }

        private static bool IsIgnoreInvocation(IMethodSymbol method)
        {
            return method != null && method.Name == "Ignore" && method.Parameters.Length == 0;
        }

        private static bool IsReadNeutralPersistenceInvocation(IMethodSymbol method)
        {
            if (method == null || method.Parameters.Length != 0)
            {
                return false;
            }

            switch (method.Name)
            {
                case "ExcludeFromInsert":
                case "ExcludeFromUpdate":
                case "ReadOnly":
                case "Computed":
                case "DatabaseDefaultOnInsert":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsGeneratedReadConverterInvocation(IMethodSymbol method)
        {
            return method != null &&
                   (method.Name == "ConvertFromDatabaseUsing" || method.Name == "ConvertUsing") &&
                   method.IsGenericMethod &&
                   method.TypeArguments.Length == 2;
        }

        private static bool IsWriteOnlyConverterInvocation(IMethodSymbol method)
        {
            return method != null && method.Name == "ConvertToDatabaseUsing";
        }

        private static bool TryCreateReadConverterBinding(
            IMethodSymbol method,
            ITypeSymbol mappedPropertyType,
            out GeneratedReadConverterBinding binding,
            out string reason)
        {
            binding = null;
            reason = null;

            var converterType = method.TypeArguments[0] as INamedTypeSymbol;
            var databaseType = method.TypeArguments[1];
            if (converterType == null)
            {
                reason = "the converter type is not a named type";
                return false;
            }

            if (!HasPublicParameterlessConstructor(converterType))
            {
                reason = "the converter type does not have a public parameterless constructor";
                return false;
            }

            if (!IsAccessibleFromGeneratedCode(converterType))
            {
                reason = "the converter type is not accessible from generated code";
                return false;
            }

            var databaseMatches = converterType.AllInterfaces
                .Where(type => IsReadPropertyConverterInterface(type))
                .Where(type => IsSameOrNullableEquivalent(type.TypeArguments[0], databaseType))
                .ToList();

            if (databaseMatches.Count == 0)
            {
                reason = $"the converter does not implement IReadPropertyConverter<{FormatSymbol(databaseType)}, TProperty>";
                return false;
            }

            var propertyMatches = databaseMatches
                .Where(type => CanAssignValue(mappedPropertyType, type.TypeArguments[1]))
                .ToList();

            if (propertyMatches.Count == 0)
            {
                var converterPropertyType = databaseMatches[0].TypeArguments[1];
                reason = $"the converter returns '{FormatSymbol(converterPropertyType)}', which cannot be assigned to mapped property type '{FormatSymbol(mappedPropertyType)}'";
                return false;
            }

            if (propertyMatches.Count > 1)
            {
                reason = "the converter matches more than one compatible IReadPropertyConverter<TDatabase, TProperty> contract";
                return false;
            }

            var converterInterface = propertyMatches[0];
            binding = new GeneratedReadConverterBinding(
                converterType.ToDisplayString(FullyQualifiedTypeFormat),
                converterInterface.TypeArguments[0].ToDisplayString(FullyQualifiedTypeFormat),
                converterInterface.TypeArguments[1].ToDisplayString(FullyQualifiedTypeFormat));
            return true;
        }

        private static bool IsReadPropertyConverterInterface(INamedTypeSymbol type)
        {
            return type.OriginalDefinition.MetadataName == "IReadPropertyConverter`2" &&
                   type.OriginalDefinition.ContainingNamespace.ToDisplayString() == MappingNamespace;
        }

        private static bool CanAssignValue(ITypeSymbol targetType, ITypeSymbol valueType)
        {
            return IsSameOrNullableEquivalent(targetType, valueType) || IsAssignableFrom(targetType, valueType);
        }

        private static bool IsSameOrNullableEquivalent(ITypeSymbol left, ITypeSymbol right)
        {
            return SymbolEqualityComparer.Default.Equals(left, right) ||
                   SymbolEqualityComparer.Default.Equals(UnwrapNullable(left), right) ||
                   SymbolEqualityComparer.Default.Equals(UnwrapNullable(right), left);
        }

        private static bool IsEntityMapInterface(INamedTypeSymbol type)
        {
            return type.OriginalDefinition.MetadataName == "IEntityMap`1" &&
                   type.OriginalDefinition.ContainingNamespace.ToDisplayString() == MappingNamespace;
        }

        private static bool IsProfileMapInterface(INamedTypeSymbol type)
        {
            return type.OriginalDefinition.MetadataName == "IProfileMap`1" &&
                   type.OriginalDefinition.ContainingNamespace.ToDisplayString() == MappingNamespace;
        }

        private static bool ContainsGenericParameters(INamedTypeSymbol type)
        {
            if (type.IsGenericType && type.TypeArguments.Any(argument => argument.Kind == SymbolKind.TypeParameter))
            {
                return true;
            }

            for (var containingType = type.ContainingType; containingType != null; containingType = containingType.ContainingType)
            {
                if (containingType.TypeParameters.Length != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAccessibleFromGeneratedCode(INamedTypeSymbol type)
        {
            for (var current = type; current != null; current = current.ContainingType)
            {
                if (current.DeclaredAccessibility != Accessibility.Public &&
                    current.DeclaredAccessibility != Accessibility.Internal)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasPublicParameterlessConstructor(INamedTypeSymbol type)
        {
            return type.InstanceConstructors.Any(constructor =>
                constructor.Parameters.Length == 0 &&
                constructor.DeclaredAccessibility == Accessibility.Public);
        }

        private static int GetInheritanceDepth(INamedTypeSymbol type)
        {
            var depth = 0;
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                depth++;
            }

            return depth;
        }

        private static bool IsType(INamedTypeSymbol type, string namespaceName, string metadataName)
        {
            return type != null &&
                   type.MetadataName == metadataName &&
                   type.ContainingNamespace.ToDisplayString() == namespaceName;
        }

        private static string FormatSymbol(ISymbol symbol)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        }

        private static string EscapeStringLiteral(string value)
        {
            var builder = new StringBuilder();
            builder.Append('"');
            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '\\':
                        builder.Append(@"\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\r':
                        builder.Append(@"\r");
                        break;
                    case '\n':
                        builder.Append(@"\n");
                        break;
                    case '\t':
                        builder.Append(@"\t");
                        break;
                    default:
                        builder.Append(ch);
                        break;
                }
            }

            builder.Append('"');
            return builder.ToString();
        }

        private static string EscapeIdentifier(string name)
        {
            return SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ||
                   SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None
                ? "@" + name
                : name;
        }

        private static string CreateUniqueLocalName(string name, int index)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "arg" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            return EscapeIdentifier(name);
        }

        private sealed class MapCandidate
        {
            private MapCandidate(
                MapCandidateKind kind,
                string mapDisplayName,
                string mapTypeName,
                string entityTypeName,
                string profileTypeName,
                int entityInheritanceDepth,
                Location location,
                string skipReason,
                GeneratedMaterializerInfo materializer,
                string materializerSkipReason,
                GeneratedDiagnostic materializerDiagnostic)
            {
                Kind = kind;
                MapDisplayName = mapDisplayName;
                MapTypeName = mapTypeName;
                EntityTypeName = entityTypeName;
                ProfileTypeName = profileTypeName;
                EntityInheritanceDepth = entityInheritanceDepth;
                Location = location;
                SkipReason = skipReason;
                Materializer = materializer;
                MaterializerSkipReason = materializerSkipReason;
                MaterializerDiagnostic = materializerDiagnostic;
            }

            internal MapCandidateKind Kind { get; }

            internal string MapDisplayName { get; }

            internal string MapTypeName { get; }

            internal string EntityTypeName { get; }

            internal string ProfileTypeName { get; }

            internal string ProfileKey => EntityTypeName + "|" + ProfileTypeName;

            internal int EntityInheritanceDepth { get; }

            internal Location Location { get; }

            internal string SkipReason { get; }

            internal GeneratedMaterializerInfo Materializer { get; }

            internal string MaterializerSkipReason { get; }

            internal GeneratedDiagnostic MaterializerDiagnostic { get; }

            internal static MapCandidate Valid(
                string mapDisplayName,
                string mapTypeName,
                string entityTypeName,
                string profileTypeName,
                int entityInheritanceDepth,
                Location location,
                GeneratedMaterializerInfo materializer,
                string materializerSkipReason,
                GeneratedDiagnostic materializerDiagnostic)
            {
                return new MapCandidate(
                    MapCandidateKind.Valid,
                    mapDisplayName,
                    mapTypeName,
                    entityTypeName,
                    profileTypeName,
                    entityInheritanceDepth,
                    location,
                    null,
                    materializer,
                    materializerSkipReason,
                    materializerDiagnostic);
            }

            internal static MapCandidate InvalidRegistration(string mapDisplayName, Location location)
            {
                return new MapCandidate(
                    MapCandidateKind.InvalidRegistration,
                    mapDisplayName,
                    null,
                    null,
                    null,
                    0,
                    location,
                    null,
                    null,
                    null,
                    null);
            }

            internal static MapCandidate Skipped(string mapDisplayName, Location location, string reason)
            {
                return new MapCandidate(
                    MapCandidateKind.Skipped,
                    mapDisplayName,
                    null,
                    null,
                    null,
                    0,
                    location,
                    reason,
                    null,
                    null,
                    null);
            }
        }

        private enum MapCandidateKind
        {
            Valid,
            InvalidRegistration,
            Skipped
        }

        private sealed class GeneratedDiagnostic
        {
            private GeneratedDiagnostic(DiagnosticDescriptor descriptor, Location location, object[] arguments)
            {
                Descriptor = descriptor;
                Location = location;
                Arguments = arguments;
            }

            internal DiagnosticDescriptor Descriptor { get; }

            internal Location Location { get; }

            internal object[] Arguments { get; }

            internal static GeneratedDiagnostic InvalidReadConverter(
                Location location,
                string mapTypeName,
                string converterTypeName,
                string reason)
            {
                return new GeneratedDiagnostic(
                    InvalidGeneratedReadConverterRule,
                    location,
                    new object[] { mapTypeName, converterTypeName, reason });
            }
        }

        private sealed class GeneratedReadConverterBinding
        {
            internal GeneratedReadConverterBinding(
                string converterTypeName,
                string databaseTypeName,
                string propertyTypeName)
            {
                ConverterTypeName = converterTypeName;
                DatabaseTypeName = databaseTypeName;
                PropertyTypeName = propertyTypeName;
            }

            internal string ConverterTypeName { get; }

            internal string DatabaseTypeName { get; }

            internal string PropertyTypeName { get; }
        }

        private sealed class GeneratedMapInvocation
        {
            internal GeneratedMapInvocation(
                GeneratedMemberPath memberPath,
                string columnName,
                bool ignored,
                GeneratedReadConverterBinding readConverter)
            {
                MemberPath = memberPath;
                ColumnName = columnName;
                Ignored = ignored;
                ReadConverter = readConverter;
            }

            internal GeneratedMemberPath MemberPath { get; }

            internal string ColumnName { get; }

            internal bool Ignored { get; }

            internal GeneratedReadConverterBinding ReadConverter { get; }
        }

        private sealed class GeneratedMemberPath
        {
            private GeneratedMemberPath(IList<IPropertySymbol> properties, string display, string terminalName)
            {
                Properties = properties;
                Display = display;
                TerminalName = terminalName;
            }

            internal IList<IPropertySymbol> Properties { get; }

            internal string Display { get; }

            internal string TerminalName { get; }

            internal static GeneratedMemberPath Create(IEnumerable<IPropertySymbol> properties)
            {
                var propertyList = properties.ToList();
                return new GeneratedMemberPath(
                    propertyList,
                    string.Join(".", propertyList.Select(property => property.Name)),
                    propertyList[propertyList.Count - 1].Name);
            }
        }

        private sealed class GeneratedMaterializerInfo
        {
            internal GeneratedMaterializerInfo(
                string mapTypeName,
                string entityTypeName,
                string profileTypeName,
                IReadOnlyList<GeneratedColumnBinding> columns,
                GeneratedMaterializationNode root,
                string methodName)
            {
                MapTypeName = mapTypeName;
                EntityTypeName = entityTypeName;
                ProfileTypeName = profileTypeName;
                Columns = columns;
                Root = root;
                MethodName = methodName;
            }

            internal string MapTypeName { get; }

            internal string EntityTypeName { get; }

            internal string ProfileTypeName { get; }

            internal IReadOnlyList<GeneratedColumnBinding> Columns { get; }

            internal GeneratedMaterializationNode Root { get; }

            internal string MethodName { get; }

            internal GeneratedMaterializerInfo WithMethodName(string methodName)
            {
                return new GeneratedMaterializerInfo(
                    MapTypeName,
                    EntityTypeName,
                    ProfileTypeName,
                    Columns,
                    Root,
                    methodName);
            }
        }

        private sealed class GeneratedColumnBinding
        {
            internal GeneratedColumnBinding(
                string columnName,
                string memberPath,
                bool ignored,
                GeneratedReadConverterBinding readConverter)
            {
                ColumnName = columnName;
                MemberPath = memberPath;
                Ignored = ignored;
                ReadConverter = readConverter;
            }

            internal string ColumnName { get; }

            internal string MemberPath { get; }

            internal bool Ignored { get; }

            internal GeneratedReadConverterBinding ReadConverter { get; }
        }

        private sealed class GeneratedPropertyBinding
        {
            internal GeneratedPropertyBinding(
                int ordinal,
                string columnName,
                string memberPath,
                string propertyName,
                string typeName,
                bool hasPublicSetter,
                ITypeSymbol propertyTypeSymbol,
                GeneratedReadConverterBinding readConverter)
            {
                Ordinal = ordinal;
                ColumnName = columnName;
                MemberPath = memberPath;
                PropertyName = propertyName;
                TypeName = typeName;
                HasPublicSetter = hasPublicSetter;
                PropertyTypeSymbol = propertyTypeSymbol;
                ReadConverter = readConverter;
            }

            internal int Ordinal { get; }

            internal string ColumnName { get; }

            internal string MemberPath { get; }

            internal string PropertyName { get; }

            internal string TypeName { get; }

            internal bool HasPublicSetter { get; }

            internal bool CanAssign => HasPublicSetter;

            internal ITypeSymbol PropertyTypeSymbol { get; }

            internal GeneratedReadConverterBinding ReadConverter { get; }
        }

        private sealed class GeneratedMaterializationNode
        {
            private readonly List<GeneratedPropertyBinding> _leaves = new List<GeneratedPropertyBinding>();
            private readonly List<GeneratedMaterializationNode> _children = new List<GeneratedMaterializationNode>();

            internal GeneratedMaterializationNode(
                int id,
                ITypeSymbol type,
                IPropertySymbol parentProperty,
                string memberPath,
                bool isRoot)
            {
                Id = id;
                Type = type;
                TypeName = type.ToDisplayString(FullyQualifiedTypeFormat);
                ParentProperty = parentProperty;
                PropertyName = parentProperty == null ? null : parentProperty.Name;
                PropertyTypeSymbol = parentProperty == null ? type : parentProperty.Type;
                MemberPath = memberPath;
                IsRoot = isRoot;
                HasPublicGetter = parentProperty != null && MappingRegistrationGenerator.HasPublicGetter(parentProperty);
                HasPublicSetter = parentProperty != null && MappingRegistrationGenerator.HasPublicSetter(parentProperty);
                CanAssignNull = parentProperty == null || MappingRegistrationGenerator.CanAssignNull(parentProperty.Type);
            }

            internal int Id { get; }

            internal ITypeSymbol Type { get; }

            internal string TypeName { get; }

            internal IPropertySymbol ParentProperty { get; }

            internal string PropertyName { get; }

            internal ITypeSymbol PropertyTypeSymbol { get; }

            internal string MemberPath { get; }

            internal bool IsRoot { get; }

            internal bool HasPublicGetter { get; }

            internal bool HasPublicSetter { get; }

            internal bool CanAssignNull { get; }

            internal bool CanAssignToParent => IsRoot || HasPublicSetter;

            internal IReadOnlyList<GeneratedPropertyBinding> Leaves => _leaves;

            internal IReadOnlyList<GeneratedMaterializationNode> Children => _children;

            internal GeneratedConstructorBinding Constructor { get; private set; }

            internal IReadOnlyList<GeneratedPropertyBinding> PostConstructorLeaves { get; private set; }

            internal IReadOnlyList<GeneratedMaterializationNode> PostConstructorChildren { get; private set; }

            internal IReadOnlyList<int> SubtreeOrdinals { get; private set; }

            internal GeneratedMaterializationNode FindOrAddChild(IPropertySymbol property, ref int nextNodeId)
            {
                var existing = _children.FirstOrDefault(candidate => SymbolEqualityComparer.Default.Equals(candidate.ParentProperty, property));
                if (existing != null)
                {
                    return existing;
                }

                var memberPath = IsRoot
                    ? property.Name
                    : MemberPath + "." + property.Name;
                var createdChild = new GeneratedMaterializationNode(nextNodeId++, property.Type, property, memberPath, isRoot: false);
                _children.Add(createdChild);
                return createdChild;
            }

            internal void AddLeaf(GeneratedPropertyBinding leaf)
            {
                _leaves.Add(leaf);
            }

            internal bool Seal(INamedTypeSymbol entityType, out string skipReason)
            {
                foreach (var child in _children)
                {
                    if (!child.Seal(entityType, out skipReason))
                    {
                        return false;
                    }
                }

                SubtreeOrdinals = _leaves
                    .Select(leaf => leaf.Ordinal)
                    .Concat(_children.SelectMany(child => child.SubtreeOrdinals))
                    .Distinct()
                    .ToArray();

                var hasParameterlessConstructor = Type is INamedTypeSymbol namedType &&
                                                  MappingRegistrationGenerator.HasPublicParameterlessConstructor(namedType);
                var requiresConstructor = !hasParameterlessConstructor ||
                                          _leaves.Any(leaf => !leaf.CanAssign) ||
                                          _children.Any(child => !child.CanAssignToParent);

                if (!requiresConstructor)
                {
                    Constructor = null;
                    PostConstructorLeaves = _leaves.ToArray();
                    PostConstructorChildren = _children.ToArray();
                    skipReason = null;
                    return true;
                }

                if (!(Type is INamedTypeSymbol))
                {
                    skipReason = $"type '{FormatSymbol(Type)}' at member path '{MemberPath}' is not supported by generated constructor materialization";
                    return false;
                }

                if (!TryCreateConstructorBinding(entityType, this, out var constructor, out skipReason))
                {
                    return false;
                }

                Constructor = constructor;
                PostConstructorLeaves = _leaves
                    .Where(leaf => !constructor.Uses(leaf))
                    .ToArray();
                PostConstructorChildren = _children
                    .Where(child => !constructor.Uses(child))
                    .ToArray();

                var unsupportedLeaf = PostConstructorLeaves.FirstOrDefault(leaf => !leaf.CanAssign);
                if (unsupportedLeaf != null)
                {
                    skipReason = $"type '{FormatSymbol(Type)}' at member path '{MemberPath}' cannot assign mapped property '{unsupportedLeaf.MemberPath}' in generated materialization";
                    return false;
                }

                var unsupportedChild = PostConstructorChildren.FirstOrDefault(child => !child.CanAssignToParent);
                if (unsupportedChild != null)
                {
                    skipReason = $"type '{FormatSymbol(Type)}' at member path '{MemberPath}' cannot assign nested object '{unsupportedChild.MemberPath}' in generated materialization";
                    return false;
                }

                skipReason = null;
                return true;
            }

            internal IEnumerable<string> GetColumnNames()
            {
                return _leaves.Select(leaf => leaf.ColumnName)
                    .Concat(_children.SelectMany(child => child.GetColumnNames()));
            }

            internal IEnumerable<GeneratedPropertyBinding> GetLeaves()
            {
                return _leaves.Concat(_children.SelectMany(child => child.GetLeaves()));
            }
        }

        private sealed class GeneratedConstructorBinding
        {
            internal GeneratedConstructorBinding(
                IMethodSymbol constructor,
                IReadOnlyList<GeneratedConstructorParameter> parameters,
                int score)
            {
                Constructor = constructor;
                Parameters = parameters;
                Score = score;
            }

            internal IMethodSymbol Constructor { get; }

            internal IReadOnlyList<GeneratedConstructorParameter> Parameters { get; }

            internal int Score { get; }

            internal bool Uses(GeneratedPropertyBinding leaf)
            {
                return Parameters.Any(parameter => parameter.Leaf == leaf);
            }

            internal bool Uses(GeneratedMaterializationNode child)
            {
                return Parameters.Any(parameter => parameter.Child == child);
            }
        }

        private sealed class GeneratedConstructorParameter
        {
            internal GeneratedConstructorParameter(
                string localName,
                string typeName,
                GeneratedPropertyBinding leaf,
                GeneratedMaterializationNode child)
            {
                LocalName = localName;
                TypeName = typeName;
                Leaf = leaf;
                Child = child;
            }

            internal string LocalName { get; }

            internal string TypeName { get; }

            internal GeneratedPropertyBinding Leaf { get; }

            internal GeneratedMaterializationNode Child { get; }
        }

        private sealed class GeneratedParameterMatch
        {
            private GeneratedParameterMatch(
                GeneratedPropertyBinding leaf,
                GeneratedMaterializationNode child,
                int score)
            {
                Leaf = leaf;
                Child = child;
                Score = score;
            }

            internal GeneratedPropertyBinding Leaf { get; }

            internal GeneratedMaterializationNode Child { get; }

            internal int Score { get; }

            internal static GeneratedParameterMatch ForLeaf(GeneratedPropertyBinding leaf, int score)
            {
                return new GeneratedParameterMatch(leaf, null, score);
            }

            internal static GeneratedParameterMatch ForChild(GeneratedMaterializationNode child, int score)
            {
                return new GeneratedParameterMatch(null, child, score);
            }
        }
    }
}
