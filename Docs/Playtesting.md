# Pit Panic demo playtest

Extract the complete Windows demo folder and launch `PitPanic.exe`. Let a new
tester play without coaching; ask them to describe what they think they should
do next. Record where they hesitate, not just whether they eventually succeed.

| Check | Expected outcome |
| --- | --- |
| Title/settings | New Game, Continue, volume sliders, fullscreen and Quit work. No active casino is visible behind the title. |
| New player | Tiger's short portrait lessons pause all simulation/audio; LET ME TRY! resumes real practice. Only the current lesson's stations show ! markers, timers and task-HUD entries. Complete all 13 lessons without random-task waits. HELP revisits explanations without resetting progress. SKIP TRAINING restores normal task visibility. |
| Controls | WASD and arrow keys follow the screen axes; diagonal speed is normal. E opens tables or delivers a carried tray first. Q opens the same inspection. Escape exits every interaction. |
| Blackjack | All-bust hands end without dealer draws. Stakes return automatically on non-losses; pay only 1× winnings on a win, 1.5× on Blackjack, nothing on a push. Check the finite float and accounting separately. SHUFFLE! remains a retryable two-stack sequence. |
| Roulette COVER! | A working dealer reveals the actual round result before requesting COVER! Click winning bets or numbered chips, not losers. Test every bet type, zero, overlapping/identical customer bets, NO WINNERS and BET GUIDE. Escape/timeout must preserve the same result and unpaid bets. The button says only COVER! |
| Roulette settlement | COVER! automatically clears and pays with actual inventory. Force a shortage after a partial payout, deliver float and verify only unpaid bets resume. No duplicate settlement or invented chips. Straight-up profit remains 35:1; split/street/corner/six-line are 17/11/8/5:1. |
| Roulette takeover | Exhaust the dealer before and after the wheel spins. COVER! becomes unavailable. TAKE OVER uses SPIN!/SWEEP!/PAY!, retaining an existing result and payouts on cancel/retry. Do not mix it with COVER! |
| Cash Desk | Both games accept clicks, not dragging. £2.50/£5/£25/£100 choices add visible chips; Undo works before finishing. Verify finite register counts and standard float value (£200). Cancellation/timeout refunds a float reservation; carry/deliver only to the named table. |
| Urgency/markers | Real station meters match HUD seconds and actual patience/attempt time, with green/yellow/red transitions. No fake timer on float/staff jobs. Approach each ! for specific instructions. Expired customers lose satisfaction; their assistance marker clears when no recipients remain. |
| Manual dealing | Exhaust every dealer. E → Take Over runs a real Blackjack hand with Space and a real Roulette SPIN/SWEEP/PAY round. Verify exact payouts and no second settlement on retry. Escape releases control; returning cover resumes the current round. |
| Carried tray | Idle and walk facing all four directions; one tray stays at the penguin's hands with correctly layered chips. No detached tray or backward-facing hands. |
| Staff/break room | Assign cover, send the relieved dealer to rest, then reassign. At zero energy service stops, customers lose patience and warnings clear after cover/rest. |
| Pause/restart | P freezes audio, timers, movement and tasks, including a minigame. Resume preserves it; Restart starts fresh; Main Menu preserves unlocks/perks. |
| Shift results | Finish a natural shift. Hands/tasks/satisfaction explain pass or failure; profit excludes float transfers. Failure allows Retry without rewards/unlocks. |
| Campaign progression | Clear Level 1 to unlock High Rollers, then Level 2 to unlock Shady Business. Best stars persist per level; replay is available only for unlocked levels. New Game resets campaign stars/unlocks when a level is selected, while legacy saves and settings remain intact. |
| High Rollers | Confirm VIP guests wear the gold crown/lapel, make larger bets, and have shorter patience. Verify a VIP is introduced during the level and the table remains serviceable. |
| Shady Business CCTV | Resolve scheduled alerts at the Pit Desk. Pause/replay the evidence, verify its displayed original bet and winning number match the real Roulette round, and confirm the chip movement does not change the wager or payout. Test both a correct decision and a false accusation. |
| Level layouts | Start each unlocked level and verify the Blackjack tables, Roulette table, seats, dealers, interaction zones and customer routes remain accessible in the themed layout. |
| Demo ending | Third level is harder but achievable; clearing it shows demo complete and allows replay. No invisible fourth-level requirement. |
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
failure/retry/restart, campaign unlock/star persistence, and cashier expiry/capacity cleanup. It uses synthetic input,
some direct UI calls and controlled fixtures; it is not a human usability test.
The expanded driver acknowledges paused lessons, clicks both chip racks, undoes
selections, checks real deadlines, completes both manual table types, sends
outgoing exhausted staff to recover, and verifies automatic return to staffing.
Any Unity Console error or exception fails this regression.
Rule fixtures exercise every COVER! bet type against all 37 results, identical
winning bets, no winners, real refill delivery after partial payment, duplicate
settlement guards, all-bust Blackjack, and profit-only Blackjack payments.

The macOS development capture can exercise an accelerated full unattended shift
and retry. Add `--pit-cover-check` alongside `--pit-capture-dir <folder>` to capture
the actual COVER! board and BET GUIDE, select that round's winning bets, and check
settlement in the standalone app. This check waits for a real dealer spin; it
does not invent a result or replace the customer bets.
A successful Windows build does **not** prove Windows runtime behaviour.
Human nine-minute playthroughs, Windows execution, minimum-spec performance,
high-DPI/accessibility testing and subjective difficulty/audio quality must be
signed off by external testers before a Steam release. No Steamworks integration,
cloud saves or achievements are included in this demo milestone.
