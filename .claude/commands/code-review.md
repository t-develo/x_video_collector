# Code Review

Run a systematic code review of uncommitted changes before committing.

## Process

1. Identify changed files: `git diff --name-only HEAD`
2. Review each file against security, architecture, and quality categories
3. Generate a report with severity ratings

## Security (CRITICAL — blocks commit)

- Hardcoded secrets, connection strings, or API keys
- SQL injection (string-concatenated EF queries)
- XSS: `innerHTML` usage in JS — must use `textContent` / `clearChildren()`
- Command injection: raw user input passed to yt-dlp/ffmpeg
- Missing authentication check on Function endpoints

## Architecture (HIGH — blocks commit)

- Dependency rule violation: Infrastructure referenced from Application/Domain
- `DateTimeOffset.UtcNow` used directly (must inject `TimeProvider`)
- `Task.Run` fire-and-forget in Functions layer
- `SaveChangesAsync` inside Repository (must use `IUnitOfWork`)
- `var` or `.then()` chains in JavaScript

## Code Quality (HIGH)

- Functions > 50 lines
- Missing `Async` suffix on async methods
- Non-sealed classes (should be sealed unless inheritance intended)
- `UpdatedAt` not updated in entity state-change methods
- `EF.Functions.Like` without wildcard escaping

## Best Practices (LOW)

- Test methods not following `MethodName_Condition_ExpectedResult`
- Static readonly Mock instances
- Missing boundary value tests
- `innerHTML = ''` to clear DOM (must use `clearChildren()`)

## Report

```
## Code Review: {date}

### CRITICAL
{issues or "None"}

### HIGH
{issues or "None"}

### LOW
{issues or "None"}

### Verdict: ✅ Approve | ⚠️ Warning | 🚫 Block
```
