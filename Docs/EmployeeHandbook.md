# Employee Handbook

An optional reference book that opens from the main menu (**HANDBOOK** button) and from level select (**HANDBOOK**, next to **BACK**). It is purely presentational and does not touch gameplay, minigames, betting, payouts, shift timing or movement.

## Editing content

All content lives in `Assets/Resources/Handbook/EmployeeHandbook.asset` (`CasinoHandbookContent`). Edit it in the Inspector; no code changes are needed to add, reorder or reword entries. Each entry has:

| Field | Meaning |
| --- | --- |
| `id` | Stable key used for save data. Do not rename after shipping, or players lose read/unlock state for that entry. |
| `category` | `Staff`, `Guests`, `Equipment`, `HouseRules` or `Archives` (tab). Entries keep asset order inside a tab. |
| `title` / `description` | Display text. Separate paragraphs in a description with a blank line. |
| `image` / `useCasinoChipArt` | An existing sprite or the existing chip art, shown in a 128 px thumbnail in the top-right corner. |
| `unlock` / `level` | `Always`, `TutorialComplete`, `LevelUnlocked` (level N available), or `LevelCleared` (stars earned on level N). |

## Unlocks and NEW markers

- Unlock rules are evaluated against the existing demo progression keys (`PitPanic.TutorialComplete`, `CasinoDemoProgression.UnlockedShift` and `BestStars`) by `CasinoHandbookProgress`.
- Earned unlocks are latched in `PitPanic.Handbook.Unlocked.<id>`, so **New Game** does not re-lock them. `StartNewGame` syncs before it clears progression.
- An entry counts as unread until it has been viewed. Read state is stored in `PitPanic.Handbook.Read.<id>`.
- Unread entries show NEW on the entry, its tab and the launcher buttons.
- Staff entries are available immediately. Most Guests and Equipment unlock after the training shift. VIP entries unlock at level 2, Footage Review at level 3, and Star Ratings when level 1 is cleared.
- The Archives tab is visible from the start. Lucky Seven Chip (Equipment) unlocks after clearing level 2; Trial 12 — Follow-Up (Archives) unlocks after clearing level 3.
- Locked entries show a silhouette and an unlock hint.

## Controls

Tabs, PREV / NEXT (both wrap), CLOSE, Escape or gamepad East to close. Keyboard and gamepad navigation are wired explicitly. While the handbook is open, the menu underneath is blocked.

## Validation

`EmployeeHandbookPlayModeValidation.Run` (visible Editor; set `PIT_PANIC_HANDBOOK_EVIDENCE=<dir>` for screenshots) runs on the real scene and checks the following:

- Opening, closing and navigation from both launchers.
- That the menu underneath is blocked while the handbook is open.
- That NEW markers clear.
- Persistence across scene reloads and New Game.
- Progression unlocks.

It isolates and restores the player's real PlayerPrefs. The pass marker is `EMPLOYEE HANDBOOK PLAY MODE PASSED`. Handbook keys are also included in the save isolation of the existing Play Mode validations.
