; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Analyzer%20Releases.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
DFM006 | Dapper.FluentMap.Configuration | Info | Entity map type is skipped by generated registration
DFM007 | Dapper.FluentMap.Configuration | Error | Multiple generated entity maps target the same entity
DFM008 | Dapper.FluentMap.Configuration | Error | Multiple generated profile maps target the same entity and profile
DFM011 | Dapper.FluentMap.Configuration | Info | Entity map uses runtime materializer fallback for generated materialization
DFM012 | Dapper.FluentMap.Configuration | Error | Generated read converter is invalid
