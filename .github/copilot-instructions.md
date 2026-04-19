See `.claude/claude.md` for project rules and guidelines.

## Post-Change Review

After every code change, spawn a `code-review` sub-agent to independently review the diff.
The reviewer follows `.claude/rules/developer.md` and `.claude/rules/code_style.md`, fixes issues directly, and verifies the build.
