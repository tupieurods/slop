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

## Implementation via Code Developer (Mandatory)

When implementing plan steps or any concrete code-change task, the **main agent delegates to the code-developer sub-agent** defined in `.claude/agents/code-developer.md`.

- Spawn one developer invocation per focused task/plan step. Provide the full task context, the relevant plan reference (if any), and explicit scope boundaries.
- The developer reads project rules itself, makes the change, runs `dotnet build src/SlopChat.slnx` (and `dotnet test` when tests are involved), and reports what it did.
- The main agent does **not** make code changes in parallel with a running developer, and does **not** skip the developer for "small" changes unless they are trivial single-line edits to non-code files.
- Model: `claude-sonnet-4.6`, reasoning effort: medium.

## Post-Change Review (Mandatory)

After the code-developer reports a successful build, **the main agent spawns** the code-reviewer sub-agent defined in `.claude/agents/code-reviewer.md`.

- The reviewer reads project rules independently and checks the diff for bugs, style violations, and regressions.
- The reviewer **reports issues only** — it does not modify code.
- The **main agent** feeds any issues back to the code-developer for fixes, then re-reviews.
- Do **not** skip this step, even for small changes.
- Model: `claude-opus-4.7`, reasoning effort: medium.
