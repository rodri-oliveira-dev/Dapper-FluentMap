$ErrorActionPreference = 'Stop'

$testProjects = @(
  './test/Dapper.FluentMap.Tests/Dapper.FluentMap.Tests.csproj',
  './test/Dapper.FluentMap.GeneratedRegistration.Tests/Dapper.FluentMap.GeneratedRegistration.Tests.csproj',
  './test/Dapper.FluentMap.DependencyInjection.Tests/Dapper.FluentMap.DependencyInjection.Tests.csproj',
  './test/Dapper.FluentMap.Dommel.Tests/Dapper.FluentMap.Dommel.Tests.csproj',
  './test/Dapper.FluentMap.ProviderCompatibility.Tests/Dapper.FluentMap.ProviderCompatibility.Tests.csproj',
  './test/Dapper.FluentMap.Analyzers.Tests/Dapper.FluentMap.Analyzers.Tests.csproj',
  './test/Dapper.FluentMap.Generators.Tests/Dapper.FluentMap.Generators.Tests.csproj'
)

foreach ($testProject in $testProjects) {
  dotnet test $testProject --configuration Release --no-build --verbosity normal
}
