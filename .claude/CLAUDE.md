# Telegram AI chatbot

## Rules

| File | Purpose |
|---|---|
| `.claude/rules/developer.md` | Coding standards, .NET 10 / C# 14, patterns |
| `.claude/rules/code_style.md` | Formatting, naming, member ordering |
| `.claude/rules/agents.md` | Serena usage + what every code-changing agent must read |
| `.claude/rules/project_structure.md` | Solution layout, projects, deployment |

## Agents

| File | Purpose |
|---|---|
| `.claude/agents/code-developer.md` | Implements plan steps / code changes (Sonnet 4.6, medium effort) |
| `.claude/agents/code-reviewer.md` | Reviews the resulting diff, reports issues only (Opus 4.7, medium effort) |

Main agent flow for implementation work: **plan → spawn code-developer → on green build, spawn code-reviewer → feed issues back to code-developer → repeat until clean**.

## Important

1. Git Commit & Push Rules
- Never commit unless explicitly told
- Never squash/force-push unless explicitly told
- Never push to an active PR without explicit ask (even in autopilot)
- Prefer new commits over amending (exceptions: asked to amend, or minor fix to unpushed broken commit)

2. Documentation Maintenance (Mandatory)
After any code change, the main agent must check whether `README.md` and `.claude/rules/project_structure.md` are still accurate and update them in the same task if the change affects them. No separate request from the user is required.

Update `README.md` when the change affects user-facing behavior, e.g.:
- A command is added, removed, renamed, or its arguments / semantics change
- A configuration env var is added, removed, or renamed
- A documented feature changes meaningfully (new modality, new flow, new default)

Update `.claude/rules/project_structure.md` when the change affects repository/solution layout, e.g.:
- A project, top-level folder, or key service file is added, removed, renamed, or moved
- A new responsibility large enough to deserve a row in the "Key Services" table is introduced
- Build/test/deployment commands or paths change

Purely internal refactors (extracting private helpers, renaming locals, reformatting) do not require doc updates. When in doubt, prefer updating over leaving stale.