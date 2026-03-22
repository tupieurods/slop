# Code Style & Conventions

Source: `resharper_settings.DotSettings` (ReSharper shared settings).

## Project setup
- Nullable reference types: enabled
- Implicit usings: enabled
- Namespace style: **block-scoped** (`namespace Foo { ... }`)

## `var` keyword
- **Never** use `var` for built-in or simple types — always use explicit type names.

## Braces
- **Required** for all `if`, `for`, `foreach`, `while` bodies.
- Embedded statements must always be on a **new line** (never on same line as keyword).

## Method / operator bodies
- Prefer **expression body** (`=>`) for methods and operators when possible.

## Spacing
- **No space** before parentheses in control flow keywords: `if(`, `for(`, `foreach(`, `while(`, `switch(`, `catch(`, `lock(`, `using(`, `fixed(`
- **No space** before `:` in base class list: `class Foo: Bar`
- **No space** after `operator` keyword

## Line length & wrapping
- Wrap limit: **180 characters**
- Wrap **before** binary operator
- Max **1 blank line** in code or declarations
- No blank lines after block statements; no blank lines around fields

## Attributes
- Always on a **separate line** — never on the same line as the member.

## Alignment
- Align multiline extends lists and multiline parameter lists
- Do **not** align multiline binary expression chains

## Naming (C#)
| Element | Convention |
|---|---|
| Private instance fields | `_camelCase` |
| Private static fields | `_camelCase` |
| Public/internal types, members, namespaces | `PascalCase` |
| Local variables, parameters | `camelCase` |

## Member ordering (within a class)
1. Public delegates
2. Public enums
3. *(grouped by access)*: non-static fields → properties/indexers
4. Static fields and constants
5. Events
6. Constructors (static first)
7. Interface implementations (grouped by interface)
8. All other members
9. Nested types
