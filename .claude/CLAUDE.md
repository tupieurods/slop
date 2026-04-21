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
| `.claude/agents/code-reviewer.md` | Reviews the resulting diff, reports issues only (Sonnet 4.6, medium effort) |

Main agent flow for implementation work: **plan → spawn code-developer → on green build, spawn code-reviewer → feed issues back to code-developer → repeat until clean**.

## Important

1. Git Commit & Push Rules
- Never commit unless explicitly told
- Never squash/force-push unless explicitly told
- Never push to an active PR without explicit ask (even in autopilot)
- Prefer new commits over amending (exceptions: asked to amend, or minor fix to unpushed broken commit)