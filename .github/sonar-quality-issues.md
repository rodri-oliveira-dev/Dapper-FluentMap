# Sonar Quality Issues synchronization

The `sonar-quality-issues.yml` workflow synchronizes open SonarQube Cloud findings with software-quality impact severity `HIGH` or `BLOCKER` into GitHub Issues.

## Runtime

- Scheduled weekly on Mondays at 09:17 America/Sao_Paulo (12:17 UTC).
- Can also be started manually with `workflow_dispatch`.
- Runs only when the repository variable `SONAR_CI_ENABLED` is set to `true`.
- Reuses the repository secret `SONAR_TOKEN`.
- Creates or reuses the label `Sonar Quality Issues`.

## Idempotency and provenance

Each managed GitHub issue stores the immutable Sonar issue key inside the workflow-managed body section. Existing items are accepted as synchronization targets only when they are GitHub Issues (not pull requests), were created by `github-actions[bot]`, and contain the expected managed delimiters.

This prevents duplicate issues and avoids treating user-authored content that happens to contain a public Sonar issue key as a canonical synchronization target.

Manual notes written outside the managed section are preserved when Sonar data is refreshed.

## Lifecycle

If a tracked Sonar finding remains open with `HIGH` or `BLOCKER` impact, its GitHub issue is updated and reopened when necessary. The workflow intentionally does not automatically close GitHub issues when a finding disappears from the query, avoiding accidental closures caused by temporary Sonar inconsistencies or severity reclassification.
