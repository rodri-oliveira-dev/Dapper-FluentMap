# Mutation Candidate Catalog

Read this reference only for an explicitly exhaustive audit or when mutation semantics are unfamiliar. For focused analysis, use the smaller risk-ranked workflow in `SKILL.md`.

## Candidate categories

| Category | Typical changes | What a killing test must observe |
|---|---|---|
| Boundary | `<` ↔ `<=`, `>` ↔ `>=`, zero/one, first/last index | Exact value at and immediately around the boundary |
| Boolean/logic | `&&` ↔ `||`, negate/remove one condition, `true` ↔ `false` | Each condition independently changes asserted behavior |
| Return value | value ↔ default/empty/null, true ↔ false, count ±1 | The returned value or downstream state |
| Error/guard | remove guard, change exception/error type, swallow propagation | Invalid input and exact observable error semantics |
| Arithmetic | `+` ↔ `-`, `*` ↔ `/`, sign flip, increment ↔ decrement | Exact calculated result, not only a broad range |
| Collection | empty/non-empty, omit first/last item, order reversal | Contents, count, and order where relevant |
| State transition | skip assignment, retain old state, alter an existing update | Both result and resulting state |

## Dapper-FluentMap-specific candidates

- change explicit mapping vs convention vs Dapper fallback precedence;
- alter case-sensitive/case-insensitive member resolution;
- remove duplicate/invalid mapping guards;
- change `Ignore()` or Dommel-visible metadata behavior;
- alter cache keys or invalidation behavior;
- change nested/value-object member resolution;
- change analyzer diagnostic presence/severity/location;
- change source-generator output in a way that affects consumer compilation/runtime behavior;
- change exception type or error propagation;
- alter async/cancellation behavior where present.

## Equivalence and noise filters

Exclude generated output unless generated behavior itself is the contract under test, logging/formatting-only changes unless contractual, impossible domain values, redundant checks whose removal cannot affect behavior, and private representation changes no public input can distinguish.

## Exhaustive audit procedure

1. Enumerate meaningful candidates by production behavior, not token/operator.
2. State the public input and different original/mutant observations.
3. Map each candidate to covering tests and relevant assertions.
4. Classify obvious killed/equivalent candidates statically.
5. Execute every candidate that might be reported as **Survived**.
6. After a green run, re-check that the mutation is publicly observable.
7. Revert after each run and confirm the clean baseline at the end.
8. Count only executed or definitively killed/equivalent candidates in totals; disclose omitted scope.
