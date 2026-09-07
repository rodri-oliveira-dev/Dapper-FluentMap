using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper.FluentMap;
using Dapper.FluentMap.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Dapper.FluentMap.Analyzers.Tests
{
    public sealed class FluentMapConfigurationAnalyzerTests
    {
        [Fact]
        public async Task InvalidMapExpressionShouldReportDfm001()
        {
            var source = @"
using Dapper.FluentMap.Mapping;

public sealed class Customer
{
    public string Name { get; set; }

    public string GetName() => Name;
}

public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        Map(c => c.GetName()).ToColumn(""customer_name"");
    }
}";

            var diagnostic = await GetSingleDiagnosticAsync(source, FluentMapConfigurationAnalyzer.InvalidMapExpressionDiagnosticId);

            AssertDiagnostic(diagnostic, DiagnosticSeverity.Error, "Map expression 'c.GetName()' is invalid");
            AssertDiagnosticLineContains(source, diagnostic, "Map(c => c.GetName()).ToColumn");
        }

        [Fact]
        public async Task DuplicateMemberPathShouldReportDfm002()
        {
            var source = @"
using Dapper.FluentMap.Mapping;

public sealed class Customer
{
    public int Id { get; set; }
}

public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        Map(c => c.Id).ToColumn(""customer_id"");
        Map(c => c.Id).ToColumn(""other_id"");
    }
}";

            var diagnostic = await GetSingleDiagnosticAsync(source, FluentMapConfigurationAnalyzer.DuplicateMemberPathDiagnosticId);

            AssertDiagnostic(diagnostic, DiagnosticSeverity.Error, "Property path 'Id' is mapped more than once");
            AssertDiagnosticLineContains(source, diagnostic, "Map(c => c.Id).ToColumn(\"other_id\")");
        }

        [Fact]
        public async Task DuplicateColumnShouldReportDfm003()
        {
            var source = @"
using Dapper.FluentMap.Mapping;

public sealed class Customer
{
    public int Id { get; set; }

    public string Name { get; set; }
}

public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        Map(c => c.Id).ToColumn(""shared_column"");
        Map(c => c.Name).ToColumn(""shared_column"");
    }
}";

            var diagnostic = await GetSingleDiagnosticAsync(source, FluentMapConfigurationAnalyzer.DuplicateColumnDiagnosticId);

            AssertDiagnostic(diagnostic, DiagnosticSeverity.Error, "Column 'shared_column' is mapped by more than one property path");
            AssertDiagnosticLineContains(source, diagnostic, "Map(c => c.Name).ToColumn(\"shared_column\")");
        }

        [Fact]
        public async Task InvalidIncludeBaseShouldReportDfm004()
        {
            var source = @"
using Dapper.FluentMap.Mapping;

public class CustomerBase
{
    public int Id { get; set; }
}

public sealed class Customer : CustomerBase
{
    public string Name { get; set; }
}

public sealed class OtherCustomer
{
    public int Id { get; set; }
}

public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        IncludeBase<OtherCustomer>();
    }
}";

            var diagnostic = await GetSingleDiagnosticAsync(source, FluentMapConfigurationAnalyzer.InvalidIncludeBaseDiagnosticId);

            AssertDiagnostic(diagnostic, DiagnosticSeverity.Error, "Type 'OtherCustomer' cannot be included as a base mapping for entity 'Customer'");
            AssertDiagnosticLineContains(source, diagnostic, "IncludeBase<OtherCustomer>()");
        }

        [Fact]
        public async Task InvalidGenericMapRegistrationShouldReportDfm005()
        {
            var source = @"
using System.Collections.Generic;
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Mapping;

public sealed class NonGenericMap : IEntityMap
{
    public IList<IPropertyMap> PropertyMaps { get; } = new List<IPropertyMap>();
}

public sealed class Startup
{
    public void Configure(FluentMapConfiguration configuration)
    {
        configuration.AddMap<NonGenericMap>();
    }
}";

            var diagnostic = await GetSingleDiagnosticAsync(source, FluentMapConfigurationAnalyzer.InvalidGenericMapRegistrationDiagnosticId);

            AssertDiagnostic(diagnostic, DiagnosticSeverity.Error, "Entity map type 'NonGenericMap' must implement exactly one closed IEntityMap<TEntity> interface");
            AssertDiagnosticLineContains(source, diagnostic, "configuration.AddMap<NonGenericMap>()");
        }

        [Fact]
        public async Task InvalidGenericProfileRegistrationShouldReportDfm009()
        {
            var source = @"
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Mapping;

public sealed class Customer
{
    public int Id { get; set; }
}

public sealed class CustomerMap : EntityMap<Customer>
{
}

public sealed class Startup
{
    public void Configure(FluentMapConfiguration configuration)
    {
        configuration.AddProfile<CustomerMap>();
    }
}";

            var diagnostic = await GetSingleDiagnosticAsync(source, FluentMapConfigurationAnalyzer.InvalidGenericProfileRegistrationDiagnosticId);

            AssertDiagnostic(diagnostic, DiagnosticSeverity.Error, "Profile map type 'CustomerMap' must implement exactly one closed IEntityMap<TEntity> interface and exactly one closed IProfileMap<TProfile> interface");
            AssertDiagnosticLineContains(source, diagnostic, "configuration.AddProfile<CustomerMap>()");
        }

        [Fact]
        public async Task DuplicateProfileRegistrationShouldReportDfm010()
        {
            var source = @"
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Mapping;

public sealed class LegacyProfile : IMappingProfile
{
}

public sealed class Customer
{
    public int Id { get; set; }
}

public sealed class FirstCustomerMap : EntityMap<Customer>, IProfileMap<LegacyProfile>
{
}

public sealed class SecondCustomerMap : EntityMap<Customer>, IProfileMap<LegacyProfile>
{
}

public sealed class Startup
{
    public void Configure(FluentMapConfiguration configuration)
    {
        configuration
            .AddProfile<FirstCustomerMap>()
            .AddProfile<SecondCustomerMap>();
    }
}";

            var diagnostic = await GetSingleDiagnosticAsync(source, FluentMapConfigurationAnalyzer.DuplicateProfileRegistrationDiagnosticId);

            AssertDiagnostic(diagnostic, DiagnosticSeverity.Error, "Entity 'Customer' registers mapping profile 'LegacyProfile' more than once");
            AssertDiagnosticLineContains(source, diagnostic, ".AddProfile<SecondCustomerMap>()");
        }

        [Fact]
        public async Task PersistenceConfigurationAfterIgnoreShouldReportDfm013()
        {
            var source = @"
using Dapper.FluentMap.Mapping;

public sealed class Customer
{
    public string Name { get; set; }
}

public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        Map(c => c.Name).ToColumn(""customer_name"").Ignore().ReadOnly();
    }
}";

            var diagnostic = await GetSingleDiagnosticAsync(source, FluentMapConfigurationAnalyzer.InvalidPersistenceBehaviorDiagnosticId);

            AssertDiagnostic(diagnostic, DiagnosticSeverity.Error, "Property path 'Name' has invalid persistence behavior");
            AssertDiagnostic(diagnostic, DiagnosticSeverity.Error, "after Ignore()");
            AssertDiagnosticLineContains(source, diagnostic, "ReadOnly()");
        }

        [Fact]
        public async Task ComputedAndDatabaseDefaultShouldReportDfm013()
        {
            var source = @"
using Dapper.FluentMap.Mapping;

public sealed class Customer
{
    public string Total { get; set; }
}

public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        Map(c => c.Total).Computed().DatabaseDefaultOnInsert();
    }
}";

            var diagnostic = await GetSingleDiagnosticAsync(source, FluentMapConfigurationAnalyzer.InvalidPersistenceBehaviorDiagnosticId);

            AssertDiagnostic(diagnostic, DiagnosticSeverity.Error, "DatabaseDefaultOnInsert() cannot be combined with computed persistence semantics");
            AssertDiagnosticLineContains(source, diagnostic, "DatabaseDefaultOnInsert()");
        }

        [Fact]
        public async Task DatabaseDefaultAndComputedShouldReportDfm013()
        {
            var source = @"
using Dapper.FluentMap.Mapping;

public sealed class Customer
{
    public string Total { get; set; }
}

public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        Map(c => c.Total).DatabaseDefaultOnInsert().Computed();
    }
}";

            var diagnostic = await GetSingleDiagnosticAsync(source, FluentMapConfigurationAnalyzer.InvalidPersistenceBehaviorDiagnosticId);

            AssertDiagnostic(diagnostic, DiagnosticSeverity.Error, "computed values cannot also be configured with DatabaseDefaultOnInsert()");
            AssertDiagnosticLineContains(source, diagnostic, "Computed()");
        }

        [Fact]
        public async Task ComputedAndKeyShouldReportDfm013()
        {
            var source = @"
using Dapper.FluentMap.Dommel.Mapping;

public sealed class Customer
{
    public string Code { get; set; }
}

public sealed class CustomerMap : DommelEntityMap<Customer>
{
    public CustomerMap()
    {
        Map(c => c.Code).Computed().IsKey();
    }
}";

            var diagnostic = await GetSingleDiagnosticAsync(source, FluentMapConfigurationAnalyzer.InvalidPersistenceBehaviorDiagnosticId);

            AssertDiagnostic(diagnostic, DiagnosticSeverity.Error, "key persistence semantics cannot be combined with computed values");
            AssertDiagnosticLineContains(source, diagnostic, "IsKey()");
        }

        [Fact]
        public async Task GeneratedOptionComputedAndDatabaseDefaultShouldReportDfm013()
        {
            var source = @"
using System.ComponentModel.DataAnnotations.Schema;
using Dapper.FluentMap.Dommel.Mapping;

public sealed class Customer
{
    public string Total { get; set; }
}

public sealed class CustomerMap : DommelEntityMap<Customer>
{
    public CustomerMap()
    {
        Map(c => c.Total)
            .SetGeneratedOption(DatabaseGeneratedOption.Computed)
            .DatabaseDefaultOnInsert();
    }
}";

            var diagnostic = await GetSingleDiagnosticAsync(source, FluentMapConfigurationAnalyzer.InvalidPersistenceBehaviorDiagnosticId);

            AssertDiagnostic(diagnostic, DiagnosticSeverity.Error, "DatabaseDefaultOnInsert() cannot be combined with computed persistence semantics");
            AssertDiagnosticLineContains(source, diagnostic, "DatabaseDefaultOnInsert()");
        }

        [Fact]
        public async Task ValidPersistenceCombinationsShouldNotReportDfm013()
        {
            var source = @"
using System.ComponentModel.DataAnnotations.Schema;
using Dapper.FluentMap.Dommel.Mapping;

public sealed class Customer
{
    public int Id { get; set; }

    public string Code { get; set; }

    public string ReadOnlyName { get; set; }

    public string InsertExcluded { get; set; }

    public string UpdateExcluded { get; set; }

    public string DefaultValue { get; set; }

    public string ComputedValue { get; set; }
}

public sealed class CustomerMap : DommelEntityMap<Customer>
{
    public CustomerMap()
    {
        Map(c => c.Id).IsIdentity();
        Map(c => c.Code).IsKey().SetGeneratedOption(DatabaseGeneratedOption.None);
        Map(c => c.ReadOnlyName).ReadOnly();
        Map(c => c.InsertExcluded).ExcludeFromInsert();
        Map(c => c.UpdateExcluded).ExcludeFromUpdate();
        Map(c => c.DefaultValue).DatabaseDefaultOnInsert().ExcludeFromUpdate();
        Map(c => c.ComputedValue).Computed();
    }
}";

            var diagnostics = await GetAnalyzerDiagnosticsAsync(source);

            Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == FluentMapConfigurationAnalyzer.InvalidPersistenceBehaviorDiagnosticId);
        }

        [Fact]
        public async Task InvalidReadConverterContractShouldReportDfm014()
        {
            var source = @"
using Dapper.FluentMap.Mapping;

public enum AccountStatus
{
    Active
}

public sealed class Customer
{
    public AccountStatus Status { get; set; }
}

public sealed class StatusConverter
{
}

public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        Map(c => c.Status).ConvertFromDatabaseUsing<StatusConverter, string>();
    }
}";

            var diagnostic = await GetSingleDiagnosticAsync(source, FluentMapConfigurationAnalyzer.InvalidPropertyConverterDiagnosticId);

            AssertDiagnostic(diagnostic, DiagnosticSeverity.Error, "Property path 'Status' has invalid read converter 'StatusConverter'");
            AssertDiagnostic(diagnostic, DiagnosticSeverity.Error, "does not implement IReadPropertyConverter<string, TProperty>");
            AssertDiagnosticLineContains(source, diagnostic, "ConvertFromDatabaseUsing<StatusConverter, string>()");
        }

        [Fact]
        public async Task InvalidWriteConverterContractShouldReportDfm014()
        {
            var source = @"
using Dapper.FluentMap.Mapping;

public enum AccountStatus
{
    Active
}

public sealed class Customer
{
    public AccountStatus Status { get; set; }
}

public sealed class StatusConverter : IWritePropertyConverter<int, string>
{
    public string ConvertToDatabase(int value) => value.ToString();
}

public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        Map(c => c.Status).ConvertToDatabaseUsing<StatusConverter, string>();
    }
}";

            var diagnostic = await GetSingleDiagnosticAsync(source, FluentMapConfigurationAnalyzer.InvalidPropertyConverterDiagnosticId);

            AssertDiagnostic(diagnostic, DiagnosticSeverity.Error, "Property path 'Status' has invalid write converter 'StatusConverter'");
            AssertDiagnostic(diagnostic, DiagnosticSeverity.Error, "accepts 'int', which is not compatible with mapped property type 'AccountStatus'");
            AssertDiagnosticLineContains(source, diagnostic, "ConvertToDatabaseUsing<StatusConverter, string>()");
        }

        [Fact]
        public async Task DuplicateReadConverterInSameFluentChainShouldReportDfm015()
        {
            var source = @"
using Dapper.FluentMap.Mapping;

public sealed class Customer
{
    public string Name { get; set; }
}

public sealed class FirstConverter : IReadPropertyConverter<string, string>
{
    public string ConvertFromDatabase(string value) => value;
}

public sealed class SecondConverter : IReadPropertyConverter<string, string>
{
    public string ConvertFromDatabase(string value) => value;
}

public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        Map(c => c.Name)
            .ConvertFromDatabaseUsing<FirstConverter, string>()
            .ConvertFromDatabaseUsing<SecondConverter, string>();
    }
}";

            var diagnostic = await GetSingleDiagnosticAsync(source, FluentMapConfigurationAnalyzer.DuplicatePropertyConverterDiagnosticId);

            AssertDiagnostic(diagnostic, DiagnosticSeverity.Error, "Property path 'Name' configures more than one read converter");
            AssertDiagnosticLineContains(source, diagnostic, "ConvertFromDatabaseUsing<SecondConverter, string>()");
        }

        [Fact]
        public async Task DuplicateWriteConverterInSameFluentChainShouldReportDfm015()
        {
            var source = @"
using Dapper.FluentMap.Mapping;

public sealed class Customer
{
    public string Name { get; set; }
}

public sealed class FirstConverter : IWritePropertyConverter<string, string>
{
    public string ConvertToDatabase(string value) => value;
}

public sealed class SecondConverter : IWritePropertyConverter<string, string>
{
    public string ConvertToDatabase(string value) => value;
}

public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        Map(c => c.Name)
            .ConvertToDatabaseUsing<FirstConverter, string>()
            .ConvertToDatabaseUsing<SecondConverter, string>();
    }
}";

            var diagnostic = await GetSingleDiagnosticAsync(source, FluentMapConfigurationAnalyzer.DuplicatePropertyConverterDiagnosticId);

            AssertDiagnostic(diagnostic, DiagnosticSeverity.Error, "Property path 'Name' configures more than one write converter");
            AssertDiagnosticLineContains(source, diagnostic, "ConvertToDatabaseUsing<SecondConverter, string>()");
        }

        [Fact]
        public async Task ValidMappingConfigurationShouldNotReportDiagnostics()
        {
            var source = @"
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Mapping;

public sealed record ConstructorCustomer(int Id, string Name);

public class CustomerBase
{
    public int Id { get; set; }
}

public sealed class Customer : CustomerBase
{
    public string Name { get; set; }

    public Rank Rank { get; set; }

    public Seniority Seniority { get; set; }
}

public sealed class Rank
{
    public int Level { get; set; }
}

public sealed class Seniority
{
    public int Level { get; set; }
}

public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        IncludeBase<CustomerBase>();
        Map(c => c.Name).ToColumn(""customer_name"");
        Map(c => c.Rank.Level).ToColumn(""rank_level"");
        Map(c => c.Seniority.Level).ToColumn(""seniority_level"");
    }
}

