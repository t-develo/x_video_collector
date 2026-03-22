# Resume Session

Load and resume prior work from a saved session file in `.claude/sessions/`.

## Usage

- `/resume-session` — loads the most recent session file
- `/resume-session 2026-03-22` — loads the session from that date
- `/resume-session .claude/sessions/2026-03-22-domain-entities-session.md` — loads a specific file

## Process

1. **Locate file** — search `$CLAUDE_PROJECT_DIR/.claude/sessions/` for the matching file
   - No argument: find the file with the most recent date in the filename
   - Date argument: find the file matching that date
   - Path argument: use that exact file
2. **Read completely** — read the entire session file without summarizing
3. **Provide structured briefing** — output the report below
4. **Await direction** — do NOT begin work automatically; wait for explicit instruction

## Briefing Format

```
## Session Restored: {filename}

**Project**: XVideoCollector
**Building**: {What We Are Building from file}
**Sprint**: {Sprint Context from file}
**Status**: {working | in-progress | blocked}

### Completed in Last Session
{Summary of What WORKED}

### ⚠️ Do NOT Retry
{What Did NOT Work — exact failures listed to prevent repeating them}

### Untried Approaches
{What Has NOT Been Tried Yet}

### File States
{Current State of Files table}

### Open Questions / Blockers
{Blockers & Open Questions}

### Recommended Next Step
{Exact Next Step from file}
```

## Constraints

- Session files are read-only — never modify them
- If the file doesn't exist, list available sessions from `.claude/sessions/`
- Sessions older than 14 days include a timestamp notice
- If `.claude/sessions/` is empty, prompt the user to run `/save-session` at the end of their next session
