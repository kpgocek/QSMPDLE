---
name: QSMPDLE Project Structure
description: Repository organization and file-placement rules for QSMPDLE.
alwaysApply: true
---

# QSMPDLE Project Structure

QSMPDLE uses feature-first organization.

Organize code around business capabilities rather than technical layers.

The repository should make it easy to find all code belonging to a feature without searching through global folders such as:

```text
Services
Models
Repositories
Controllers
```

Prefer feature ownership.

---

## Top-Level Structure

The application is organized approximately as:

```text
web
├── Components
├── Features
├── Infrastructure
├── Themes
├── Workers
└── wwwroot
```

Keep top-level folders focused on their defined responsibility.

Do not create new top-level folders when an existing architectural area already owns the functionality.

---

## Components

`Components` contains Blazor UI.

Components may:

- render data
- receive user input
- handle UI events
- invoke feature services
- subscribe to events

Components must not contain:

- business rules
- statistics calculations
- database access
- SQL
- direct LocalStorage access
- persistence implementation details

### Current Structure

```text
Components
├── Game
│   ├── Modules
│   └── Gameplay.razor
│
├── Statistics
│   └── Charts
│
└── UX
    ├── Dialogs
    ├── Navigation
    └── StatDisplay
```

#### Game

Contains gameplay UI.

Examples:

- guess cards
- guess tables
- character selectors
- portrait reveal components

#### Statistics

Contains statistics presentation.

Charts should receive prepared data/models.

Charts should not calculate statistics themselves.

#### UX

Contains reusable presentation components such as:

- dialogs
- navigation
- stat cards
- chips

---

## Features

Business logic belongs inside `Features`.

Each feature owns the code directly related to its business responsibility.

Typical structure:

```text
Features
└── FeatureName
    ├── Models
    ├── Services
    ├── Builders
    ├── Helpers
    └── ServiceCollectionExtensions.cs
```

Only create directories that are actually needed.

Do not create empty or speculative layers.

---

## Gameplay

`Features/Gameplay` owns the core game logic.

It may contain:

- `Game`
- `DailyGame`
- `GameState`
- `GuessResult`
- character comparison logic
- gameplay services

Gameplay is responsible for:

- creating games
- validating guesses
- comparing characters
- managing runtime game state
- publishing gameplay events

Gameplay must not contain statistics persistence logic.

---

## Statistics

`Features/Statistics` owns statistics-related application logic.

It may contain:

- player statistics
- game sessions
- retention calculations
- global statistics
- aggregated statistics models
- statistics services

Statistics consumes gameplay events and persists or exposes statistical data.

Statistics must never modify gameplay outcomes.

---

## Sharing

`Features/Sharing` owns shareable-content generation.

It may contain:

- share builders
- clipboard-related application services

Do not place gameplay rules in Sharing.

---

## Communication

`Features/Communication` contains application events and event-bus communication.

Examples:

```text
GameStartedEvent
GuessMadeEvent
GameFinishedEvent
```

Keep events focused on communication between application features.

Do not place business logic in event classes.

---

## Sitemap

`Features/Sitemap` contains sitemap generation logic.

It is responsible for generating search-engine-friendly URLs and sitemap data.

No UI logic belongs here.

---

## Infrastructure

`Infrastructure` contains implementation details shared by application features.

Examples:

- EF Core
- SQLite
- LocalStorage
- repositories
- migrations
- caching
- database initialization

Do not place business logic in Infrastructure.

---

## Persistence

Database-specific implementation belongs under the persistence area of `Infrastructure`.

Typical responsibilities include:

- `DbContext`
- repositories
- EF Core configuration
- migrations
- database initialization

UI components must never access `DbContext` directly.

Feature logic should depend on application abstractions rather than database-specific implementations.

---

## LocalStorage

Browser persistence belongs under the LocalStorage area of `Infrastructure`.

Examples:

```text
IGameStateStore
IPlayerStatsStore
LocalStorageGameStateStore
LocalStoragePlayerStatsStore
```

The stores hide browser persistence details from components and application logic.

Do not access LocalStorage APIs or JSInterop directly from gameplay components.

---

## Themes