public sealed class CustomerBaseMap : EntityMap<CustomerBase>
{
    public CustomerBaseMap()
    {
        Map(c => c.Id).ToColumn(""customer_id"");
    }
}

public sealed class ConstructorCustomerMap : EntityMap<ConstructorCustomer>
{
    public ConstructorCustomerMap()
    {
        Map(c => c.Id).ToColumn(""constructor_customer_id"");
    }
}

public sealed class LegacyProfile : IMappingProfile
{
}

public sealed class LegacyCustomerMap : EntityMap<Customer>, IProfileMap<LegacyProfile>
{
    public LegacyCustomerMap()
    {
        Map(c => c.Id).ToColumn(""legacy_customer_id"");
    }
}

public sealed class Startup
{
    public void Configure(FluentMapConfiguration configuration)
    {
        configuration
            .AddMap<CustomerMap>()
            .AddMap<CustomerBaseMap>()
            .AddMap<ConstructorCustomerMap>()
            .AddProfile<LegacyCustomerMap>();
    }
}";

            var diagnostics = await GetAnalyzerDiagnosticsAsync(source);

            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task CaseSensitiveColumnNamesWithDifferentCasingShouldNotReportDfm003()
        {
            var source = @"
using Dapper.FluentMap.Mapping;

public sealed class Customer
{
    public int Id { get; set; }

    public string Name { get; set; }
}

public sealed class CustomerMap : EntityMap<Customer>
{
    public CustomerMap()
    {
        Map(c => c.Id).ToColumn(""Customer"");
        Map(c => c.Name).ToColumn(""customer"");
    }
}";

            var diagnostics = await GetAnalyzerDiagnosticsAsync(source);

            Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == FluentMapConfigurationAnalyzer.DuplicateColumnDiagnosticId);
        }

        private static async Task<Diagnostic> GetSingleDiagnosticAsync(string source, string diagnosticId)
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(source);
            return Assert.Single(diagnostics, diagnostic => diagnostic.Id == diagnosticId);
        }

        private static async Task<IReadOnlyList<Diagnostic>> GetAnalyzerDiagnosticsAsync(string source)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(
                source,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview),
                path: "Test0.cs");

            var references = GetMetadataReferences();
            var compilation = CSharpCompilation.Create(
                "AnalyzerTest",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var compilerErrors = compilation
                .GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(diagnostic => diagnostic.ToString())
                .ToList();

            Assert.Empty(compilerErrors);

            var analyzer = new FluentMapConfigurationAnalyzer();
            var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
            var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

            return diagnostics
                .Where(diagnostic => diagnostic.Id.StartsWith("DFM", StringComparison.Ordinal))
                .OrderBy(diagnostic => diagnostic.Id, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Location.SourceSpan.Start)
                .ToList();
        }

        private static IReadOnlyList<MetadataReference> GetMetadataReferences()
        {
            var trustedPlatformAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
                .Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path));

            var explicitAssemblies = new[]
            {
                typeof(FluentMapper).Assembly.Location,
                typeof(Dapper.FluentMap.Dommel.Mapping.DommelEntityMap<>).Assembly.Location,
                typeof(global::Dommel.DommelMapper).Assembly.Location,
                typeof(Dapper.SqlMapper).Assembly.Location
            }
            .Select(path => MetadataReference.CreateFromFile(path));

            return trustedPlatformAssemblies
                .Concat(explicitAssemblies)
                .GroupBy(reference => reference.Display, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static void AssertDiagnostic(Diagnostic diagnostic, DiagnosticSeverity severity, string messageFragment)
        {
            Assert.Equal(severity, diagnostic.Severity);
            Assert.Contains(messageFragment, diagnostic.GetMessage(), StringComparison.Ordinal);
        }

        private static void AssertDiagnosticLineContains(string source, Diagnostic diagnostic, string expectedLineFragment)
        {
            var line = diagnostic.Location.GetLineSpan().StartLinePosition.Line;
            var sourceLine = source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)[line];

            Assert.Contains(expectedLineFragment, sourceLine, StringComparison.Ordinal);
        }
    }
}
