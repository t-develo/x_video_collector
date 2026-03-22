# Checkpoint

Create, verify, or list git-based checkpoints during development.

## Usage

- `/checkpoint create <name>` — create a checkpoint at the current state
- `/checkpoint verify <name>` — compare current state against a checkpoint
- `/checkpoint list` — show all saved checkpoints
- `/checkpoint clear` — remove old checkpoints, keep 5 most recent

## Create

1. Verify working tree is in a known state: `git status`
2. Run tests: `dotnet test --no-build 2>&1 | tail -5`
3. Create a git stash or commit with the checkpoint label:
   ```
   git stash push -m "checkpoint: {name}"
   ```
   Or if ready to commit:
   ```
   git commit -m "chore: checkpoint {name}"
   ```
4. Log to `.claude/checkpoints.log`:
   ```
   {ISO timestamp} | {name} | {git SHA} | tests: {pass/fail count}
   ```

## Verify

Compare current state to checkpoint `{name}`:
- Files changed since checkpoint
- Test results: pass/fail delta
- Build status

## List

Read `.claude/checkpoints.log` and display as a table:

| Name | Timestamp | Git SHA | Status |
|------|-----------|---------|--------|

## Typical Workflow

```
/checkpoint create sprint3-start
# ... implement use cases ...
/checkpoint create after-usecases
# ... refactor ...
/checkpoint verify after-usecases   # confirm no regressions
```
