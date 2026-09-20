# Production memory investigation â€” 20 September 2026

## Findings and evidence

| Finding | Evidence | Change / interpretation |
| --- | --- | --- |
| Confirmed process-lifetime component retention | The original singleton GameEventBus kept delegates capturing ArchiveNavigator and ArchiveDay in lists, with no unsubscribe API. Components subscribed on creation and never removed callbacks. | Bus is circuit-scoped; subscriptions return disposable tokens; components and the bus release callbacks on disposal. Publishing snapshots handles removal during dispatch. |
| Confirmed cross-player notification and post-persistence failure path | Every circuit published to the same singleton lists. GameStateManager saved browser state and recorded a database guess before awaiting GuessMadeEvent subscribers. An exception could escape before completion persistence and the UI callback returned. | Subscriber exceptions are logged independently and do not abort other notifications or guess completion. A regression test with throwing guess/finish subscribers verifies the winning guess, database completion, and local completion marker. This does not prove the stream incident used that path. |
| Confirmed cache isolation defect, not established as incident cause | ArchiveStatusCache keyed results by date range, but its factory queried a specific player's sessions. It was singleton. Current calendar calls use local-storage fallback and bypass that cache. | Cache is now scoped, disposable, bounded to 4,096 cached day statuses, with ten-minute expiry and overlapping-range invalidation on guess/finish. Copies isolate callers. Fetches invalidated while in flight are not stored. Expired entries are removed on access; idle caches remain hard-bounded and release on scope disposal. |
| No circuit-long gameplay change tracker found | DatabaseGameStatsStore and CachedDatabaseCharacterStore create and await-dispose factory contexts per operation. Read queries use AsNoTracking. SitemapService directly injects a context, but is resolved by the HTTP sitemap endpoint, not game components. | No speculative EF lifetime rewrite. StatisticsRefreshWorker now disposes its one context before its five-minute delay. That context was not multiplied by players. |
| No confirmed application-owned JS-reference or timer leak | No application DotNetObjectReference / IJSObjectReference allocations or per-player timers were found. Sharing/scrolling use direct JS calls. Reconnect listeners belong to the document; the statistics refresh loop belongs to the process. | No invented cleanup for references the application does not own. Third-party MudBlazor/chart/library retention would require heap evidence. |
| Shared cache growth is not established as the production cause | Character and lookup lists use two fixed keys. Daily-character entries expire after one day; sitemap has one expiring key. No shared player-history dictionary was found. Full player-history queries can allocate transiently. | Shared cache entry counts are sampled. The shared framework cache has no new global size limit. No claim that allocation pressure or retained framework circuits is eliminated. |

Both repository HEAD and the initial working tree use Interactive Server. HEAD made Routes interactive globally. The working tree already attempted page-level interactivity and 30-second / 100-circuit disconnected retention. The deployed commit is unknown.

## Guess and UI behavior

The selector sets its bound character and invokes Gameplay.Guess. The manager claims/reconciles a canonical session, compares the character, updates in-memory state, saves browser state, records start/guess/completion as applicable, and publishes notifications. The component then resolves the answer name, notifies its parent, and renders the table/cards, portrait, finished-state selector visibility, and popup.

A null guess result can mean the manager replaced its state with an existing canonical session. Gameplay now updates from that state instead of exiting before its parent/answer update. Persistence failures remain exceptions: the new log includes stage, elapsed time, and successful local-save/guess/completion stages. These flags mean the awaited operation returned successfully; a false flag does not prove that a multi-step operation made no partial write. There is no new transaction or blind retry.

Failure logs do not add player IDs, game IDs, or guess contents. Notification failures retain the exception and event type; circuit exceptions remain enabled in server logs.

## Render modes and reconnect policy

Daily, Archive Day, Practice, and Stats explicitly use Interactive Server. Navigation and the shared MudBlazor providers are interactive islands in the static layout; HeadOutlet matches. Providers are removed from Gameplay to avoid duplication. Archive titles use PageTitle.

The navigation/profile island still needs a circuit on otherwise static pages. Page-level rendering should not be interpreted as eliminating those circuits.

Disconnected retention defaults are 30 seconds and 100 circuits, configurable via:
- Circuits__DisconnectedRetentionSeconds
- Circuits__MaxDisconnected

These settings do not cap connected players. Persisted browser/database game state supports recovery after expiration; unsent input is not guaranteed after a reload. A rejected reconnect followed by a failed resume now reloads instead of leaving a dead circuit. Detailed client errors are Development-only.

## Diagnostics and production logging

One structured MemoryDiagnosticsWorker sample is emitted at startup and every 60 seconds:
- PID, process start UTC, uptime.
- Process working set/private bytes.
- GC.GetTotalMemory(false), cumulative allocated bytes, last-GC heap size and fragmentation, generation collection counts.
- Open, connected, and disconnected circuit counts.
- Active subscriptions, scoped archive cache entries, and shared MemoryCache entries.

