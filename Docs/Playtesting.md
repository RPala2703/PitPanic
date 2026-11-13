# Pit Panic demo playtest

Extract the complete Windows demo folder and launch `PitPanic.exe`. Let a new
tester play without coaching; ask them to describe what they think they should
do next. Record where they hesitate, not just whether they eventually succeed.

| Check | Expected outcome |
| --- | --- |
| Title/settings | New Game, Continue, volume sliders, fullscreen and Quit work. No active casino is visible behind the title. |
| New player | Tiger's short portrait lessons pause all simulation/audio; Okay resumes real practice. Only the current lesson's stations show ! markers, timers and task-HUD entries. Complete all 13 lessons without random-task waits. HELP revisits explanations without resetting progress. SKIP TRAINING restores normal task visibility. |
| Controls | WASD and arrow keys follow the screen axes; diagonal speed is normal. E opens tables or delivers a carried tray first. Q opens the same inspection. Escape exits every interaction. |
| Blackjack | All-bust hands end without dealer draws. Stakes return automatically on non-losses; pay only 1× winnings on a win, 1.5× on Blackjack, nothing on a push. Check the finite float and accounting separately. SHUFFLE! remains a retryable two-stack sequence. |
| Roulette COVER! | A working dealer reveals the actual round result before requesting COVER! Click winning bets or numbered chips, not losers. Test every bet type, zero, overlapping/identical customer bets, NO WINNERS and BET GUIDE. Escape/timeout must preserve the same result and unpaid bets. The button says only COVER! |
| Roulette settlement | COVER! automatically clears and pays winnings only: £5 red wins £5, £5 straight-up wins £175. Winning stakes return separately; only losing stakes enter the float. Force a shortage after a partial payout, deliver float and verify only unpaid bets resume. No duplicate settlement or invented chips. Split/street/corner/six-line remain 17/11/8/5:1. |
| Roulette takeover | Exhaust the dealer before and after the wheel spins. COVER! becomes unavailable. TAKE OVER uses SPIN!/SWEEP!/PAY!, retaining an existing result and payouts on cancel/retry. Do not mix it with COVER! |
| Cash Desk | Both games accept clicks, not dragging. £2.50/£5/£25/£100 choices add visible blue/red/black/pink chips; Undo works before finishing. Deplete different denominations and verify different exact refill quantities (including red-only orders), not a repeated £200 bundle. Cancellation/timeout refunds the reservation and preserves that order; carry/deliver only to the named table. |
| Urgency/markers | Real station meters match the active task's patience/attempt time, with green/yellow/red transitions. Complete a task while customers remain seated: its timer must disappear, including when only an untimed refill remains. No unrelated or stale timers. Approach each ! for a player speech bubble of at most three words; leaving range or opening a panel hides it. |
| Manual dealing | Exhaust every dealer. E → Take Over runs a real Blackjack hand with Space and a real Roulette SPIN/SWEEP/PAY round. Verify exact payouts and no second settlement on retry. Escape releases control; returning cover resumes the current round. |
| Carried tray | Idle and walk facing all four directions; one tray stays at the penguin's hands with correctly layered chips. No detached tray or backward-facing hands. |
| Staff/break room | Assign cover, send the relieved dealer to rest, then reassign. Exhausted dealers and resting staff show a Zzz speech bubble; it clears when they return to service. Player and dealer bubbles sit above the visible sprite's head, not inside the body, while turning or moving. At zero energy service stops, customers lose patience and warnings clear after cover/rest. |
| Blackjack payouts | During a payout, Space must not skip or deal. Confirm a wrong amount: the same hand stays selected with "Wrong amount!" and must be recounted. Tables show no printed cards before dealing. |
| Failed tasks | A wrong SHUFFLE stack plays the failure buzz, closes the minigame and reopens with the same, still-ticking countdown. Missed tasks and walk-outs play the same buzz. |
| Floats/cash-outs | Refills mainly ask for reds and blacks; blues appear only when the blue tray is empty. Cash-outs vary between reds, black/red mixes and pinks/blacks (VIPs favour big chips). |
| Arrivals/VIPs | Opening customers walk in one at a time without overlapping. VIPs show a gold, pulsing HURRY! patience bar; regulars show a bar only below half patience. Swapped dealers stand exactly at the table. |
| Pause/restart | P freezes audio, timers, movement and tasks, including a minigame. Resume preserves it; Restart starts fresh; Main Menu preserves unlocks/perks. |
| Shift results | Finish a timed shift. Only satisfaction and tasks determine results: 4 completed actions, at least 60% happy, at most 5 misses/failed attempts combined. No hand target or profit statistic anywhere on results or campaign briefings. Failure allows Retry without rewards/unlocks. |
| Campaign progression | Clear Level 1 to unlock High Rollers, then Level 2 to unlock Shady Business. Best stars persist per level; replay is available only for unlocked levels. New Game resets campaign stars/unlocks when a level is selected, while legacy saves and settings remain intact. |
| High Rollers | Confirm VIP guests have full formalwear sprites, top hats and a soft golden aura in every direction/action, not a floating crown. They make larger bets and have shorter patience. Verify a VIP is introduced and the table remains serviceable. |
| Shady Business CCTV | Resolve scheduled alerts at the Pit Desk. Pause/replay the evidence, verify its displayed original bet and winning number match the real Roulette round, and confirm the chip movement does not change the wager or payout. Test both a correct decision and a false accusation. |
| Level layouts | Start each unlocked level and verify the Blackjack tables, Roulette table, seats, dealers, interaction zones and customer routes remain accessible in the themed layout. |
| Seating | Watch arrivals at all ten seats in all three layouts. Customers settle onto the visible cushion, including diagonal Roulette seats; chairs stay beside their own table. No floating, compressed bodies, stale table obstacles or persistent offsets after leaving. |
| Animation | Watch more than one full idle/walk/rest cycle. Loops keep playing; deal/collect/pay/shuffle/reaction actions anticipate, make contact and recover through 60 baked frames per direction. Front/back walking alternates steps without spreading feet sideways; waist/legs remain joined. Blackjack customers keep empty hands low and cards on the felt, including VIPs. No frozen last frames or transform-squashed sitting. |
| Popup spacing | Okay and SKIP TRAINING are separate, aligned and inset. Blackjack Deal, payout chips/Remove/Confirm, Roulette Spin, staff controls and CCTV decisions stay inside their panels without overlapping. Keyboard Space still works. Also try 1024×768 and 2560×1080; popups must fit without clipping. |
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
Presentation checks also cover 60-frame playback, action clamping versus looping,
adjacent-frame continuity and loop seams in every direction/species. Play Mode
checks chair anchors and reachable approaches across all layouts, actual seated
customer alignment, action-frame advancement, and popup button bounds/overlaps.

The macOS development capture can exercise an accelerated full unattended shift
and retry. Add `--pit-cover-check` alongside `--pit-capture-dir <folder>` to capture
the actual COVER! board and BET GUIDE, select that round's winning bets, and check
settlement in the standalone app. This check waits for a real dealer spin; it
does not invent a result or replace the customer bets.
Use `--pit-seating-check` with the capture directory to route ten real customers
to all ten chairs in each campaign layout, verify their visual anchors, and save
overview/close-up screenshots. This controlled presentation fixture holds table
settlement while navigation and sitting run; it is not a full service playthrough.
A successful Windows build does **not** prove Windows runtime behaviour.
Human nine-minute playthroughs, Windows execution, minimum-spec performance,
high-DPI/accessibility testing and subjective difficulty/audio quality must be
signed off by external testers before a Steam release. No Steamworks integration,
cloud saves or achievements are included in this demo milestone.

Tips: `CasinoGameplayValidation` checks that tips grow with satisfaction, VIPs tip more, customers with no money cannot tip, no service event tips twice, and the star bonus works. The Play Mode run checks bankroll deduction, an unchanged float, and safe delivery when the customer is destroyed mid-tip. Animation feel and audio sync still need a human playtest.
