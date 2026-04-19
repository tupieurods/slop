# Agent Guidelines

## Serena (Code Intelligence)

This project has **Serena** configured for C# code intelligence.

- When **reading or navigating code** (finding classes, methods, symbols, references), use **Serena tools** instead of `grep`/`glob` for `.cs` files.
- Serena must be aware of `.claude/rules/code_style.md` when generating or suggesting code.
- Activate the project first if not already active: project name is `slop`.

## Any Agent That Modifies Code

Before making any code changes, **always read**:

1. `.claude/rules/developer.md` — coding standards, target platform (.NET 10 / C# 14), patterns to follow and avoid
2. `.claude/rules/code_style.md` — formatting, naming conventions, member ordering

After changes, always run `dotnet build src/SlopChat.slnx` to verify compilation.

## Post-Change Review (Mandatory)

After every code change that compiles successfully, **spawn a sub-agent** using `.claude/agents/code-reviewer.md` to perform an independent review.

- The reviewer reads project rules independently and checks the diff for bugs, style violations, and regressions.
- The reviewer will **fix issues directly** if found and re-verify the build.
- Do **not** skip this step, even for small changes.