Counters retain numbers only. Circuit close/disposal and cache/bus disposal decrement counts. SharedCacheEntries covers the shared IMemoryCache, including library consumers; ArchiveCacheEntries includes expired entries until their next access/disposal. Last-GC values are snapshots, not current live-object counts. Samples are approximate across concurrently changing counters.

EF command success logging is Warning-filtered, routine Stats start/success/end messages are Debug, and per-day calendar Console.WriteLine output is removed. Errors remain visible. Railway environment logging overrides can supersede repository settings.

## Verification

- Baseline: 63 tests passed and Release web build passed, with an existing MudBlazor aria attribute warning.
- Final: 71 tests passed in both Debug and Release. Release build and local publish succeeded. The aria attribute warning was corrected.
- Added regressions cover DI event isolation, disposal/idempotence, disposal during publication, subscriber failure isolation through winning-guess persistence, canonical-session reconciliation, circuit counter transitions, cache copies/isolation/expiry/capacity/in-flight invalidation, and game-event cache invalidation.
- Browser testing used a separate local PostgreSQL database containing only synthetic characters/puzzles. Verified incorrect guesses and wins without refresh, answer/popup rendering, profile statistics/dialog, six-guess archive loss, archive navigation and calendar toggle, Practice reset, Stats filters, and corrected archive title.
- Mobile verification at 390 × 844 confirmed guess cards update and the navigation menu opens. A three-second interruption preserved an unsent selection, which could then be submitted.
- Published Release build was exercised with Production settings. A local TCP proxy interrupted only its test connection. Recovery after a 40-second interruption preserved earlier guesses and accepted another guess without manual refresh. This verifies recovery beyond the configured retention window; it does not distinguish framework resume from circuit recreation.
- Diagnostics were observed running. A local sample after leaving archive gameplay returned subscriptions to zero. Local single-browser Windows memory readings are not a Railway load comparison.
- Test server/proxy processes were stopped and the synthetic database and temporary publish/proxy artifacts were removed after verification.
- An existing unused ApexCharts script produces a browser module-loading error; the game/profile/current MudBlazor chart flows above still worked. It is not attributed as the refresh or memory cause.

## Changed files

Lifetime/persistence:
- web/Features/Communication/ServiceCollectionExtensions.cs
- web/Features/Communication/GameEvents/IGameEventBus.cs
- web/Features/Communication/GameEvents/GameEventBus.cs
- web/Features/Gameplay/Services/GameStateManager.cs
- web/Services/ArchiveStatusCache.cs
- web/Services/ArchiveStatusService.cs
- web/Workers/StatisticsRefreshWorker.cs

Rendering and subscriptions:
- web/Components/App.razor
- web/Components/Game/Gameplay.razor
- web/Components/Layout/MainLayout.razor
- web/Components/Layout/NavMenu.razor
- web/Components/Layout/InteractiveProviders.razor (new)
- web/Components/Layout/ReconnectModal.razor.js
- web/Components/Pages/Archive/ArchiveDay.razor
- web/Components/Pages/Game/Practice.razor
- web/Components/Pages/Static/Stats.razor
- web/Components/UX/Navigation/ArchiveNavigator.razor

Diagnostics/configuration/tests:
- web/Program.cs
- web/appsettings.json
- web/Diagnostics/RuntimeCounters.cs (new)
- web/Diagnostics/CountingCircuitHandler.cs (new)
- web/Workers/MemoryDiagnosticsWorker.cs (new)
- tests/QSMPDLE.Web.Tests/Services/RuntimeLifetimeTests.cs (new)
- tests/QSMPDLE.Web.Tests/GameplayFlow/GameStateManagerGuessTests.cs
- docs/production-memory-investigation.md (this report)

Pre-existing edits were preserved/completed in App, Gameplay, MainLayout, ArchiveDay, Practice, and Program. Home.razor's existing InteractiveServer addition and the empty untracked web/_Imports.razor were left untouched. No production deployment, schema migration change, WebAssembly migration, or forced GC was introduced.

## What production must still establish

1. Match the stream's timestamps and deployed commit to Railway Events, deployments, restarts, OOM exits, and replica changes. The sharp memory drop is unexplained.
2. Compare equivalent player/circuit counts, refresh rates, and traffic windows using the new process-start identity and memory samples. Observe after players disconnect and natural collections occur. Working set alone is not proof of a managed leak.
3. If managed memory grows despite stable circuit/subscription/cache counts, capture managed heap evidence to locate roots; distinguish native memory, GC reserved/committed memory, allocation pressure, and live managed objects.
4. For refresh reports, align server guess-stage/circuit errors with browser WebSocket close/reconnect and JS/network failures. Inspect persistence safely to distinguish saved guesses from failed writes without adding identifiers or guess contents to routine logs.
5. Check real mobile network/reconnect experience before reducing retention further. No production-scale load result or measured MB saving is claimed.
