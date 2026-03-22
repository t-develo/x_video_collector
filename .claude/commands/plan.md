# Plan

Activate the planner agent to create a detailed implementation strategy before writing any code.

## When to Use

Use `/plan` when:
- Starting a new sprint or feature
- Requirements are unclear
- Multiple layers (Domain/Application/Infrastructure) are affected
- Making architectural changes

## Process

The planner agent will:

1. **Restate requirements** — confirm understanding of what needs to be built
2. **Identify affected layers** — which of Domain / Application / Infrastructure / Functions changes
3. **Break into steps** — granular implementation steps with exact file paths
4. **Sequence work** — order to minimize context-switching and enable incremental testing
5. **Identify risks** — potential clean architecture violations or Azure constraints

**No code is written until you explicitly confirm the plan.**

## Output Format

The plan will include:
- Sprint context and branch name
- Affected files per layer
- Step-by-step implementation sequence
- Test plan (unit + integration)
- Risk assessment
- Success criteria (build green + tests pass)

## After Plan Approval

Proceed with `/tdd` to implement test-first, or begin implementation directly if tests already exist.
