# C# Code Style Rules

Derived from `resharper_settings.DotSettings`, refined by user formatting preferences.

## Indentation
- **2 spaces** — no tabs.

## `var` usage
- Use `var` when the type is **obvious from context** (e.g., `var history = GetHistory(chatId)`, `var models = await _openRouter.GetModelsAsync(ct)`).
- Use **explicit types** for built-in types, ambiguous expressions, or when the type isn't clear from the right-hand side.

## Braces
- Braces are **required** for all `if`, `for`, `foreach`, `while` bodies, even single-line ones.
- Embedded statements must always be on a **new line** (never same line as keyword).

## Method / operator bodies
- Prefer **expression body** (`=>`) for methods and operators when possible (single-expression methods).

## Namespace style
- Use **block-scoped** namespaces (`namespace Foo { ... }`), not file-scoped.

## Spacing
- **No space** before parentheses in keywords: `if(`, `for(`, `foreach(`, `while(`, `switch(`, `catch(`, `lock(`, `using(`, `fixed(`
- **No space** before `:` in base type list: `class Foo: Bar`
- **No space** after `operator` keyword

## Method signatures & parameter lists
- If a method signature fits on one line, keep it on one line.
- If it doesn't fit, put **each parameter on its own line** with the closing `)` on its own line.
- **Never** mix: e.g. opening `(` on a new line followed by all parameters crammed on one line is forbidden.
  ```csharp
  // ✅ All on one line
  public void DoStuff(int a, string b, CancellationToken ct)

  // ✅ Each parameter on its own line
  public void DoStuff(
    int a,
    string b,
    CancellationToken ct
  )

  // ❌ Mixed — parameters on new line but crammed together
  public void DoStuff(
    int a, string b, CancellationToken ct)
  ```
- The same rule applies to method **calls** and **constructor invocations**.

## Line length & wrapping
- Wrap limit: **180 characters**
- Wrap **before** binary operator (not after)
- Max **1 blank line** in code or declarations
- No blank lines after block statements
- No blank lines around fields
- **Closing parenthesis on its own line** for multi-line method calls / declarations:
  ```csharp
  // ✅ Correct
  await bot.SendMessage(
    chatId,
    text,
    cancellationToken: ct
  );

  // ❌ Wrong
  await bot.SendMessage(
    chatId,
    text,
    cancellationToken: ct);
  ```

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

## Member ordering (within a class)
1. Instance readonly fields (`_camelCase`)
2. Static readonly fields and constants
3. Properties and indexers
4. Constructors
5. Public methods
6. Private methods
7. Nested types

## Other
- Remove unused `using` directives.
- Prefer positional arguments over named arguments when the meaning is clear from context.
