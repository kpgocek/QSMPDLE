---
name: QSMPDLE Architecture
description: Architectural principles, boundaries, dependencies, and design constraints for QSMPDLE.
alwaysApply: true
---

# QSMPDLE Architecture

QSMPDLE follows a feature-first architecture.

The project is intentionally organized around business capabilities instead of technical layers.

The architecture prioritizes:

- maintainability
- readability
- low coupling
- high cohesion
- testability
- incremental feature development

Prefer simple, explicit solutions.

Performance optimizations are welcome but should not compromise code clarity.

---

## Architectural Layers

The logical dependency flow is:

```text
Presentation
    ↓
Application / Features
    ↓
Infrastructure
    ↓
Persistence / Browser Storage
```

Dependencies point downward.

Lower layers must never depend on higher layers.

The current application intentionally remains a single-project solution. Do not introduce multiple projects or assemblies unless there is a clear need.

---

## Feature Ownership

Business capabilities are represented as features.

Examples:

- Gameplay
- Statistics
- Sharing
- Sitemap
- Communication

A feature owns the application logic directly related to its business responsibility.

Prefer feature ownership over shared technical folders.

Do not create shared abstractions merely for convenience.

---

## Presentation

Blazor components belong to the presentation layer.

Components are responsible for:

- rendering UI
- receiving user input
- handling UI events
- invoking application services
- displaying application state
- subscribing to relevant application events

Components must remain thin.

Components must not:

- contain business rules
- calculate application statistics
- access `DbContext`
- execute SQL
- access browser LocalStorage APIs directly
- contain persistence logic

Business logic belongs in feature services.

---

## Feature Services

Feature services contain application logic and coordinate operations within their feature.

Services should expose meaningful domain operations rather than generic CRUD-style operations.

Prefer:

```csharp
gameService.MakeGuess(characterId);
```

over:

```csharp
gameService.UpdateGuess(...);
```

A service should have a focused responsibility.

Avoid large services or "God objects". Split a service when its responsibilities become unrelated or difficult to understand.

---

## Domain Models

Models represent application concepts.

Examples:

- `Character`
- `Game`
- `DailyGame`
- `GameState`
- `GuessResult`
- `PlayerStats`
- `GameSession`

Models may contain lightweight behavior related to the concept they represent.

Infrastructure concerns must never leak into domain or application models.

Do not expose EF Core entities directly to the UI.

---

## Infrastructure

Infrastructure contains implementation details required by the application.

Examples:

- EF Core
- SQLite
- LocalStorage
- repositories
- caching
- migrations

Application logic must not depend directly on infrastructure implementations when an abstraction provides a meaningful boundary.

Infrastructure implements abstractions required by features.

Infrastructure must not contain gameplay or presentation logic.

---

## Persistence

Persistence is an implementation detail.

Gameplay and other application features must not know about:

- SQL
- SQLite
- EF Core
- database-specific implementation details

Database access belongs behind feature/application abstractions and persistence implementations.

Repositories should be used when they provide a useful persistence boundary. Do not create repositories solely because every entity has a database table.

---

## Browser LocalStorage

Browser storage must be abstracted behind stores.

Examples:

- `IGameStateStore`
- `IPlayerStatsStore`

Components and gameplay logic must not directly access browser APIs or JSInterop for LocalStorage.

---

## State Management

Runtime state and persistent state are separate concepts.

Runtime state includes:

- `GameState`

Persistent browser-side state includes:

- `PlayerStats`

Persistent server-side gameplay records include:

- `GameSession`

Do not conflate runtime state with persisted statistics.

---

## Event-Driven Communication

Features should communicate through events when one feature only needs to notify another feature that something happened.

Typical flow:

```text
Gameplay
    ↓
GuessMadeEvent
    ↓
Statistics
    ↓
Persistence
    ↓
Charts
```

Events describe something that has already happened.

Prefer:

```text
GameStartedEvent
GuessMadeEvent
GameFinishedEvent
```

Avoid imperative event names such as:

```text
UpdateStatisticsEvent
```

Use direct service dependencies when actual interaction is required and an event would make the design unnecessarily indirect.

Events are not a requirement for every interaction.

---

## Feature Independence

Features should remain independently understandable and loosely coupled.

For example:

- Gameplay must not know how Statistics stores data.
- Statistics must not influence gameplay outcomes.
- Sharing must not contain gameplay rules.
- Sitemap must not contain UI logic.

If a feature only needs notification from another feature, prefer an event over a direct dependency.

Avoid circular dependencies between features.

---

## Dependency Injection

Services and other application dependencies should be registered through feature-specific extension methods.

Prefer:

```csharp
builder.Services
    .AddGameplay()
    .AddStatistics()
    .AddSharing()
    .AddPersistence();
```

Keep `Program.cs` focused on application composition and configuration.

Registration logic belonging to a feature should live in that feature's `ServiceCollectionExtensions`.

---

## Abstractions

Prefer interfaces when they define a meaningful boundary between:

- features
- application logic and infrastructure
- application logic and browser storage
- replaceable implementations

Do not create interfaces solely because a class exists.

Do not introduce abstractions merely for theoretical testability or architectural purity.

The project values simplicity over abstraction.

---

## Error Handling

Expected or recoverable failures should be represented as results, validation failures, or other explicit outcomes whenever practical.

Unexpected failures should throw exceptions.

Do not use exceptions as normal control flow.

The UI should present friendly error messages and must not expose exception details to users.

---

## Architectural Constraints

The following constraints must be preserved unless the architecture is explicitly changed:

- Components must not access `DbContext`.
- Components must not execute SQL.
- Components must not access LocalStorage directly.
- Components must not implement business rules.
- Services must not depend on Razor components.
- Services must not contain presentation logic.
- Infrastructure must not contain gameplay logic.
- Statistics must never determine gameplay outcomes.
- Gameplay must not know how statistics are persisted.
- Features must not create circular dependencies.
- EF Core entities must not be exposed directly to the UI.
- Business logic must not depend directly on infrastructure implementations when an appropriate abstraction exists.

If a requested change conflicts with one of these constraints, do not silently violate it.

Explain the conflict and the available design options before making an architectural change.

---

## Design Principles

Prefer:

- composition over inheritance
- explicit code over magic
- meaningful abstractions over abstraction for its own sake
- feature ownership over shared utilities
- low coupling
- high cohesion
- readable code
- incremental changes

Avoid introducing new architectural patterns unless there is a clear and concrete benefit.

Existing architectural conventions should be extended rather than replaced without a strong reason.

---

## Evolution

The current architecture is intentionally modular enough that features could be extracted into separate projects in the future.

A possible future split is:

```text
QSMPDLE.Web
QSMPDLE.Gameplay
QSMPDLE.Statistics
QSMPDLE.Infrastructure
QSMPDLE.Shared
```

Do not perform this split prematurely.

The current single-project organization is preferred until there is a clear need for multiple assemblies.
