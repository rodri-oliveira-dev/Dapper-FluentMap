# Support Policy

This project is maintained as an open source .NET library. The policy is intentionally small and sustainable.

## Supported Versions

The supported line is the current fork line once a release candidate or stable release is published from this repository.

| Line | Support status |
| --- | --- |
| Current fork prerelease/RC | Supported for adoption feedback, bug reports and compatibility validation. Preview behavior may still change before stable. |
| Current fork stable | Supported for compatible bug fixes and security fixes once published. |
| Historical archived `Dapper.FluentMap` packages | Not actively maintained by this fork, except where compatibility is explicitly preserved or migration guidance is provided. |

Do not publish or depend on an unreleased local package as if it were a stable support line.

## Bug Fixes

Bug fixes should preserve source, binary and behavioral compatibility unless the existing behavior is clearly incorrect and the change is documented as a bug fix.

Fixes should include focused tests when practical, especially for:

- public mapping behavior;
- Dapper integration;
- Dommel integration;
- generated materialization;
- provider compatibility;
- global-state or cache behavior.

## Security Fixes

Security issues are prioritized over ordinary bugs. The project does not promise an SLA, but security reports should include enough detail to reproduce or evaluate the issue.

If private GitHub security advisories are enabled for the repository, use that channel. Otherwise, open an issue with minimal sensitive detail and request a private follow-up path.

## Preview And RC Behavior

Release candidates are intended to validate package shape, API compatibility, migration guidance and real consumer adoption before a stable release.

During RC:

- new APIs may still receive naming or documentation adjustments;
- analyzer severities and generator diagnostics may still be tuned;
- compatibility gaps may block stable promotion;
- unsupported claims should stay documented rather than implied.

Breaking changes after a stable release require an explicit major-version decision.

## Unsupported Environments

The project currently does not support or certify:

- full Native AOT compatibility;
- Dapper `3.x` or later;
- Dommel `4.x` or later;
- provider certification without real automated or documented integration tests;
- Dommel isolation per `FluentMapRuntime`;
- using FluentMap as an ORM, CRUD framework, query builder, migration tool or connection abstraction.

## Issue Reporting

Good issues include:

- package name and version;
- .NET runtime/SDK version;
- Dapper and Dommel versions when relevant;
- database provider and version when provider behavior matters;
- a minimal entity/map/query example;
- expected behavior;
- actual behavior;
- whether the issue happens with normal `Dapper.Query<T>()`, FluentMap `QueryMapped*`, Dommel, generated registration or DI.

For provider issues, include whether the provider is SQLite, SQL Server, PostgreSQL, MySQL/MariaDB or another provider, and whether the failure reproduces outside FluentMap.
