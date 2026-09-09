---
name: binlog-failure-analysis
description: "Analyze MSBuild binary logs to diagnose build failures. USE FOR: unclear MSBuild errors, cascading failures across the multi-project solution, tracing target execution/order, and inspecting evaluated properties/items. Requires an existing .binlog or explicit permission to generate one. DO NOT USE FOR: non-MSBuild build systems."
license: MIT
---

# Analyzing MSBuild Failures with Binary Logs

This skill diagnoses MSBuild failures from `.binlog` evidence.

> **Dapper-FluentMap integration:** prefer the narrowest failing project or `Dapper.FluentMap.slnx` command. Consider root `Directory.Build.props`, `Directory.Build.targets`, project-specific package versions, analyzer/generator projects, pack targets, and compatibility validation when tracing failures. A binlog-aware MCP/tool is optional, not assumed. Do not attempt to read `.binlog` as plain text.

## Preferred path — structured binlog tooling when available

If a binlog-aware tool is available, inspect:

- errors and warnings;
- evaluated MSBuild properties;
- `PackageReference` and `ProjectReference` items;
- project evaluation data;
- target execution/order;
- the first causal failure before cascaded project failures.

Stop once the cause and affected target/property chain are sufficiently established.

## Fallback — replay binary log to focused text logs

```bash
dotnet msbuild build.binlog -noconlog \
  -fl  -flp:v=diag\;logfile=full.log\;performancesummary \
  -fl1 -flp1:errorsonly\;logfile=errors.log \
  -fl2 -flp2:warningsonly\;logfile=warnings.log
```

PowerShell requires appropriate quoting around semicolon-delimited logger parameters.

Then search generated text logs, not the binary file:

```bash
cat errors.log
grep -n -B2 -A2 "CS0246" full.log
grep -i "CoreCompile.*FAILED\|Build FAILED\|error MSB" full.log
```

## Generating a binlog when needed

If no binlog exists and the task requires one:

```bash
dotnet build ./Dapper.FluentMap.slnx --configuration Release /bl:build.binlog
```

Prefer a narrower failing `.csproj` command when it reproduces the issue. Treat `.binlog` and replay logs as temporary diagnostic artifacts unless explicitly requested otherwise.

## Repository-specific checks

When the failure involves packaging/release, correlate the binlog with:

- `Directory.Build.props` / `Directory.Build.targets`;
- `eng/package-catalog.json`;
- package validation targets/scripts;
- analyzer/generator project settings;
- `netstandard2.0` compatibility constraints;
- explicit package-version properties/`PackageReference` values.

## Validation

- [ ] Cause is supported by properties/items/targets from the log rather than guessed from the final console error
- [ ] Cascading errors are distinguished from the first causal failure
- [ ] No binary log was parsed with plain-text tools
- [ ] Temporary diagnostic artifacts are not committed
- [ ] Fix is validated with the repository's deterministic build/test and pack checks when relevant
