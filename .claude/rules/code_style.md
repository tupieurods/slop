# C# Code Style Rules

Derived from `resharper_settings.DotSettings`.

## `var` usage
- **Never** use `var` for built-in types or simple types — always use explicit type names.

## Braces
- Braces are **required** for all `if`, `for`, `foreach`, `while` bodies, even single-line ones.
- Embedded statements must always be on a **new line** (never same line as keyword).

## Method / operator bodies
- Prefer **expression body** (`=>`) for methods and operators when possible.

## Namespace style
- Use **block-scoped** namespaces (`namespace Foo { ... }`), not file-scoped.

## Spacing
- **No space** before parentheses in keywords: `if(`, `for(`, `foreach(`, `while(`, `switch(`, `catch(`, `lock(`, `using(`, `fixed(`
- **No space** before `:` in base type list: `class Foo: Bar`
- **No space** after `operator` keyword

## Line length & wrapping
- Wrap limit: **180 characters**
- Wrap **before** binary operator (not after)
- Max **1 blank line** in code or declarations
- No blank lines after block statements
- No blank lines around fields

## Attributes
- Attributes are always placed on a **separate line** — never on the same line as the member.

## Alignment
- Align multiline extends lists and multiline parameter lists
- Do **not** align multiline binary expression chains

## Naming conventions (C#)
| Element | Style |
|---|---|
| Private instance fields | `_camelCase` (prefix `_`) |
| Private static fields | `_camelCase` (prefix `_`) |
| Public/internal members, types, namespaces | `PascalCase` (ReSharper default) |
| Local variables, parameters | `camelCase` (ReSharper default) |

## Member ordering (within a class — Default Pattern)
1. Public delegates
2. Public enums
3. *(grouped by access modifier)*:
   - Non-static fields
   - Properties and indexers
4. Static fields and constants
5. Events
6. Constructors (static constructor first)
7. Interface implementations (grouped by interface)
8. All other members
9. Nested types
