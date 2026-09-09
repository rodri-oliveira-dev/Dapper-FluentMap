# Decouple NuGet Package IDs from FluentMap project identities

## Status

Accepted

## Context

FluentMap publishes multiple NuGet packages from one repository. Historically the NuGet PackageId, project name, assembly name, directory name and C# namespace often used the same text. That similarity is convenient, but it is not a technical requirement.

During publication of version 3.0.0, NuGet.org accepted `Dapper.FluentMap` and `Dapper.FluentMap.Dommel`, then rejected creation of `Dapper.FluentMap.DependencyInjection` with HTTP 409 because the `Dapper.FluentMap.*` package prefix is reserved. Trusted Publishing/OIDC authentication had already succeeded. The failure was not caused by invalid credentials, OIDC configuration, package corruption, build failure, test failure, checksum failure or a duplicate version of the new DependencyInjection package.

The same namespace constraint can affect other newly introduced PackageIds under `Dapper.FluentMap.*`, including analyzers and source generators.

A NuGet flat-container HTTP 404 proves only that a particular PackageId/version is not currently published. It does not prove that the authenticated publisher is authorized to create that PackageId under a reserved prefix.

The 3.0.0 artifacts already accepted by NuGet.org are immutable historical artifacts:

- `Dapper.FluentMap` 3.0.0
- `Dapper.FluentMap.Dommel` 3.0.0

The repository must not delete, recreate, move, force-update or retag `v3.0.0`, and it must not create new 3.0.0 packages from a newer commit to fill the previous partial release.

## Decision

We will decouple only the distribution identities that are blocked by NuGet.org prefix reservation. Existing package identities that were already accepted remain unchanged:

| Project / Assembly | NuGet PackageId |
| --- | --- |
| `Dapper.FluentMap` | `Dapper.FluentMap` |
| `Dapper.FluentMap.Dommel` | `Dapper.FluentMap.Dommel` |
| `Dapper.FluentMap.DependencyInjection` | `FluentMap.DependencyInjection` |
| `Dapper.FluentMap.Analyzers` | `FluentMap.Analyzers` |
| `Dapper.FluentMap.Generators` | `FluentMap.Generators` |

Project identity, assembly identity, namespace identity and NuGet PackageId are independent identities:

```text
Project identity != Assembly identity != Namespace identity != NuGet PackageId
```

The repository will preserve source and binary identities for the three renamed distribution packages:

- `src/Dapper.FluentMap.DependencyInjection/`
- `Dapper.FluentMap.DependencyInjection.csproj`
- `Dapper.FluentMap.DependencyInjection.dll`
- `namespace Dapper.FluentMap.DependencyInjection`
- `src/Dapper.FluentMap.Analyzers/`
- `Dapper.FluentMap.Analyzers.csproj`
- `Dapper.FluentMap.Analyzers.dll`
- `namespace Dapper.FluentMap.Analyzers`
- `src/Dapper.FluentMap.Generators/`
- `Dapper.FluentMap.Generators.csproj`
- `Dapper.FluentMap.Generators.dll`
- `namespace Dapper.FluentMap.Generators`

Package identity will be managed through a single machine-readable catalog under `eng/`. Release, recovery, metadata validation and consumer smoke tests should use that catalog wherever practical.

The next coherent release after this decision should be 3.0.1 for the complete package family:

- `Dapper.FluentMap`
- `Dapper.FluentMap.Dommel`
- `FluentMap.DependencyInjection`
- `FluentMap.Analyzers`
- `FluentMap.Generators`

This decision prepares the repository for 3.0.1. It does not publish packages, create `v3.0.1` or create a GitHub Release.

## Consequences

Consumers install the three new feature packages through their new NuGet identities while continuing to use the existing C# namespaces and public APIs.

Analyzer and source-generator packages intentionally keep their assembly names inside the analyzer package layout. For example, `FluentMap.Analyzers` can contain `analyzers/dotnet/cs/Dapper.FluentMap.Analyzers.dll`.

Release preflight must distinguish "PackageId/version is not currently published" from "the publisher is authorized to create the PackageId." Unexpected NuGet responses must fail closed.

Release and recovery workflows must treat partial publication as recoverable by explicitly checking which PackageId/version pairs already exist, validating existing packages before accepting them and publishing only packages that are genuinely missing. They must not use `--skip-duplicate`, hide the original `dotnet nuget push` diagnostic, move existing tags or mask artifact mismatches.

Documentation must explain the PackageId change as a distribution identity change, not a namespace rename, assembly rename or public API breaking change.
