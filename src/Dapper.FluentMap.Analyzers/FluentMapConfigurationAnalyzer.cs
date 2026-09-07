using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapper.FluentMap.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class FluentMapConfigurationAnalyzer : DiagnosticAnalyzer
    {
        public const string InvalidMapExpressionDiagnosticId = "DFM001";
        public const string DuplicateMemberPathDiagnosticId = "DFM002";
        public const string DuplicateColumnDiagnosticId = "DFM003";
        public const string InvalidIncludeBaseDiagnosticId = "DFM004";
        public const string InvalidGenericMapRegistrationDiagnosticId = "DFM005";
        public const string InvalidGenericProfileRegistrationDiagnosticId = "DFM009";
        public const string DuplicateProfileRegistrationDiagnosticId = "DFM010";
        public const string InvalidPersistenceBehaviorDiagnosticId = "DFM013";
        public const string InvalidPropertyConverterDiagnosticId = "DFM014";
        public const string DuplicatePropertyConverterDiagnosticId = "DFM015";

        private const string Category = "Dapper.FluentMap.Configuration";
        private const string MappingNamespace = "Dapper.FluentMap.Mapping";
        private const string ConfigurationNamespace = "Dapper.FluentMap.Configuration";
        private const string DommelMappingNamespace = "Dapper.FluentMap.Dommel.Mapping";

        private static readonly DiagnosticDescriptor InvalidMapExpressionRule = new DiagnosticDescriptor(
            InvalidMapExpressionDiagnosticId,
            "Map expression must resolve to a property path",
            "Map expression '{0}' is invalid: {1}",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Dapper.FluentMap Map expressions must resolve to a property path rooted in the entity parameter.");

        private static readonly DiagnosticDescriptor DuplicateMemberPathRule = new DiagnosticDescriptor(
            DuplicateMemberPathDiagnosticId,
            "Property path is mapped more than once",
            "Property path '{0}' is mapped more than once in this entity map constructor",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Mapping the same property path more than once in the same entity map constructor is an invalid FluentMap configuration.",
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        private static readonly DiagnosticDescriptor DuplicateColumnRule = new DiagnosticDescriptor(
            DuplicateColumnDiagnosticId,
            "Column is mapped by more than one property path",
            "Column '{0}' is mapped by more than one property path in this entity map constructor: '{1}' and '{2}'",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Two explicit FluentMap mappings in the same entity map constructor must not resolve the same column when that conflict is statically known.",
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        private static readonly DiagnosticDescriptor InvalidIncludeBaseRule = new DiagnosticDescriptor(
            InvalidIncludeBaseDiagnosticId,
            "Included mapping type must be a base class",
            "Type '{0}' cannot be included as a base mapping for entity '{1}'",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "IncludeBase<TBase>() can only include a real base class of the entity mapped by the current EntityMap.");

        private static readonly DiagnosticDescriptor InvalidGenericMapRegistrationRule = new DiagnosticDescriptor(
            InvalidGenericMapRegistrationDiagnosticId,
            "Generic map registration type is invalid",
            "Entity map type '{0}' must implement exactly one closed IEntityMap<TEntity> interface targeting a class type",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "AddMap<TMap>() can only register map types that implement exactly one closed IEntityMap<TEntity> interface whose entity type is a class.");

        private static readonly DiagnosticDescriptor InvalidGenericProfileRegistrationRule = new DiagnosticDescriptor(
            InvalidGenericProfileRegistrationDiagnosticId,
            "Generic profile registration type is invalid",
            "Profile map type '{0}' must implement exactly one closed IEntityMap<TEntity> interface and exactly one closed IProfileMap<TProfile> interface",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "AddProfile<TMap>() can only register map types that implement one entity map interface and one mapping profile interface.");

        private static readonly DiagnosticDescriptor DuplicateProfileRegistrationRule = new DiagnosticDescriptor(
            DuplicateProfileRegistrationDiagnosticId,
            "Mapping profile is registered more than once",
            "Entity '{0}' registers mapping profile '{1}' more than once in this configuration method",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "The same entity/profile pair must not be registered more than once.",
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        private static readonly DiagnosticDescriptor InvalidPersistenceBehaviorRule = new DiagnosticDescriptor(
            InvalidPersistenceBehaviorDiagnosticId,
            "Persistence mapping behavior is invalid",
            "Property path '{0}' has invalid persistence behavior: {1}",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Persistence mapping calls such as Ignore, Computed, DatabaseDefaultOnInsert, key and identity must not be combined in contradictory ways.");

        private static readonly DiagnosticDescriptor InvalidPropertyConverterRule = new DiagnosticDescriptor(
            InvalidPropertyConverterDiagnosticId,
            "Property converter is invalid",
            "Property path '{0}' has invalid {1} converter '{2}': {3}",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Type-based FluentMap property converters must implement a compatible read or write converter contract for the mapped property path.");

        private static readonly DiagnosticDescriptor DuplicatePropertyConverterRule = new DiagnosticDescriptor(
            DuplicatePropertyConverterDiagnosticId,
            "Property converter is configured more than once",
            "Property path '{0}' configures more than one {1} converter in this fluent chain",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "A single FluentMap property mapping can have at most one read converter and at most one write converter.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                InvalidMapExpressionRule,
                DuplicateMemberPathRule,
                DuplicateColumnRule,
                InvalidIncludeBaseRule,
                InvalidGenericMapRegistrationRule,
                InvalidGenericProfileRegistrationRule,
                DuplicateProfileRegistrationRule,
                InvalidPersistenceBehaviorRule,
                InvalidPropertyConverterRule,
                DuplicatePropertyConverterRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(startContext =>
            {
                var constructorMapInvocations = new ConcurrentBag<MapInvocation>();
                var profileRegistrations = new ConcurrentBag<ProfileRegistrationInvocation>();

                startContext.RegisterSyntaxNodeAction(
                    nodeContext => AnalyzeInvocation(nodeContext, constructorMapInvocations, profileRegistrations),
                    SyntaxKind.InvocationExpression);

                startContext.RegisterCompilationEndAction(
                    endContext =>
                    {
                        AnalyzeConstructorMapInvocations(endContext, constructorMapInvocations);
                        AnalyzeProfileRegistrations(endContext, profileRegistrations);
                    });
            });
        }

        private static void AnalyzeInvocation(
            SyntaxNodeAnalysisContext context,
            ConcurrentBag<MapInvocation> constructorMapInvocations,
            ConcurrentBag<ProfileRegistrationInvocation> profileRegistrations)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;

            if (method == null)
            {
                return;
            }

            if (IsMapInvocation(method))
            {
                AnalyzeMapInvocation(context, invocation, constructorMapInvocations);
                return;
            }

            if (IsIncludeBaseInvocation(method))
            {
                AnalyzeIncludeBaseInvocation(context, invocation, method);
                return;
            }

            if (IsGenericAddMapInvocation(method))
            {
                AnalyzeGenericAddMapInvocation(context, invocation, method);
                return;
            }

            if (IsGenericAddProfileInvocation(method))
            {
                AnalyzeGenericAddProfileInvocation(context, invocation, method, profileRegistrations);
            }
        }

        private static void AnalyzeMapInvocation(
            SyntaxNodeAnalysisContext context,
            InvocationExpressionSyntax invocation,
            ConcurrentBag<MapInvocation> constructorMapInvocations)
        {
            if (invocation.ArgumentList.Arguments.Count != 1)
            {
                return;
            }

            var argument = invocation.ArgumentList.Arguments[0].Expression;
            if (!TryGetLambda(argument, out var lambda))
            {
                return;
            }

            if (!TryCreateMemberPath(lambda.Body, context.SemanticModel, context.CancellationToken, out var memberPath, out var reason))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidMapExpressionRule,
                    lambda.Body.GetLocation(),
                    lambda.Body.ToString(),
                    reason));
                return;
            }

            if (!TryCreateDirectConstructorMapInvocation(
                    invocation,
                    context.SemanticModel,
                    memberPath,
                    context,
                    context.CancellationToken,
                    out var mapInvocation))
            {
                return;
            }

            constructorMapInvocations.Add(mapInvocation);
        }

        private static void AnalyzeIncludeBaseInvocation(
            SyntaxNodeAnalysisContext context,
            InvocationExpressionSyntax invocation,
            IMethodSymbol method)
        {
            if (method.TypeArguments.Length != 1)
            {
                return;
            }

            var containingType = context.ContainingSymbol?.ContainingType;
            if (containingType == null)
            {
                return;
            }

            var entityType = FindEntityType(containingType);
            var baseType = method.TypeArguments[0] as INamedTypeSymbol;
            if (entityType == null || baseType == null)
            {
                return;
            }

            if (baseType.TypeKind == TypeKind.Class &&
                !SymbolEqualityComparer.Default.Equals(baseType, entityType) &&
                IsAssignableTo(entityType, baseType))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                InvalidIncludeBaseRule,
                invocation.GetLocation(),
                FormatSymbol(baseType),
                FormatSymbol(entityType)));
        }

        private static void AnalyzeGenericAddMapInvocation(
            SyntaxNodeAnalysisContext context,
            InvocationExpressionSyntax invocation,
            IMethodSymbol method)
        {
            if (method.TypeArguments.Length != 1)
            {
                return;
            }

            var mapType = method.TypeArguments[0] as INamedTypeSymbol;
            if (mapType == null)
            {
                return;
            }

            if (TryGetEntityMapInterface(mapType, out _))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                InvalidGenericMapRegistrationRule,
                invocation.GetLocation(),
                FormatSymbol(mapType)));
        }

        private static void AnalyzeGenericAddProfileInvocation(
            SyntaxNodeAnalysisContext context,
            InvocationExpressionSyntax invocation,
            IMethodSymbol method,
            ConcurrentBag<ProfileRegistrationInvocation> profileRegistrations)
        {
            if (method.TypeArguments.Length != 1)
            {
                return;
            }

            var mapType = method.TypeArguments[0] as INamedTypeSymbol;
            if (mapType == null)
            {
                return;
            }

            if (!TryGetEntityMapInterface(mapType, out var entityType) ||
                !TryGetProfileMapInterface(mapType, out var profileType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidGenericProfileRegistrationRule,
                    invocation.GetLocation(),
                    FormatSymbol(mapType)));
                return;
            }

            if (context.ContainingSymbol != null)
            {
                profileRegistrations.Add(new ProfileRegistrationInvocation(
                    context.ContainingSymbol,
                    entityType,
                    profileType,
                    GetInvocationNameLocation(invocation)));
            }
        }

        private static void AnalyzeConstructorMapInvocations(
            CompilationAnalysisContext context,
            ConcurrentBag<MapInvocation> constructorMapInvocations)
        {
            var groups = constructorMapInvocations
                .GroupBy(invocation => invocation.Constructor, SymbolEqualityComparer.Default);

            foreach (var group in groups)
            {
                var invocations = group
                    .OrderBy(invocation => invocation.InvocationLocation.SourceSpan.Start)
                    .ToList();

                ReportDuplicateMemberPaths(context, invocations);
                ReportDuplicateColumns(context, invocations);
            }
        }

        private static void ReportDuplicateMemberPaths(
            CompilationAnalysisContext context,
            IList<MapInvocation> invocations)
        {
            var seen = new Dictionary<string, MapInvocation>(StringComparer.Ordinal);

            foreach (var invocation in invocations)
            {
                if (seen.ContainsKey(invocation.MemberPath.Key))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DuplicateMemberPathRule,
                        invocation.InvocationLocation,
                        invocation.MemberPath.Display));
                    continue;
                }

                seen.Add(invocation.MemberPath.Key, invocation);
            }
        }

        private static void ReportDuplicateColumns(
            CompilationAnalysisContext context,
            IList<MapInvocation> invocations)
        {
            for (var i = 0; i < invocations.Count; i++)
            {
                var left = invocations[i];
                if (!left.ColumnKnown || left.Ignored)
                {
                    continue;
                }

                for (var j = i + 1; j < invocations.Count; j++)
                {
                    var right = invocations[j];
                    if (!right.ColumnKnown ||
                        right.Ignored ||
                        left.MemberPath.Key == right.MemberPath.Key ||
                        !ColumnNamesOverlap(left, right))
                    {
                        continue;
                    }

                    context.ReportDiagnostic(Diagnostic.Create(
                        DuplicateColumnRule,
                        right.ColumnLocation,
                        right.ColumnName,
                        left.MemberPath.Display,
                        right.MemberPath.Display));
                }
            }
        }

        private static void AnalyzeProfileRegistrations(
            CompilationAnalysisContext context,
            ConcurrentBag<ProfileRegistrationInvocation> profileRegistrations)
        {
            var groups = profileRegistrations
                .GroupBy(
                    registration => registration.ContainingSymbol,
                    SymbolEqualityComparer.Default);

            foreach (var group in groups)
            {
                var seen = new Dictionary<string, ProfileRegistrationInvocation>(StringComparer.Ordinal);
                foreach (var registration in group.OrderBy(item => item.Location.SourceSpan.Start))
                {
                    var key = FormatSymbol(registration.EntityType) + "|" + FormatSymbol(registration.ProfileType);
                    if (seen.ContainsKey(key))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DuplicateProfileRegistrationRule,
                            registration.Location,
                            FormatSymbol(registration.EntityType),
                            FormatSymbol(registration.ProfileType)));
                        continue;
                    }

                    seen.Add(key, registration);
                }
            }
        }

        private static bool ColumnNamesOverlap(MapInvocation left, MapInvocation right)
        {
            if (string.Equals(left.ColumnName, right.ColumnName, StringComparison.Ordinal))
            {
                return true;
            }

            return (!left.CaseSensitive || !right.CaseSensitive) &&
                   string.Equals(left.ColumnName, right.ColumnName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryCreateDirectConstructorMapInvocation(
            InvocationExpressionSyntax mapInvocation,
            SemanticModel semanticModel,
            MemberPathInfo memberPath,
            SyntaxNodeAnalysisContext context,
            System.Threading.CancellationToken cancellationToken,
            out MapInvocation result)
        {
            result = null;

            var statement = mapInvocation.FirstAncestorOrSelf<ExpressionStatementSyntax>();
            var block = statement?.Parent as BlockSyntax;
            var constructor = block?.Parent as ConstructorDeclarationSyntax;
            if (statement == null || constructor == null)
            {
                return false;
            }

            var constructorSymbol = semanticModel.GetDeclaredSymbol(constructor, cancellationToken);
            if (constructorSymbol == null)
            {
                return false;
            }

            var column = memberPath.TerminalName;
            var columnKnown = true;
            var caseSensitive = true;
            var ignored = false;
            var columnLocation = mapInvocation.GetLocation();
            var persistenceState = new PersistenceChainState();
            var conversionState = new ConversionChainState();

            SyntaxNode current = mapInvocation;
            while (current.Parent is MemberAccessExpressionSyntax memberAccess &&
                   memberAccess.Expression == current &&
                   memberAccess.Parent is InvocationExpressionSyntax chainedInvocation)
            {
                var chainedMethod = semanticModel.GetSymbolInfo(chainedInvocation, cancellationToken).Symbol as IMethodSymbol;
                if (chainedMethod == null)
                {
                    return false;
                }

                if (IsToColumnInvocation(chainedMethod))
                {
                    columnLocation = chainedInvocation.GetLocation();
                    if (!TryGetColumn(chainedInvocation, semanticModel, cancellationToken, out column, out caseSensitive))
                    {
                        columnKnown = false;
                    }
                }
                else if (IsIgnoreInvocation(chainedMethod))
                {
                    ignored = true;
                    persistenceState.ApplyIgnore();
                }
                else if (TryGetPersistenceAction(chainedMethod, chainedInvocation, semanticModel, cancellationToken, out var persistenceAction))
                {
                    if (!persistenceState.TryApply(persistenceAction, out var reason))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            InvalidPersistenceBehaviorRule,
                            GetInvocationNameLocation(chainedInvocation),
                            memberPath.Display,
                            reason));
                    }
                }
                else if (TryGetConversionAction(chainedMethod, out var conversionAction))
                {
                    foreach (var direction in conversionAction.Directions)
                    {
                        if (!conversionState.TryApply(direction))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                DuplicatePropertyConverterRule,
                                GetInvocationNameLocation(chainedInvocation),
                                memberPath.Display,
                                FormatDirection(direction)));
                        }
                    }

                    if (IsTypeBasedConversionInvocation(chainedMethod))
                    {
                        ValidateTypeBasedConverter(
                            context,
                            chainedInvocation,
                            chainedMethod,
                            memberPath,
                            conversionAction);
                    }
                }

                current = chainedInvocation;
            }

            if (current != statement.Expression)
            {
                return false;
            }

            result = new MapInvocation(
                constructorSymbol,
                memberPath,
                column,
                columnKnown,
                caseSensitive,
                ignored,
                mapInvocation.GetLocation(),
                columnLocation);
            return true;
        }

        private static bool TryGetColumn(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken,
            out string column,
            out bool caseSensitive)
        {
            column = null;
            caseSensitive = true;

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

            foreach (var argument in invocation.ArgumentList.Arguments.Skip(1))
            {
                var name = argument.NameColon?.Name.Identifier.ValueText;
                if (name != null && name != "caseSensitive")
                {
                    continue;
                }

                var caseConstant = semanticModel.GetConstantValue(argument.Expression, cancellationToken);
                if (!caseConstant.HasValue || !(caseConstant.Value is bool caseValue))
                {
                    return false;
                }

                caseSensitive = caseValue;
                return true;
            }

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
            out MemberPathInfo memberPath,
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
                        memberPath = MemberPathInfo.Create(properties);
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

        private static bool IsMapInvocation(IMethodSymbol method)
        {
            return method.Name == "Map" &&
                   method.Parameters.Length == 1 &&
                   IsType(method.ContainingType.OriginalDefinition, MappingNamespace, "EntityMapBase`2");
        }

        private static bool IsIncludeBaseInvocation(IMethodSymbol method)
        {
            return method.Name == "IncludeBase" &&
                   method.IsGenericMethod &&
                   method.TypeArguments.Length == 1 &&
                   method.Parameters.Length == 0 &&
                   IsType(method.ContainingType.OriginalDefinition, MappingNamespace, "EntityMapBase`2");
        }

        private static bool IsGenericAddMapInvocation(IMethodSymbol method)
        {
            return method.Name == "AddMap" &&
                   method.IsGenericMethod &&
                   method.TypeArguments.Length == 1 &&
                   method.Parameters.Length == 0 &&
                   IsType(method.ContainingType, ConfigurationNamespace, "FluentMapConfiguration");
        }

        private static bool IsGenericAddProfileInvocation(IMethodSymbol method)
        {
            return method.Name == "AddProfile" &&
                   method.IsGenericMethod &&
                   method.TypeArguments.Length == 1 &&
                   method.Parameters.Length == 0 &&
                   IsType(method.ContainingType, ConfigurationNamespace, "FluentMapConfiguration");
        }

        private static bool IsToColumnInvocation(IMethodSymbol method)
        {
            return method.Name == "ToColumn" &&
                   method.Parameters.Length >= 1 &&
                   method.Parameters[0].Type.SpecialType == SpecialType.System_String;
        }

        private static bool IsIgnoreInvocation(IMethodSymbol method)
        {
            return method.Name == "Ignore" && method.Parameters.Length == 0;
        }

        private static bool TryGetPersistenceAction(
            IMethodSymbol method,
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken,
            out PersistenceAction action)
        {
            action = PersistenceAction.None;

            if (method == null || !IsPersistenceMethod(method))
            {
                return false;
            }

            if (method.Parameters.Length == 0)
            {
                switch (method.Name)
                {
                    case "ExcludeFromInsert":
                        action = PersistenceAction.ExcludeFromInsert;
                        return true;
                    case "ExcludeFromUpdate":
                        action = PersistenceAction.ExcludeFromUpdate;
                        return true;
                    case "ReadOnly":
                        action = PersistenceAction.ReadOnly;
                        return true;
                    case "Computed":
                        action = PersistenceAction.Computed;
                        return true;
                    case "DatabaseDefaultOnInsert":
                        action = PersistenceAction.DatabaseDefaultOnInsert;
                        return true;
                    case "IsKey":
                        action = PersistenceAction.Key;
                        return true;
                    case "IsIdentity":
                        action = PersistenceAction.Identity;
                        return true;
                }
            }

            if (method.Name == "SetGeneratedOption" &&
                method.Parameters.Length == 1 &&
                invocation.ArgumentList.Arguments.Count == 1)
            {
                var option = semanticModel.GetConstantValue(
                    invocation.ArgumentList.Arguments[0].Expression,
                    cancellationToken);
                if (!option.HasValue || !(option.Value is int optionValue))
                {
                    return false;
                }

                switch (optionValue)
                {
                    case 0:
                        action = PersistenceAction.GeneratedNone;
                        return true;
                    case 1:
                        action = PersistenceAction.GeneratedIdentity;
                        return true;
                    case 2:
                        action = PersistenceAction.GeneratedComputed;
                        return true;
                }
            }

            return false;
        }

        private static bool IsPersistenceMethod(IMethodSymbol method)
        {
            var containingType = method.ContainingType;
            if (IsType(containingType, DommelMappingNamespace, "DommelPropertyMap"))
            {
                return true;
            }

            for (var current = containingType; current != null; current = current.BaseType)
            {
                if (IsType(current.OriginalDefinition, MappingNamespace, "PropertyMapBase`1"))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetConversionAction(IMethodSymbol method, out ConversionAction action)
        {
            action = null;

            if (method == null || !IsPropertyMapMethod(method))
            {
                return false;
            }

            switch (method.Name)
            {
                case "ConvertFromDatabaseUsing":
                    action = ConversionAction.Read;
                    return true;
                case "ConvertToDatabaseUsing":
                    action = ConversionAction.Write;
                    return true;
                case "ConvertUsing":
                    action = ConversionAction.ReadWrite;
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsTypeBasedConversionInvocation(IMethodSymbol method)
        {
            return method.IsGenericMethod &&
                   method.TypeArguments.Length == 2 &&
                   method.Parameters.Length == 0;
        }

        private static void ValidateTypeBasedConverter(
            SyntaxNodeAnalysisContext context,
            InvocationExpressionSyntax invocation,
            IMethodSymbol method,
            MemberPathInfo memberPath,
            ConversionAction action)
        {
            var converterType = method.TypeArguments[0] as INamedTypeSymbol;
            var databaseType = method.TypeArguments[1];
            if (converterType == null || databaseType == null)
            {
                return;
            }

            foreach (var direction in action.Directions)
            {
                if (TryFindCompatibleConverterContract(
                    context.Compilation,
                    converterType,
                    databaseType,
                    memberPath.TerminalType,
                    direction,
                    out var reason))
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidPropertyConverterRule,
                    GetInvocationNameLocation(invocation),
                    memberPath.Display,
                    FormatDirection(direction),
                    FormatSymbol(converterType),
                    reason));
            }
        }

        private static bool TryFindCompatibleConverterContract(
            Compilation compilation,
            INamedTypeSymbol converterType,
            ITypeSymbol databaseType,
            ITypeSymbol mappedPropertyType,
            PropertyConversionDirection direction,
            out string reason)
        {
            reason = null;
            var interfaceName = direction == PropertyConversionDirection.Read
                ? "IReadPropertyConverter`2"
                : "IWritePropertyConverter`2";

            var databaseMatches = converterType.AllInterfaces
                .Where(type => IsType(type.OriginalDefinition, MappingNamespace, interfaceName))
                .Where(type =>
                {
                    var converterDatabaseType = direction == PropertyConversionDirection.Read
                        ? type.TypeArguments[0]
                        : type.TypeArguments[1];

                    return IsSameOrNullableEquivalent(converterDatabaseType, databaseType);
                })
                .ToList();

            if (databaseMatches.Count == 0)
            {
                reason = direction == PropertyConversionDirection.Read
                    ? $"it does not implement IReadPropertyConverter<{FormatSymbol(databaseType)}, TProperty>"
                    : $"it does not implement IWritePropertyConverter<TProperty, {FormatSymbol(databaseType)}>";
                return false;
            }

            var matches = databaseMatches
                .Where(type =>
                {
                    var converterPropertyType = direction == PropertyConversionDirection.Read
                        ? type.TypeArguments[1]
                        : type.TypeArguments[0];

                    return direction == PropertyConversionDirection.Read
                        ? CanAssignValue(compilation, mappedPropertyType, converterPropertyType)
                        : CanAssignValue(compilation, converterPropertyType, mappedPropertyType);
                })
                .ToList();

            if (matches.Count == 0)
            {
                var converterPropertyType = databaseMatches[0].TypeArguments[
                    direction == PropertyConversionDirection.Read ? 1 : 0];
                reason = direction == PropertyConversionDirection.Read
                    ? $"it returns '{FormatSymbol(converterPropertyType)}', which cannot be assigned to mapped property type '{FormatSymbol(mappedPropertyType)}'"
                    : $"it accepts '{FormatSymbol(converterPropertyType)}', which is not compatible with mapped property type '{FormatSymbol(mappedPropertyType)}'";
                return false;
            }

            if (matches.Count > 1)
            {
                reason = $"it matches more than one compatible {interfaceName.Replace("`2", "<,>")} contract";
                return false;
            }

            return true;
        }

        private static bool IsPropertyMapMethod(IMethodSymbol method)
        {
            var containingType = method.ContainingType;
            if (IsType(containingType, DommelMappingNamespace, "DommelPropertyMap"))
            {
                return true;
            }

            for (var current = containingType; current != null; current = current.BaseType)
            {
                if (IsType(current.OriginalDefinition, MappingNamespace, "PropertyMapBase`1"))
                {
                    return true;
                }
            }

            return false;
        }

        private static Location GetInvocationNameLocation(InvocationExpressionSyntax invocation)
        {
            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
            return memberAccess == null
                ? invocation.GetLocation()
                : memberAccess.Name.GetLocation();
        }

        private static INamedTypeSymbol FindEntityType(INamedTypeSymbol mapType)
        {
            for (var current = mapType; current != null; current = current.BaseType)
            {
                if (IsType(current.OriginalDefinition, MappingNamespace, "EntityMapBase`2") ||
                    IsType(current.OriginalDefinition, MappingNamespace, "EntityMap`1"))
                {
                    return current.TypeArguments[0] as INamedTypeSymbol;
                }
            }

            return null;
        }

        private static bool IsAssignableTo(INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, baseType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsType(INamedTypeSymbol type, string namespaceName, string metadataName)
        {
            return type != null &&
                   type.MetadataName == metadataName &&
                   type.ContainingNamespace.ToDisplayString() == namespaceName;
        }

        private static bool CanAssignValue(Compilation compilation, ITypeSymbol targetType, ITypeSymbol valueType)
        {
            if (IsSameOrNullableEquivalent(targetType, valueType))
            {
                return true;
            }

            var conversion = compilation.ClassifyConversion(valueType, targetType);
            return conversion.IsImplicit;
        }

        private static bool IsSameOrNullableEquivalent(ITypeSymbol left, ITypeSymbol right)
        {
            return SymbolEqualityComparer.Default.Equals(left, right) ||
                   SymbolEqualityComparer.Default.Equals(GetNullableUnderlyingType(left), right) ||
                   SymbolEqualityComparer.Default.Equals(GetNullableUnderlyingType(right), left);
        }

        private static ITypeSymbol GetNullableUnderlyingType(ITypeSymbol type)
        {
            var namedType = type as INamedTypeSymbol;
            if (namedType == null ||
                namedType.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T ||
                namedType.TypeArguments.Length != 1)
            {
                return null;
            }

            return namedType.TypeArguments[0];
        }

        private static bool TryGetEntityMapInterface(INamedTypeSymbol mapType, out INamedTypeSymbol entityType)
        {
            entityType = null;
            var entityMapInterfaces = mapType.AllInterfaces
                .Where(type => IsType(type.OriginalDefinition, MappingNamespace, "IEntityMap`1"))
                .ToList();

            if (entityMapInterfaces.Count != 1 ||
                entityMapInterfaces[0].TypeArguments[0].TypeKind != TypeKind.Class)
            {
                return false;
            }

            entityType = entityMapInterfaces[0].TypeArguments[0] as INamedTypeSymbol;
            return entityType != null;
        }

        private static bool TryGetProfileMapInterface(INamedTypeSymbol mapType, out INamedTypeSymbol profileType)
        {
            profileType = null;
            var profileMapInterfaces = mapType.AllInterfaces
                .Where(type => IsType(type.OriginalDefinition, MappingNamespace, "IProfileMap`1"))
                .ToList();

            if (profileMapInterfaces.Count != 1)
            {
                return false;
            }

            profileType = profileMapInterfaces[0].TypeArguments[0] as INamedTypeSymbol;
            return profileType != null;
        }

        private static string FormatSymbol(ISymbol symbol)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        }

        private static string FormatSymbol(ITypeSymbol symbol)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        }

        private static string FormatDirection(PropertyConversionDirection direction)
        {
            return direction == PropertyConversionDirection.Read ? "read" : "write";
        }

        private sealed class MapInvocation
        {
            internal MapInvocation(
                IMethodSymbol constructor,
                MemberPathInfo memberPath,
                string columnName,
                bool columnKnown,
                bool caseSensitive,
                bool ignored,
                Location invocationLocation,
                Location columnLocation)
            {
                Constructor = constructor;
                MemberPath = memberPath;
                ColumnName = columnName;
                ColumnKnown = columnKnown;
                CaseSensitive = caseSensitive;
                Ignored = ignored;
                InvocationLocation = invocationLocation;
                ColumnLocation = columnLocation;
            }

            internal IMethodSymbol Constructor { get; }

            internal MemberPathInfo MemberPath { get; }

            internal string ColumnName { get; }

            internal bool ColumnKnown { get; }

            internal bool CaseSensitive { get; }

            internal bool Ignored { get; }

            internal Location InvocationLocation { get; }

            internal Location ColumnLocation { get; }
        }

        private sealed class MemberPathInfo
        {
            private MemberPathInfo(string key, string display, string terminalName, ITypeSymbol terminalType)
            {
                Key = key;
                Display = display;
                TerminalName = terminalName;
                TerminalType = terminalType;
            }

            internal string Key { get; }

            internal string Display { get; }

            internal string TerminalName { get; }

            internal ITypeSymbol TerminalType { get; }

            internal static MemberPathInfo Create(IEnumerable<IPropertySymbol> properties)
            {
                var propertyList = properties.ToList();
                var key = string.Join(
                    ".",
                    propertyList.Select(property => property.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + property.MetadataName));
                var display = string.Join(".", propertyList.Select(property => property.Name));
                var terminal = propertyList[propertyList.Count - 1];
                return new MemberPathInfo(key, display, terminal.Name, terminal.Type);
            }
        }

        private enum PropertyConversionDirection
        {
            Read,
            Write
        }

        private sealed class ConversionAction
        {
            internal static readonly ConversionAction Read =
                new ConversionAction(new[] { PropertyConversionDirection.Read });
            internal static readonly ConversionAction Write =
                new ConversionAction(new[] { PropertyConversionDirection.Write });
            internal static readonly ConversionAction ReadWrite =
                new ConversionAction(new[] { PropertyConversionDirection.Read, PropertyConversionDirection.Write });

            private ConversionAction(IEnumerable<PropertyConversionDirection> directions)
            {
                Directions = directions.ToArray();
            }

            internal IReadOnlyList<PropertyConversionDirection> Directions { get; }
        }

        private sealed class ConversionChainState
        {
            private bool _read;
            private bool _write;

            internal bool TryApply(PropertyConversionDirection direction)
            {
                if (direction == PropertyConversionDirection.Read)
                {
                    if (_read)
                    {
                        return false;
                    }

                    _read = true;
                    return true;
                }

                if (_write)
                {
                    return false;
                }

                _write = true;
                return true;
            }
        }

        private sealed class ProfileRegistrationInvocation
        {
            internal ProfileRegistrationInvocation(
                ISymbol containingSymbol,
                INamedTypeSymbol entityType,
                INamedTypeSymbol profileType,
                Location location)
            {
                ContainingSymbol = containingSymbol;
                EntityType = entityType;
                ProfileType = profileType;
                Location = location;
            }

            internal ISymbol ContainingSymbol { get; }

            internal INamedTypeSymbol EntityType { get; }

            internal INamedTypeSymbol ProfileType { get; }

            internal Location Location { get; }
        }

        private enum PersistenceAction
        {
            None,
            ExcludeFromInsert,
            ExcludeFromUpdate,
            ReadOnly,
            Computed,
            DatabaseDefaultOnInsert,
            Key,
            Identity,
            GeneratedNone,
            GeneratedComputed,
            GeneratedIdentity
        }

        private sealed class PersistenceChainState
        {
            private bool _ignored;
            private bool _computed;
            private bool _databaseDefaultOnInsert;
            private bool _key;
            private bool _identity;

            internal void ApplyIgnore()
            {
                _ignored = true;
            }

            internal bool TryApply(PersistenceAction action, out string reason)
            {
                reason = null;

                if (_ignored)
                {
                    reason = "Ignore() disables materialization and persistence metadata; write persistence calls cannot be applied after Ignore().";
                    return false;
                }

                switch (action)
                {
                    case PersistenceAction.Computed:
                    case PersistenceAction.GeneratedComputed:
                        if (_databaseDefaultOnInsert)
                        {
                            reason = "computed values cannot also be configured with DatabaseDefaultOnInsert().";
                            return false;
                        }

                        if (_key)
                        {
                            reason = "computed values cannot also be configured as keys.";
                            return false;
                        }

                        if (_identity)
                        {
                            reason = "computed values cannot also be configured as identity values.";
                            return false;
                        }

                        _computed = true;
                        return true;
                    case PersistenceAction.DatabaseDefaultOnInsert:
                        if (_computed)
                        {
                            reason = "DatabaseDefaultOnInsert() cannot be combined with computed persistence semantics.";
                            return false;
                        }

                        if (_identity)
                        {
                            reason = "DatabaseDefaultOnInsert() cannot be combined with identity persistence semantics.";
                            return false;
                        }

                        _databaseDefaultOnInsert = true;
                        return true;
                    case PersistenceAction.Key:
                        if (_computed)
                        {
                            reason = "key persistence semantics cannot be combined with computed values.";
                            return false;
                        }

                        _key = true;
                        return true;
                    case PersistenceAction.Identity:
                    case PersistenceAction.GeneratedIdentity:
                        if (_computed)
                        {
                            reason = "identity persistence semantics cannot be combined with computed values.";
                            return false;
                        }

                        if (_databaseDefaultOnInsert)
                        {
                            reason = "identity persistence semantics cannot be combined with DatabaseDefaultOnInsert().";
                            return false;
                        }

                        _key = true;
                        _identity = true;
                        return true;
                    case PersistenceAction.GeneratedNone:
                        _identity = false;
                        _computed = false;
                        _databaseDefaultOnInsert = false;
                        return true;
                    default:
                        return true;
                }
            }
        }
    }
}
