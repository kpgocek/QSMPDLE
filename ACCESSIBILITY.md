# Accessibility

Last updated: November 2026 (v 1.4.0)

QSMPDLE aims to be playable by as many people as possible. This page describes what the game supports today and where it still falls short, so players know what to expect and contributors know what to fix.

This is an honest snapshot, not a conformance claim. QSMPDLE has **not** been formally audited against WCAG, and it has not yet been tested end to end with a screen reader. The notes below come from a review of the 1.4.0 source code (commit `6ab1f7d`).

## At a glance

| Area | Status in 1.4.0 |
| --- | --- |
| Keyboard play | ✅ Mostly supported. A full game can be played without a mouse. |
| Focus visibility | ⚠️ Partial. Links have their focus outline removed. |
| Screen readers | ❌ Limited. Clues and results are not announced in a usable way. |
| Colour and contrast | ⚠️ Partial. Clues use colour plus text, but several colours have low contrast. |
| Motion | ✅ Supported. Animations respect the system "reduce motion" setting. |
| Zoom and small screens | ✅ Supported. Layout switches to cards at narrow widths or high zoom. |
| Accounts, timers, CAPTCHAs | ✅ None. There are no time limits or sign-in steps. |

## What works

### Keyboard

- The guess box is a normal text field. Type part of a name, use **Arrow Up** and **Arrow Down** to move through the suggestions, and press **Enter** to submit the highlighted one.
- Suggestions are real buttons, so they can also be reached with **Tab** and activated with **Enter** or **Space**.
- Navigation (Daily, Archive, Practice, How to Play, Player Stats) uses standard links and buttons in a logical tab order.
- The Player Stats window closes with **Escape**.
- Archive mode's previous and next day buttons are keyboard reachable and are hidden from both the keyboard and assistive technology when there is no previous or next day.
- The archive calendar is MudBlazor's date picker. Its days are buttons, so they can be reached with **Tab**.

### Page structure

- The page declares its language (`lang="en"`), and each game and info page sets its own page title (for example "Daily Game - QSMPDLE" or "Archive Day #12 - Nov 3, 2026 | QSMPDLE").
- Main content sits in a `<main>` landmark, with a `<header>`, `<nav>` and `<footer>` around it.
- On wide screens, guesses are shown in a real HTML table with column headers (Character, Pronouns, Species, Languages, Affiliations, Joined).
- Icon-only controls that have labels: the mobile menu button ("Toggle Menu") and the archive arrows ("Previous day", "Next day").
- Links that open other sites (X, Ko-fi, Discord) open in a new tab with `rel="noopener noreferrer"`.

### Colour is not the only signal

Every clue chip shows its value as text (for example "Day 12" or "The Regime"), and comparison clues add an arrow showing whether the answer is higher, lower, earlier or later. When the game ends, the answer's name is written out under the portrait, and the result window says "Correct!" or "Welp!" in words.

### Motion

