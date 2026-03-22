# Build Fix

Systematically resolve .NET build errors with minimal changes.

## Process

1. **Detect**: Run `dotnet build 2>&1` and capture all errors
2. **Organize**: Group errors by file, sequence by dependency order
3. **Fix**: Address one error at a time, re-run build after each fix
4. **Verify**: `dotnet test` passes (or only pre-existing failures remain)

## Commands

```bash
# Full build
dotnet build

# Build specific project
dotnet build src/api/XVideoCollector.Domain/

# Run tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~RegisterVideoUseCaseTests"
```

## Common Fixes

| Error Code | Cause | Fix |
|------------|-------|-----|
| `CS8600` | Null to non-nullable | Add null check or `!` |
| `CS8618` | Uninitialized non-nullable | Initialize in ctor or `= null!` (EF only) |
| `CS8603` | Possible null return | Add null check or change return type |
| `CS0535` | Interface not implemented | Add missing method |
| `CS1061` | Member doesn't exist | Fix typo or add `using` |
| `CS0246` | Type not found | Add `using` directive |

## Constraints

- Fix errors only — do NOT refactor, change architecture, or add features
- If a fix requires structural redesign, stop and ask the user
- Changed lines must be < 5% of affected files

## Success Criteria

- `dotnet build` exits with code 0
- `dotnet test` runs without new failures
