# Coding Conventions

This document describes coding conventions used throughout QSMPDLE.

The goal is consistency, readability and maintainability.

---

# General Principles

Prefer:

- simple code
- explicit code
- readable code

Avoid clever solutions unless they significantly improve maintainability.

Code is read much more often than it is written.

---

# C# Style

## Properties

Prefer expression-bodied members only for trivial code.

Good

```csharp
public bool IsWon => Guesses.Count == MaxGuesses;
```

Avoid

```csharp
public bool IsWon
{
    get
    {
        ...
    }
}
```

for simple expressions.

---

## var

Use `var` when the type is obvious.

Good

```csharp
var character = repository.Get(id);
```

Avoid

```csharp
Character character = repository.Get(id);
```

unless readability improves.

---

## Nullability

Nullable reference types are enabled.

Never suppress warnings with `!` unless absolutely necessary.

Prefer proper null handling.

---

## Async

Always prefer async APIs.

Never block using:

- .Result
- .Wait()

Methods returning asynchronous work should end with `Async`.

---

# Dependency Injection

Always depend on interfaces.

Good

```csharp
IGameService
```

Avoid

```csharp
GameService
```

unless registration requires it.

Never use service locators.

---

# Razor Components

Components should remain thin.

Allowed:

- rendering
- event handlers
- calling services

Avoid:

- business logic
- statistics calculations
- SQL
- LocalStorage access

Large components should be split into reusable child components.

---

# Services

Services contain application logic.

A service should have one responsibility.

Avoid services exceeding roughly 300–500 lines without good reason.

---

# Models

Models represent concepts.

Avoid "God objects".

DTOs should remain immutable whenever practical.

---

# Events

Prefer publishing events instead of tightly coupling features.

Events should describe something that already happened.

Examples:

- GuessMadeEvent
- GameFinishedEvent

Avoid imperative event names.

Bad:

```
UpdateStatisticsEvent
```

Good:

```
GameFinishedEvent
```

---

# Exceptions

Throw exceptions only for unexpected situations.

Expected failures should be represented by results or validation.

---

# Logging

Log:

- unexpected failures
- startup
- background workers

Avoid logging every successful operation.

---

# Naming

Interfaces

```
IGameService
```

Services

```
GameService
```

Stores

```
PlayerStatsStore
```

Builders

```
ShareTextBuilder
```

Helpers

```
CharacterImageHelper
```

Events

```
GuessMadeEvent
```

Enums should never contain prefixes.

Good

```
Practice
Daily
Archive
```

Avoid

```
GameModePractice
```

---

# Folder Organization

Keep related files together.

Prefer

```
Gameplay
    Models
    Services
```

over

```
Models
Services
Repositories
```

---

# CSS

Each reusable component should own its CSS when practical.

Avoid one massive stylesheet.

---

# JavaScript

Use JavaScript only when Blazor cannot provide the required functionality.

Keep JS isolated inside wwwroot/js.

---

# Comments

Prefer self-documenting code.

Comment:

- why

Avoid commenting:

- what

Bad

```csharp
// Increment counter
counter++;
```

Good

```csharp
// Character images are cached because the wiki is rate-limited.
```

---

# Performance

Optimize only after measuring.

Prefer readable code over micro-optimizations.

---

# Consistency

When adding new code, follow the surrounding style instead of introducing a different one.
