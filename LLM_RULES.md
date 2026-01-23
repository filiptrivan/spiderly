# Cursor Coding Guidelines

## Commenting

- Avoid unnecessary comments. Only write comments when they provide essential clarification or context.

## .NET (C#)

- **Avoid `var`** — Use explicit types unless working with anonymous types.
- **Entity relationships** — For one-to-many relationships in entities, always use `List<T>`. Other collection types may not work properly.
- **List initialization** — Always initialize lists immediately with `new()`.
- **Object instantiation**
  - Prefer shorthand initialization: `Class c = new();` — but only when the constructor has no parameters.
  - If the constructor has parameters, use object initializer syntax:  
    `Class c = new Class { /* property assignments */ };`
