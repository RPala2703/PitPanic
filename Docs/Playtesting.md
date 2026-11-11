# Pit Panic demo playtest

Extract the complete Windows demo folder and launch `PitPanic.exe`. Let a new
tester play without coaching; ask them to describe what they think they should
do next. Record where they hesitate, not just whether they eventually succeed.

| Check | Expected outcome |
| --- | --- |
| Title/settings | New Game, Continue, volume sliders, fullscreen and Quit work. No active casino is visible behind the title. |
| New player | New Game explains its reset, then guides one objective at a time; destinations and available actions match. Skip Training works. |
| Controls | WASD follows the screen axes; diagonal speed is normal. E opens tables or delivers a carried tray first. Q opens the same inspection. Escape exits every interaction. |
| Blackjack | FLICK! can succeed, fail, cancel and retry. Dealing resumes; chips and results match the hands. No duplicate payouts on retry. |
| Roulette | SPIN!, SWEEP! and PAY! each finish or cancel safely. Winner chips are protected; each payout settles once. |
| Cash Desk | PACK! is readable, has enough time and refunds a cancelled order. Carry the correct tray to its named table; cash-outs and float delivery remain usable afterward. |
| Staff/break room | Assign cover, send the relieved dealer to rest, then reassign. At zero energy service stops, customers lose patience and warnings clear after cover/rest. |
| Pause/restart | P freezes audio, timers, movement and tasks, including a minigame. Resume preserves it; Restart starts fresh; Main Menu preserves unlocks/perks. |
| Shift results | Finish a natural shift. Hands/tasks/satisfaction explain pass or failure; profit excludes float transfers. Failure allows Retry without rewards/unlocks. |
| Upgrades/save | Clear a shift, buy one perk or skip, then start the next. Relaunch and Continue: perk/stamps persist. A perk cannot be purchased twice. New Game resets progress only after starting. |
| Demo ending | Third shift is harder but achievable; clearing it shows demo complete and allows replay. No invisible fourth-shift requirement. |
| Performance/readability | Try 1280×720, 1600×900 and 1920×1080, windowed/fullscreen. No clipped controls, blocking labels, stationary agents or sustained stutter at the customer cap. Record average/minimum FPS and hardware. |

For each bug record build/commit, shift, tutorial stage, exact inputs, expected
versus actual behaviour, screenshot/video and `Player.log`. Windows logs are at
`%USERPROFILE%\AppData\LocalLow\DefaultCompany\PitPanic\Player.log`.
Report save problems before starting another New Game. Stop distribution for
crashes, unfinishable tasks, lost progression or incorrect money accounting.

## Automated coverage and limits

The generator validates compilation, rules/payout cases, duplicate accounting,
float reservations, task eligibility, sprite imports, references and NavMesh paths.
The Play Mode driver exercises guided training, actual task completion/cancellation,
pause/audio isolation, exhaustion, results, perk purchase/application, Continue,
failure/retry/restart and cashier expiry/capacity cleanup. It uses synthetic input,
some direct UI calls and controlled fixtures; it is not a human usability test.

The macOS development capture can exercise an accelerated full unattended shift
and retry. A successful Windows build does **not** prove Windows runtime behaviour.
Human nine-minute playthroughs, Windows execution, minimum-spec performance,
high-DPI/accessibility testing and subjective difficulty/audio quality must be
signed off by external testers before a Steam release. No Steamworks integration,
cloud saves or achievements are included in this demo milestone.
