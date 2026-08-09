---
name: QSMPDLE Development Workflow
description: How the AI should analyze, modify, and verify QSMPDLE.
alwaysApply: true
---

# Development Workflow

## Before Changes

1. Understand the user's actual request.
2. Inspect the relevant existing implementation.
3. Search for similar functionality before creating new code.
4. Identify which feature owns the change.
5. Determine the smallest change that satisfies the request.

Do not invent requirements or implementation details.

If requirements are ambiguous and the ambiguity affects the implementation, ask before proceeding.

## Existing Code

Prefer extending existing functionality over creating parallel implementations.

Reuse existing:

- services
- interfaces
- models
- stores
- builders
- helpers
- components

Do not create abstractions merely because they are theoretically cleaner.

Do not introduce a new library or architectural pattern when the existing stack can reasonably solve the problem.

## Architecture

The architecture rules in `architecture.md` are constraints, not suggestions.

Do not silently violate them.

If the requested change conflicts with an architectural constraint:

1. explain the conflict
2. explain the available options
3. wait for a decision when the choice is architectural

## Implementation

Keep changes:

- focused
- incremental
- consistent with existing code
- as small as reasonably possible

Do not modify unrelated code.

Do not rewrite an entire feature to implement a small change.

## Verification

After making changes:

1. Inspect the resulting implementation.
2. Check for obvious compile-time errors.
3. Run relevant tests when available.
4. Run `dotnet build` when appropriate.
5. Verify DI registrations and dependencies when services were added or changed.

Never claim that code works, builds, or passes tests unless it was actually verified.

## Explanations

When fixing a bug:

1. explain the root cause
2. explain the fix
3. implement the fix

Prefer fixing the underlying cause over adding defensive workarounds.

When proposing architectural changes, explain the trade-offs before implementing them.

## Uncertainty

If you do not know something:

- say so
- inspect the code or documentation when possible
- do not invent APIs, framework behavior, project requirements, or existing implementations

## Scope

Only make changes necessary to accomplish the requested task.

Do not opportunistically refactor unrelated code.

Do not add comments that merely describe what the code does.

## Completion

After implementation, summarize:

- what changed
- important architectural decisions
- verification performed
- anything that remains uncertain