`Themes` contains MudBlazor theme and presentation configuration.

Keep this folder limited to presentation configuration.

Do not place application or business logic here.

---

## Workers

`Workers` contains background services.

Current responsibility includes:

- refreshing statistics materialized views

Workers must not contain UI logic.

Workers should coordinate background operations rather than becoming another location for feature business logic.

---

## wwwroot

`wwwroot` contains static assets.

Examples:

- CSS
- JavaScript
- fonts
- graphics
- third-party static libraries

Do not place application or business logic in `wwwroot`.

JavaScript should only be used when Blazor cannot provide the required functionality.

Project JavaScript belongs under:

```text
wwwroot/js
```

Reusable component CSS should live alongside its component when practical.

Avoid one massive global stylesheet.

---

## Feature-to-Folder Mapping

When adding functionality, first determine which feature owns the behavior.

Examples:

```text
Character comparison
    → Features/Gameplay

Player retention
    → Features/Statistics

Share text generation
    → Features/Sharing

Application events
    → Features/Communication

Sitemap generation
    → Features/Sitemap
```

If functionality clearly belongs to an existing feature, extend that feature.

Do not create a new feature simply because the change introduces a new class.

---

## Where New Code Goes

Use these rules when deciding where to put a new file.

### Business model

Put it in the owning feature:

```text
Features/<Feature>/Models
```

### Application service

Put it in the owning feature:

```text
Features/<Feature>/Services
```

### Builder

Put it in the owning feature:

```text
Features/<Feature>/Builders
```

### Pure helper

Put it in the owning feature:

```text
Features/<Feature>/Helpers
```

Prefer pure helpers.

Do not create shared helpers unless multiple features genuinely own the concept.

### Feature DI registration

Put it in:

```text
Features/<Feature>/ServiceCollectionExtensions.cs
```

### UI

Put it in:

```text
Components/<Feature>
```

or an appropriate reusable `Components/UX` location when the component is genuinely presentation-wide.

### Database implementation

Put it under:

```text
Infrastructure/Persistence
```

### Browser storage implementation

Put it under:

```text
Infrastructure/LocalStorage
```

### Background service

Put it under:

```text
Workers
```

---

## Core Models and Ownership

Use the following ownership when adding or modifying these concepts:

| Concept | Owner |
| --- | --- |
| `Character` | Gameplay |
| `Game` | Gameplay |
| `DailyGame` | Gameplay |
| `GameState` | Gameplay |
| `GuessResult` | Gameplay |
| `GameSession` | Statistics |
| `PlayerStats` | Statistics |
| `RetentionStats` | Statistics |
| Application events | Communication |

Do not duplicate these models across features.

If a concept's ownership becomes unclear, inspect existing usage and architecture before creating another representation.

---

## Dependency Direction

The intended dependency direction is:

```text
Components
    ↓
Features
    ↓
Infrastructure
    ↓
Persistence / Browser Storage
```

Do not introduce reverse dependencies.

In particular, avoid:

```text
Infrastructure
    ↓
Components
```

```text
Components
    ↓
DbContext
```

```text
Components
    ↓
LocalStorage APIs
```

Features must not depend circularly on one another.

---

## Adding a New Feature

Only create a new feature when the functionality represents a distinct business capability.

When a new feature is justified:

```text
Features
└── NewFeature
    ├── Models
    ├── Services
    ├── Builders
    ├── Helpers
    └── ServiceCollectionExtensions.cs
```

Add only the directories that are required.

If UI is required:

```text
Components
└── NewFeature
```

If persistence is required:

```text
Infrastructure
└── Persistence
```

Do not move unrelated existing code merely to make the new feature conform to an idealized structure.

---

## Do Not

Do not:

- create global `Models`, `Services`, or `Repositories` folders for feature-specific code
- place business logic in Razor components
- access `DbContext` from UI
- access LocalStorage directly from components
- duplicate models across features
- bypass feature services
- create circular feature dependencies
- expose EF entities directly to the UI
- place statistics logic inside Gameplay
- place gameplay logic inside Statistics
- put reusable UI inside feature services
- create shared utility classes merely to avoid a few lines of duplication
- create folders or abstractions before they are needed
