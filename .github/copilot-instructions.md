See `.claude/CLAUDE.md` for project rules and guidelines.

## Post-Change Review

After every code change, the main agent spawns the `code-reviewer` sub-agent (defined in `.claude/agents/code-reviewer.md`) to independently review the diff.

The reviewer reads `.claude/rules/developer.md` and `.claude/rules/code_style.md`, **reports issues only**, and does not modify code or run the build itself. The main agent routes any reported issues back to the `code-developer` sub-agent for fixes, then re-reviews.
