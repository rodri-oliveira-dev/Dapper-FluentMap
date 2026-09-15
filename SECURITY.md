# Security Policy

## Supported versions

Security fixes are prioritized for the latest supported release line and the current `master` branch. Older versions may receive fixes when the impact is high and a safe backport is practical, but long-term support is not implied unless explicitly documented.

## Reporting a vulnerability

Please report suspected vulnerabilities privately instead of opening a public issue containing exploit details, secrets, or sensitive reproduction data.

Prefer GitHub private vulnerability reporting or Security Advisories when the repository exposes the **Report a vulnerability** option. Include the affected package/version or commit, reproduction steps, expected impact, relevant prerequisites, and any known mitigation.

If private GitHub reporting is unavailable, contact the maintainer through the contact methods exposed on the GitHub profile and request a private reporting channel before sharing sensitive details.

## Automated repository security controls

The repository uses complementary controls across source code, dependencies, quality, and release automation:

- CodeQL performs semantic C# security analysis on pull requests targeting `master`, pushes to `master`, and a weekly schedule;
- Dependency Review evaluates dependency changes introduced by pull requests and blocks additions with vulnerabilities of `high` severity or above;
- Dependabot maintains NuGet packages, the .NET SDK, and GitHub Actions references;
- SonarQube Cloud and the main CI workflow provide complementary static analysis, compatibility, build, test, package, and governance validation.

The CodeQL workflow uses the SDK selected by `global.json`, the preferred `Dapper.FluentMap.slnx` solution, manual build mode, SHA-pinned actions, and least-privilege workflow permissions. This keeps security analysis aligned with the repository's normal build contract instead of introducing an independent autobuild path.

Dependency Review is intentionally blocking for relevant findings. Do not add `continue-on-error` to security gates solely to keep a pull request green; findings should be triaged and resolved or explicitly justified.
