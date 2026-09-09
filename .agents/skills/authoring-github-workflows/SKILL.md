---
name: authoring-github-workflows
description: "Author and review GitHub Actions workflow YAML safely so syntactically-valid YAML can't ship a workflow that GitHub Actions refuses to run. USE FOR: editing, adding, or reviewing any file under .github/workflows/, writing run-name/name/if/env/run values that contain ${{ }} expressions, diagnosing workflow-load failures, deciding when a workflow scalar must be quoted, and validating workflows with actionlint. DO NOT USE FOR: non-GitHub-Actions YAML. SCOPE: syntactic/structural workflow correctness; pair with ci-release-governance for semantic CI/release design."
license: MIT
---

# Authoring GitHub Actions Workflows Safely

GitHub Actions workflow files are YAML, but valid YAML is not the same as a valid workflow. A file can parse as YAML yet still be rejected by GitHub Actions before any job starts.

> **Repository integration:** `AGENTS.md` and the existing Dapper-FluentMap workflows are authoritative. Preserve the repository's SHA-pinned actions, least-privilege permissions, release/recovery semantics, `master` default branch, Sonar Quality Gate, and multi-package publishing rules. Use `ci-release-governance` for what a workflow should do; use this skill for whether the workflow is structurally valid and safely authored.

## When to Use

- Editing, adding, or reviewing `.github/workflows/*.yml`.
- Writing `run-name`, `name`, `if`, `env`, `with`, or `run` values containing `${{ }}` expressions.
- Diagnosing a workflow rejected before jobs start.
- Reviewing download steps, permissions, expressions, quoting, or reusable workflow syntax.

## The `#` trap

In an unquoted YAML scalar, a space followed by `#` begins a comment and can truncate a GitHub expression.

```yaml
# BAD
run-name: ${{ inputs.pr_number != '' && format('Evaluate PR #{0}', inputs.pr_number) || '' }}

# GOOD
run-name: "${{ inputs.pr_number != '' && format('Evaluate PR #{0}', inputs.pr_number) || '' }}"
```

Quote expression-bearing scalars whenever literal `#`, colon-space, leading special characters, or significant whitespace make YAML interpretation ambiguous.

## Workflow

### 1. Identify changed workflows

```bash
git diff --name-only origin/master... -- .github/workflows/
```

### 2. Inspect repository semantics before editing

For release-related changes, read the existing `release.yml`, recovery workflow, `eng/package-catalog.json`, and relevant publishing scripts. Do not replace established behavior with a generic workflow pattern.

### 3. Validate with actionlint

Prefer a repository-owned pinned validation command when available. For manual validation:

```bash
ACTIONLINT_VERSION=1.7.12
ACTIONLINT_SHA256=8aca8db96f1b94770f1b0d72b6dddcb1ebb8123cb3712530b08cc387b349a3d8
curl \
  --fail \
  --silent \
  --show-error \
  --location \
  --proto '=https' \
  --proto-redir '=https' \
  --output actionlint.tar.gz \
  "https://github.com/rhysd/actionlint/releases/download/v${ACTIONLINT_VERSION}/actionlint_${ACTIONLINT_VERSION}_linux_amd64.tar.gz"
echo "${ACTIONLINT_SHA256}  actionlint.tar.gz" | sha256sum -c -
tar -xzf actionlint.tar.gz actionlint
./actionlint -shellcheck= -pyflakes= -color .github/workflows/*.yml
```

Pin both version and checksum. Restrict both initial and redirected downloads to HTTPS.

The repository also uses GitHub-workflow JSON schema validation; preserve that existing gate rather than substituting actionlint for it.

### 4. Review security-sensitive workflow behavior

- keep actions SHA-pinned;
- keep permissions minimal and job-scoped when practical;
- do not expose secrets to fork PRs;
- do not add `continue-on-error` to bypass required quality/release checks;
- do not weaken NuGet OIDC, artifact validation, release recovery, or Sonar Quality Gate behavior;
- for network downloads that follow redirects, enforce HTTPS redirects and validate checksums where feasible.

## Validation

- [ ] Changed workflow files pass repository schema validation
- [ ] `actionlint` exits 0 when used
- [ ] Risky `${{ }}` scalars are quoted
- [ ] Actions remain SHA-pinned
- [ ] Permissions remain least privilege
- [ ] Redirect-following downloads enforce HTTPS
- [ ] Existing release, recovery, CI Gate, and Sonar semantics remain intact unless intentionally changed

## References

- [actionlint](https://github.com/rhysd/actionlint)
- [GitHub Actions workflow syntax](https://docs.github.com/actions/using-workflows/workflow-syntax-for-github-actions)
- [`ci-release-governance`](../ci-release-governance/SKILL.md)
