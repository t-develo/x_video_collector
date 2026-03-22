# Save Session

Save the current work context to `.claude/sessions/` so it can be restored in the next iOS Claude Code session.

## When to Use

Run `/save-session` before ending a session to preserve:
- What was built and what works
- What failed (critical — prevents retrying failed approaches)
- Exact next step to resume from

## Process

1. Determine today's date and generate a filename: `YYYY-MM-DD-<short-description>-session.md`
   - Use the format: `$CLAUDE_PROJECT_DIR/.claude/sessions/YYYY-MM-DD-<short-description>-session.md`
   - `short-description`: kebab-case summary of what was worked on (e.g., `domain-entities`, `video-usecase`)
2. Write the session file using the template below
3. Confirm the file was created with the path

## Session File Template

```markdown
# Session: {YYYY-MM-DD} — {Short Description}

## Sprint Context
Sprint {N} — Branch: feature/sprint{N}/{branch-name}

## What We Are Building
{1-2 sentence description of the feature/task}

## What WORKED ✅
- {What succeeded, with verification evidence (e.g., "dotnet test passed: 42 tests")}
- {Include git SHA or commit message if committed}

## What Did NOT Work ❌
<!-- THIS IS THE MOST IMPORTANT SECTION — be specific about exact errors -->
- {Exact error message or failure reason}
- {Why the approach failed, so next session won't retry it}

## What Has NOT Been Tried Yet
- {Promising untested approaches}

## Current State of Files
| File | Status |
|------|--------|
| {path} | Created / Modified / Broken |

## Decisions Made
- {Architectural or design choices that are settled}

## Blockers & Open Questions
- {Unresolved issues}

## Exact Next Step
{One concrete action to take at the start of the next session}
```

## After Saving

**Important for iOS**: Session files are stored in the repo. Commit and push so the file persists across devices:

```
git add .claude/sessions/
git commit -m "chore: save session YYYY-MM-DD"
git push
```
