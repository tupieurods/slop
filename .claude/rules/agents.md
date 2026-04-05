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
