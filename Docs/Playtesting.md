# Pit Panic demo playtest

Extract the complete Windows demo folder and launch `PitPanic.exe`. Let a new
tester play without coaching; ask them to describe what they think they should
do next. Record where they hesitate, not just whether they eventually succeed.

| Check | Expected outcome |
| --- | --- |
| Title/settings | Original exterior fits 16:9, 4:3 and ultrawide without cropping the logo or controls. Penguin starts centred by the doors and idles without walking in from the side. NEW GAME and CONTINUE briefly open the supplied doors, then show the existing level selector. SETTINGS, volume sliders, fullscreen, keyboard navigation and QUIT work immediately. Idle remarks use the matching portraits, never pause or intercept clicks, and stop on selection. Returning to the title resets one Penguin and keeps one music source. |
| New player | Staff portrait conversations pause the simulation and other audio; their bleeps respect effects volume. Click, Enter, Space or E reveals a line, then advances it; Escape dismisses it without triggering the world. Check all 13 action-gated lessons, current-station markers and HELP. SKIP TRAINING restores normal task visibility. |
| Staff dialogue | Check names and all 30 supplied individual portraits across repeated speaker/expression changes: no stretching, wrapping or displaced image sections. Check typewriter text, voice bleeps and the continue indicator. Play all three level introductions; occasional event remarks must not block gameplay or repeat constantly. |
| Objective arrow | During training, open and close inspections, task windows, the Pit Desk, HELP and pause. The destination arrow must disappear while any popup is open and return at the current destination only when gameplay resumes. |
| Entrance audio | NEW GAME and CONTINUE stop title music on the click. A prominent, dramatic latch/creak/thud accompanies the doors opening, followed by footsteps and the entrance cue without music; level selection stays quiet. The door sound respects effects volume. Casino music starts as soon as the shift opens, including during the welcome dialogue. Back to title restores title music. P still pauses all gameplay audio. |
| Controls | WASD and arrow keys follow the screen axes; diagonal speed is normal. E opens tables or delivers a carried tray first. Q opens the same inspection. Escape exits every interaction. |
| Blackjack | All-bust hands end without dealer draws. Stakes return automatically on non-losses; pay only 1× winnings on a win, 1.5× on Blackjack, nothing on a push. Check the finite float and accounting separately. SHUFFLE! remains a retryable two-stack sequence. |
| Roulette COVER! | A working dealer reveals the actual round result before requesting COVER! Click winning bets or their value-labelled chips, not losers; chip rims match the bet-list colors. Test every bet type, zero, overlapping/identical customer bets, NO WINNERS and BET GUIDE. Escape/timeout must preserve the same result and unpaid bets. The button says only COVER! |
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
| Final ten seconds | Confirm the centre shows 10 through 1 once each, with pixel-font pops and audible ticks (stronger for 3/2/1), while movement and task completion remain available. Pause/resume must preserve the countdown. At zero, TIME'S UP! freezes movement, customers, dealers, effects, tasks and all interactions immediately, even with a minigame open. After about two unscaled seconds, the existing pass/fail screen fades in. Complete a real task in the final second and verify final totals; repeat finish callbacks must not award progress twice. Run `FinalCountdownPlayModeValidation.Run` in a visible Editor with `PIT_PANIC_COUNTDOWN_EVIDENCE` set for live captures. |
| Shift results | Finish a timed shift. Only satisfaction and tasks determine results: 4 completed actions, at least 60% happy, at most 5 missed tasks. Dealer fatigue/exhaustion/cover warnings add no misses, including at closing. A requested minigame can add at most one miss, not another for each retry or for remaining unfinished. No hand target or profit statistic anywhere on results or campaign briefings. Failure allows Retry without rewards/unlocks. |
| Campaign progression | Clear Level 1 to unlock High Rollers, then Level 2 to unlock Shady Business. Best stars persist per level; replay is available only for unlocked levels. New Game resets campaign stars/unlocks when a level is selected, while legacy saves and settings remain intact. |
| High Rollers | Confirm VIP guests have full formalwear sprites, top hats and a soft golden aura in every direction/action, not a floating crown. They make larger bets and have shorter patience. Verify a VIP is introduced and the table remains serviceable. |
| Shady Business CCTV | Resolve scheduled alerts at the Pit Desk. Watch the close-up roulette layout: the wager lands before betting closes, the wheel reveals its real result, then the customer reaches. Only a cheating wager moves after the result. Pause/replay must freeze/restart the entire performance. CHEATING or CLEAR shows a persistent CORRECT/WRONG verdict; Okay returns to play. Test both decisions, including evidence for a customer who has already left. |
| Level layouts | Start each unlocked level and verify the Blackjack tables, Roulette table, seats, dealers, interaction zones and customer routes remain accessible in the themed layout. |
| Seating | Watch arrivals at all ten seats in all three layouts. Customers settle onto the visible cushion, including diagonal Roulette seats; chairs stay beside their own table. No floating, compressed bodies, stale table obstacles or persistent offsets after leaving. |
| Animation | Watch more than one full idle/walk/rest cycle. Loops keep playing; deal/collect/pay/shuffle/reaction actions anticipate, make contact and recover through 60 baked frames per direction. Front/back walking alternates steps without spreading feet sideways; waist/legs remain joined. Blackjack customers keep empty hands low and cards on the felt, including VIPs. No frozen last frames or transform-squashed sitting. |
| Popup spacing | Dialogue NEXT and SKIP are separate, aligned and inset. Blackjack Deal, payout chips/Remove/Confirm, Roulette Spin, staff controls and CCTV decisions stay inside their panels without overlapping. Keyboard Space still works. Also try 1024×768 and 2560×1080; popups must fit without clipping. |
| Demo ending | Third level is harder but achievable; clearing it shows demo complete and allows replay. No invisible fourth-level requirement. |
| Performance/readability | Try 1280×720, 1600×900 and 1920×1080, windowed/fullscreen. No clipped controls, blocking labels, stationary agents or sustained stutter at the customer cap. Record average/minimum FPS and hardware. |

