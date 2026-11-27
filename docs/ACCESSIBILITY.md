# Accessibility

Last updated: November 2026 (v 1.4.0)

QSMPDLE aims to be playable by as many people as possible. This page describes what the game supports today and where it still falls short, so players know what to expect and contributors know what to fix.

## At a glance

| Area | Status in 1.4.0 |
| --- | --- |
| Keyboard play | ✅ Mostly supported. A full game can be played without a mouse. |
| Focus visibility | ⚠️ Partial. Links have their focus outline removed. |
| Screen readers | ❌ Limited. Clues and results are not announced in a usable way. |
| Colour and contrast | ⚠️ Partial. Clues use colour plus text, but several colours have low contrast. |
| Motion | ✅ Supported. Animations follow the system "reduce motion" setting, or Profile → Settings → Motion overrides it. |
| Zoom and small screens | ✅ Supported. Layout switches to cards at narrow widths or high zoom. |
| Accounts, timers, CAPTCHAs | ✅ None. There are no time limits or sign-in steps. |

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

| Element | Colours | Ratio |
| --- | --- | --- |
| Body text on the background | `#FAFBFC` on `#010a1e` | about 19:1 ✅ |
| Secondary text on panels | `#CACBCC` on `#0A2C5E` | 8.4:1 ✅ |
| Inactive navigation links and separators | 30% white on `#010a1e` | 2.5:1 ❌ |
| White text on a green "correct" chip | `#2FBB36` | 2.5:1 ❌ |
| White text on an orange "partial" chip | `#FFA056` | 2.0:1 ❌ |
| White text on a blue "higher/lower" chip | `#16BAFF` | 2.2:1 ❌ |
| White text on a red "wrong" chip | `#CC284C` | 5.3:1 ✅ |

WCAG asks for at least 4.5:1 for normal text and 3:1 for large text and interface parts.

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
