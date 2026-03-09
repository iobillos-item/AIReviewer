# Coding Standards

- All public methods must have XML documentation comments.
- Use meaningful variable and method names. No single-letter variables except in loops.
- Maximum method length: 30 lines. Refactor if exceeded.
- No magic numbers or strings. Use constants or configuration.
- All async methods must end with the `Async` suffix.
- Use `var` only when the type is obvious from the right-hand side.
- Prefer LINQ over manual loops where readability is not sacrificed.
- No nested ternary expressions.
