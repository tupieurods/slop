# Method Signature & Parameter List Formatting

Method signatures and calls must use one of two formats — never a mix:

1. **All on one line** if it fits within 180 chars:
   ```csharp
   public void DoStuff(int a, string b, CancellationToken ct)
   ```

2. **Each parameter on its own line** with closing `)` on its own line:
   ```csharp
   public void DoStuff(
     int a,
     string b,
     CancellationToken ct
   )
   ```

**Forbidden**: opening `(` with line break, then all params crammed on one line:
```csharp
// ❌ WRONG
public void DoStuff(
  int a, string b, CancellationToken ct)
```

This applies to method declarations, method calls, and constructor invocations alike.