All CSS animations and transitions (row reveal, card reveal, dialog entrance, and the portrait frame's pulse) are cut to effectively zero when the operating system or browser has "reduce motion" turned on (`prefers-reduced-motion: reduce`). Nothing flashes.

### Zoom, text size and mobile

- Sizes are set in `rem`, so the layout follows the browser's text size setting.
- Below a width of 68rem (about 1088px, or when zooming in on a normal screen), the guess table is replaced by stacked cards and the navigation collapses into a menu, so the page does not need horizontal scrolling.

### No pressure

There are no timers, no accounts, no CAPTCHAs and no time-limited prompts. Progress is saved in the browser, so a game can be left and picked up later.

## Known gaps

These are known problems in 1.4.0. They are listed roughly in order of impact.

### Screen readers

1. **Clue results are not announced.** Whether a clue is correct, partial or wrong is shown only by chip colour. The arrows for higher, lower, earlier and later are icons with no text alternative. A screen reader user hears the value (for example "Day 12") but not whether it matched.
2. **New guesses are not announced.** There is no live region, so nothing is read out after a guess is made or when the game is won or lost.
3. **The guess box has no label.** It only has placeholder text ("Type in character's name"), and it is not marked up as a combobox, so the suggestion list and the highlighted suggestion are not exposed to assistive technology.
4. **Empty placeholder rows are read out.** The six-row table and the mobile cards include empty placeholder rows and chips that are not hidden from screen readers.
5. **The win and loss windows are not real dialogs.** They are drawn as overlays without `role="dialog"`. Focus does not move into them, is not kept inside them, and they do not close with Escape. Keyboard users can tab to the page behind them.
6. **Some images and buttons lack text.** The portrait's alt text is always "Character", even after the answer is revealed. Character avatars in the guess list have no alt text (the name is written next to them, so they should be marked decorative). The Player Stats close button and the Share menu button have no accessible name.
7. **Charts have no text alternative.** The guess distribution and activity charts on the Stats page and in Player Stats are drawn as graphics without a table or summary of their values.
8. **The mobile menu button does not report its state.** It does not use `aria-expanded`, so screen readers cannot tell whether the menu is open.

### Focus visibility

- `app.css` removes the focus outline from all links (`a:focus { outline: none; }`) and from headings. Navigation and footer links have hover and "current page" styles but no keyboard focus style, so keyboard users cannot see where they are.
- The "Player Stats" button relies on the browser's default focus ring, which may be hard to see against the dark background.

### Colour and contrast

The game uses a single dark theme. There is no light theme and no high-contrast or colour-blind mode.

Contrast ratios worked out from the theme colours (WCAG asks for at least 4.5:1 for normal text and 3:1 for large text and interface parts):

| Element | Colours | Ratio |
| --- | --- | --- |
| Body text on the background | `#FAFBFC` on `#010a1e` | about 19:1 ✅ |
| Secondary text on panels | `#CACBCC` on `#0A2C5E` | 8.4:1 ✅ |
| Inactive navigation links and separators | 30% white on `#010a1e` | 2.5:1 ❌ |
| White text on a green "correct" chip | `#2FBB36` | 2.5:1 ❌ |
| White text on an orange "partial" chip | `#FFA056` | 2.0:1 ❌ |
| White text on a blue "higher/lower" chip | `#16BAFF` | 2.2:1 ❌ |
| White text on a red "wrong" chip | `#CC284C` | 5.3:1 ✅ |

The chip ratios assume MudBlazor's default white chip text, since the theme does not override it. They have not been measured in a browser.

Other colour notes:

- Green (`#2FBB36`) and red (`#CC284C`) are hard to tell apart for people with red-green colour blindness (the two are only about 2:1 apart in brightness). Correct and wrong chips carry no icon or label that tells them apart.
- The How to Play page calls the partial colour "Yellow", but it is shown as orange (`#FFA056`).
- In the archive calendar, won, lost and in-progress days are told apart by colour only.
- Body text uses a light font weight (300), which can be harder to read for people with low vision.

### Gameplay

- The core of the game is a blurred portrait that becomes clearer with each guess. This clue is visual only and has no equivalent for blind players. The attribute clues can still be used to work out the answer.
- Choosing a suggestion with the mouse, a tap or Enter submits the guess straight away. There is no confirm step and no undo, so a mis-tap or mis-key uses up one of the six guesses.
- Pressing Escape does not close the suggestion list.
- Suggestions only match character names, not nicknames or aliases.
- The "Copy" option in the share menu uses the device's share sheet rather than the clipboard. On browsers without a share sheet (most desktop browsers), it does nothing and gives no feedback.

### Connection

QSMPDLE runs on Blazor Server, so the page needs a live connection. If it drops, a "Rejoining the server..." message appears and the game cannot be played until it reconnects or the page is reloaded.

## Feedback

If something gets in your way, please let us know. Open an issue on [GitHub](https://github.com/kpgocek/QSMPDLE/issues) or mention it on [Discord](https://discord.gg/PnpeujMZg). It helps to include your device, browser, and any assistive technology you use (for example a screen reader, magnifier or voice control).

## For contributors

When changing the interface, please check:

- The game can still be played from start to finish with the keyboard alone.
- Focus is visible on every interactive element.
- New colours meet the contrast ratios above, and colour is never the only way information is shown.
- New animations are covered by the `prefers-reduced-motion` rule in `web/wwwroot/css/animations.css`.
- Icon-only buttons have an `aria-label`, and images have useful `alt` text or `alt=""` when decorative.

Please update this page when an accessibility gap is fixed or a new one is found.
