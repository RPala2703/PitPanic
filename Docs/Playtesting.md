# Pit Panic demo playtest

Extract the complete Windows demo folder and launch `PitPanic.exe`. Let a new
tester play without coaching; ask them to describe what they think they should
do next. Record where they hesitate, not just whether they eventually succeed.

| Check | Expected outcome |
| --- | --- |
| Title/settings | New Game, Continue, volume sliders, fullscreen and Quit work. No active casino is visible behind the title. |
| New player | Tiger's large portrait lessons pause all simulation/audio; Let Me Try resumes real practice. Complete all 13 lessons without random-task waits. HELP revisits explanations without resetting progress. Skip Training works. |
| Controls | WASD and arrow keys follow the screen axes; diagonal speed is normal. E opens tables or delivers a carried tray first. Q opens the same inspection. Escape exits every interaction. |
| Blackjack | SHUFFLE! uses clicks on the shoe and deals the correct opening cards. It can succeed, fail, cancel and retry. Check finite float balances and exact payouts; no duplicate payouts on retry. |
| Roulette | A staffed table requests COVER!; an unstaffed table can be taken over. SPIN!, SWEEP! and PAY! each finish or cancel safely. Test straight, split, street, corner, six-line and outside bets including zero, their stated odds, finite chips and one-time settlement. |
| Cash Desk | Both games accept clicks, not dragging. £2.50/£5/£25/£100 choices add visible chips; Undo works before finishing. Verify finite register counts and standard float value (£200). Cancellation/timeout refunds a float reservation; carry/deliver only to the named table. |
| Urgency/markers | Real station meters match HUD seconds and actual patience/attempt time, with green/yellow/red transitions. No fake timer on float/staff jobs. Approach each ! for specific instructions. Expired customers lose satisfaction; their assistance marker clears when no recipients remain. |
| Manual dealing | Exhaust every dealer. E → Take Over runs a real Blackjack hand with Space and a real Roulette SPIN/SWEEP/PAY round. Verify exact payouts and no second settlement on retry. Escape releases control; returning cover resumes the current round. |
| Carried tray | Idle and walk facing all four directions; one tray stays at the penguin's hands with correctly layered chips. No detached tray or backward-facing hands. |
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
The expanded driver acknowledges paused lessons, clicks both chip racks, undoes
selections, checks real deadlines, completes both manual table types, sends
outgoing exhausted staff to recover, and verifies automatic return to staffing.
Any Unity Console error or exception fails this regression.

The macOS development capture can exercise an accelerated full unattended shift
and retry. A successful Windows build does **not** prove Windows runtime behaviour.
Human nine-minute playthroughs, Windows execution, minimum-spec performance,
high-DPI/accessibility testing and subjective difficulty/audio quality must be
signed off by external testers before a Steam release. No Steamworks integration,
cloud saves or achievements are included in this demo milestone.