Dialogue portraits are cut from the clean original `Assets/Resources/Portraits/StaffPortraits.png`,
not the title pack's damaged extractions. Use **Pit Panic > Art > Extract Clean Dialogue Portraits**
to reproduce all 30 PNGs, with transparent padding and a common baseline, without resampling
or repainting the artwork. Gameplay validation compares every output pixel with its source crop.

The first training conversation opens with Tiger hiring Penguin, includes the four brief
skippable pauses, and ends with “Good. Use WASD or the arrow keys to move.” Movement
practice begins immediately after that line; no separate introduction screen is used.

For each bug record build/commit, shift, tutorial stage, exact inputs, expected
versus actual behaviour, screenshot/video and `Player.log`. Windows logs are at
`%USERPROFILE%\AppData\LocalLow\DefaultCompany\PitPanic\Player.log`.
Report save problems before starting another New Game. Stop distribution for
crashes, unfinishable tasks, lost progression or incorrect money accounting.

## Automated coverage and limits

Development builds accept `--pit-results-preview` to open the real Level 1
zero-customer failure results immediately without awarding progress. To inspect
any illustrated outcome and star count, pass `--pit-results-preview --pit-results-preview-level <1-3> --pit-results-preview-outcome <pass|fail> --pit-results-preview-stars <0-3>`.
Those outcome fixtures are visibly marked PREVIEW ONLY and never save shift
data or campaign progress. Add `--pit-capture-dir <folder>` to save a
`results-preview.png` capture. Retry, Next Level, Level Select and Main Menu
remain functional; a normal launch still opens the title. Import the supplied
pixel-art assets with `Pit Panic/Import Results Screen Artwork` before opening
the results screen.

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

Misses and payouts: let a customer's patience expire at a stalled table or the cashier and check that MISSED increases exactly once immediately, not on a later unrelated action. Dealer energy reaching zero and outstanding staffing warnings must add zero misses. Exit/retry SHUFFLE! or COVER! before its carried deadline: no miss until the deadline expires. Repeated failures of the same requested task must not increase MISSED again; leaving it unfinished must not count it twice in results, but it must still be listed as unfinished. A new task has its own independent miss allowance. Run `CasinoGameplayValidation.RunMissedTaskAccounting` in a batch-mode Editor for the targeted lifecycle regression; `FirstShiftPlayModeValidation.Run` also checks real depletion, staffing warnings, shift-end deduplication and customer timeout. For Blackjack, select the exact winnings and then one extra chip: the extra chip must remain selected, Pay must reject the overpayment, and the hand and float must remain unpaid/unchanged until you recount correctly.

CCTV capture: build a development player, then run it with `--pit-capture-dir <output-directory> --pit-security-check` for before/after replay screenshots and both verdicts. The replay is a reconstruction from recorded round evidence, not a live camera recording. It uses a large detailed roulette layout, captured character pose frames and a 60-frame raster hand sequence; no narrative describes the answer during playback.

Title capture: run the same development build with `--pit-capture-dir <output-directory> --pit-title-check`
and `-screen-width 1600 -screen-height 900 -screen-fullscreen 0`. Repeat at 1200x900 and
1600x675 (the same ultrawide aspect as 2560x1080, sized to fit a laptop display).
This captures arrival, idle, a supplied expressive portrait, settings,
opening doors, level selection and return, then exercises QUIT. It does not start
a campaign or award progress. The full-shift validation checks keyboard selection,
NEW GAME, CONTINUE, saved progression and return paths.
