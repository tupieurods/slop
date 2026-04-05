# Code Style & Conventions

Source: `resharper_settings.DotSettings` + user formatting preferences.

## Indentation
- **2 spaces**, no tabs.

## `var` keyword
- Use `var` when type is **obvious from context** (e.g., `var history = GetHistory(chatId)`).
- Use **explicit types** for built-in types or when the type isn't clear from the right-hand side.

## Braces
- **Required** for all `if`, `for`, `foreach`, `while` bodies.
- Embedded statements on a **new line** (never same line as keyword).

## Method / operator bodies
- Prefer **expression body** (`=>`) for single-expression methods.

## Namespace style
- **Block-scoped** (`namespace Foo { ... }`), not file-scoped.

## Spacing
- **No space** before parentheses in control flow keywords: `if(`, `for(`, `foreach(`, `while(`, `switch(`, `catch(`, `lock(`, `using(`, `fixed(`
- **No space** before `:` in base class list: `class Foo: Bar`
- **No space** after `operator` keyword

## Line length & wrapping
- Wrap limit: **180 characters**
- Wrap **before** binary operator
- Max **1 blank line** in code or declarations
- No blank lines after block statements; no blank lines around fields
- **Closing parenthesis on its own line** for multi-line method calls/declarations (closing `)` or `);` goes on a new line, aligned with the call start)

## Attributes
- Always on a **separate line**.

## Naming (C#)
| Element | Convention |
|---|---|
| Private instance fields | `_camelCase` |
| Private static fields | `_camelCase` |
| Public/internal types, members, namespaces | `PascalCase` |
| Local variables, parameters | `camelCase` |

## Member ordering (within a class)
1. Instance readonly fields
2. Static readonly fields and constants
3. Properties and indexers
4. Constructors
5. Public methods
6. Private methods
7. Nested types

## Other
- Remove unused `using` directives.
- Prefer positional arguments over named when meaning is clear.
