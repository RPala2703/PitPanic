# First shift

Open `Assets/Scenes/PitPanicPrototype.unity` in Unity and press Play. Choose
**New Game** for the introduction and Tiger's guided shift, or **Skip Training** on the introduction to start without
the lessons. Tiger's large lesson cards pause the casino and audio. **Let Me Try**
or Enter dismisses the explanation and resumes practice; Escape dismisses a lesson
rather than opening another menu. **HELP** revisits all 13 explanations without
changing progress. Gold arrows point to the relevant station. Use WASD
to move, E to open a table and play its available task, and P to pause, including
during a minigame. The tutorial can also be skipped using the HUD button.
Q remains a compatibility shortcut to the same table interface. When carrying
a float tray, E delivers the tray before opening a table. Escape closes an
interface or cancels a minigame without trapping movement.
Outside an interaction, Escape opens the pause menu; P or Escape resumes.
Pause freezes the shift, customers, dealers, minigame timers, visual effects and audio,
and blocks input to the underlying interface. **Resume** keeps your current task open.
**Main Menu** abandons the current shift and returns to the title without erasing
unlocked shifts or tutorial completion; it does not save a mid-shift checkpoint.

The animated full-screen title presentation uses its own canvas scenery,
pauses the simulation and hides the gameplay camera. **Settings** provides
a persisted sound toggle and fullscreen toggle; **Quit Game** exits the standalone
(or stops Play Mode in the Editor). It is not a second gameplay scene.
During the dealer lesson, BJ1's dealer is deliberately made tired so the
player can practise assigning cover and sending a dealer on break.
Training protects other dealers from incidental fatigue until lessons end;
the fatigue and takeover lessons deliberately tire the relevant staff.
Guided customers receive a real four-minute patience allowance so staff-recovery
practice is not an unavoidable satisfaction failure. Normal arrivals and their
next rounds return to the usual shift patience after training.
Dealers lose 0.5 energy per second during active service and half that while
waiting at a table. From full energy, the 30% break warning arrives after
about 2 minutes 20 seconds of busy work, or 4 minutes 40 seconds of idle duty.
Break recovery is 1.5 energy per second: about 45 seconds from the break-warning
threshold to ready. This keeps rotating three tables with one reserve feasible.
At zero energy a dealer stops servicing their table. Customers lose patience even
during an interrupted round, eventually leaving and lowering satisfaction. Lost
customers mean lost earning opportunities, and unresolved dealer-cover tasks hurt
the shift rating. Assign cover and send the exhausted dealer on break; this is not
an arbitrary cash fine and does not change payout accounting.

The shift duration defaults to nine minutes and can be changed on the
**First Shift Controller** in the scene. When time runs out, arrivals stop and
the results appear after the current interaction closes. Table profit is settled
stakes minus gross returns; float deliveries are transfers, not income.
Clear Shift 1 by serving **8 settled hands/bets**, completing **4 assistance,
delivery or staff actions**, and keeping **at least 60% satisfaction**. Goals are
shown on the existing objective HUD. Later demo shifts require 12 and 16 hands.
Profit is reported but is not a pass condition: a lucky roulette result should
not randomly fail an otherwise well-managed shift.
Satisfaction reflects the share of spawned customers who did not leave because
their patience expired. Missed tasks include pending minigames, unfinished
float deliveries and tables awaiting dealer cover; a float's packing prompt is
not counted a second time. Failed or abandoned attempts are listed separately.

Tutorial completion and the highest unlocked shift are stored locally in
Unity PlayerPrefs. **Continue** resumes at the highest unlocked shift; **Next
Shift** opens **Pit Perks** before loading the next shift. A cleared shift awards
1–3 stamps based on its rating; each permanent perk costs 3 stamps. Perks add
5 seconds of customer patience, improve rest recovery by 20%, or add 5 seconds
to PACK! orders. They apply on the next shift and survive Continue/relaunch.
Failed shifts award nothing and do not unlock the next shift. Both results and
pause provide retry/restart; upgrades can be skipped without purchasing.

The demo has three shifts using the same floor rather than bespoke content.
Arrivals increase by about 12% per shift and fatigue drain by 6%; patience starts
at 55 seconds and drops by 5 seconds per shift before perks. Completing Shift 3
shows a demo-complete message and allows replay. New Game resets progression
and perks, as stated in the introduction, but leaves sound/settings unchanged.
Training retains at least three minutes on the clock until lessons are complete,
so a learner cannot time out before learning dealer management. Practice covers
movement, table inspection, DEAL!, SPIN!/SWEEP!/PAY!, customer cash-out, float
packing/delivery, assignments/breaks, both manual table takeovers, returning
staff to duty and reading the shift goals. It forces real customers/tasks rather
than depending on random requests, and advances after successful actions.

## Task urgency and emergency dealing

Station meters and task-HUD seconds read actual customer patience, active
DEAL!/PACK! attempt time and working-dealer exhaustion time. Green means more
than half remains, yellow means 20–50%, red means under 20%. Expired customers
leave and lower satisfaction. Failed attempts stay retryable; their attempt
meter closes, while an outstanding customer's patience continues. Float orders
and staffing assignments have no invented expiry timer. Approach an ! for
specific instructions, including where to get float and which key to use.

At an exhausted or unstaffed table, **E → Take Over** enables pit-boss service.
At Blackjack, **Space / Deal Card** advances the existing hand; customers choose
Hit/Stand using their existing AI. In settlement, Space pays the exact return,
or use the chip/confirm buttons. At Roulette, Take Over first keeps the table
available for customers; **Space** then starts its existing SPIN/SWEEP/PAY round.
Both use the current round and existing payout/deduplication paths, not new bets.
Escape relinquishes table ownership; a returning dealer resumes the same state.
The Roulette task retains ownership through its settlement/clearing animation,
then releases it. At Pit Desk, send outgoing exhausted staff on break before
waiting for them to cover another table.

The batch-mode interaction check can be run from the project directory:

```sh
"/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -projectPath "$PWD" \
  -executeMethod FirstShiftPlayModeValidation.Run -logFile /tmp/pit-first-shift-test.log
```

This automated check uses synthetic input and moves the player between stations;
some roulette and staff UI actions are invoked directly. It does not replace a
visual playthrough.

## Artwork and presentation

`Pit Panic/Upgrade Pixel Art` rebuilds the original character, expression,
station, furniture and carpet PNGs while preserving existing texture GUIDs.
`Pit Panic/Generate Prototype Scene` also runs the art upgrade and wires
animated lamps and a cashier into the generated floor. Sprites use point
filtering without mipmaps or compression.

Characters have directional walk cycles, service poses, happy/sad reactions
and tired/resting expressions. Blackjack reuses its physical dealing/payout
animation; roulette adds customer/dealer gestures and an orbiting ball.
The cashier's chip movement follows actual customer/float service activity.
The playable pit boss is a penguin. Staff use their animal names, and customers
use the ten-species character pool with separate civilian-shirt animation atlases,
rather than staff uniforms. Away-facing seated hands and cards reach toward the
table, with direction-aware resting and service poses.
Working dealer visuals step toward the felt while their collision/navigation
positions remain outside the table, preserving access and break-room routes.

PACK! allows **15 seconds to read the order plus 2 seconds per chip**, without
the old ten-second cap. A wrong sequence chip costs half a second, and cancelled
or timed-out orders remain retryable. This applies to float packing and customer
cash-outs.
Both cash games use **click-to-select**, never dragging. **Undo Last Chip**
works in both, including a completed cash-out tray before Finish. The shared
denominations now match Blackjack/Roulette: red £5, blue £2.50, green £25,
black £100. The standard refill still totals **£200**: 10 red, 20 blue, 4 green.
Starting blue inventory/register counts were increased fourfold to preserve
their original monetary value when correcting the former £10 mismatch.
The penguin uses holding poses even while standing, and one direction-aware
tray graphic follows its hands rather than duplicating a tray inside the atlas.

A development macOS build with automated presentation captures can be made
without hardcoded output paths:

```sh
PIT_PANIC_BUILD_PATH="/absolute/output/PitPanic.app" \
  "/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -projectPath "$PWD" -executeMethod PitPanicPresentationBuild.Run \
  -quit -logFile /tmp/pit-build.log
"/absolute/output/PitPanic.app/Contents/MacOS/PitPanic" \
  --pit-capture-dir "/absolute/output/screenshots" \
  -screen-width 1600 -screen-height 900 -screen-fullscreen 0
```

Capture mode records the title, introduction, paused tutorial, casino and table interfaces, then exits. It only
exists in Editor/development builds and is inactive without the explicit flag.
It keeps rendering when unfocused, and does not complete or save tutorial progress.
The screenshots are rendered frames, not evidence of human playtesting.

## Living casino animation and audio

The existing `PixelCharacterVisual` selects the shared eight-frame action atlas,
not a second Animator. Each of the ten species has four directions for idle,
walking, dealing, collecting, paying, celebration, disappointment, tiredness,
rest, carrying, sitting, examining cards and shuffling. Sitting is played in
reverse when standing. Service reaches include anticipation and follow-through;
navigation and accounting remain authoritative. The generator checks exact
atlas dimensions, point filtering and at least four distinct drawn poses in
each eight-frame front-facing action (held poses are intentional).

Blackjack card/chip arrival cues come from its physical mover, with a short
anticipation delay before launch. Roulette bets and winnings travel on the
floor without changing settlement accounting. Customer reaction states follow
actual round outcomes, and tired/rest states follow dealer duty.
Customer sprite palettes are kept intact rather than tinting the whole animal
blue/red to represent an outfit; impatience uses a restrained warm tint.

`Pit Panic Welcome!` loops on the title screen; `Pixelated Roulette` loops during
shifts. Gameplay music is deliberately mixed at 12% of the Music slider level
(default effective gain .054) so sound effects remain prominent; title music uses
72%. The supplied MP3s are in `Assets/Resources/Audio/Music`. Original synthesized
chatter and effects remain generated locally; no other recordings were added.
Settings provides separately persisted **Music**, **Ambience** and **Effects**
sliders alongside the master mute. Card, chip, footstep, roulette, outcome,
task-result and warning sounds use a bounded voice pool and cue rate limits.
The original melody runs slightly faster toward the end of a shift.
Opening a table, Pit Desk or minigame stops outstanding world effects and mutes
casino chatter. Only effects owned by the active interface/task can play;
closing it restores the world soundscape. Music remains independently controlled.
Pause silences all channels until resumed.

Customer arrivals ramp from about 17 seconds apart to 9 seconds, then about
7 seconds in the finale, retaining the existing customer cap and task scheduler.
Dealer recovery is about 45 seconds from the tired threshold to ready before perks.
Blackjack optional assistance has a 60-second per-table gap and becomes guaranteed
after four eligible rounds once that gap has elapsed; tutorial requests bypass
the gap. Essential float and staffing warnings are never suppressed.
Cashier customers get 35 seconds more patience than table customers; abandoned
cash-outs expire safely instead of filling the entire casino with an endless queue.
Blocked journeys release customer reservations or use existing dealer route
recovery after 20 stationary seconds, with a diagnostic warning rather than teleporting.

Add `--pit-watch` to capture mode to run a minute of normal autonomous casino
activity and save twelve spaced watch frames. It temporarily skips training,
restores the tutorial preference before exiting normally, and does not finish
a shift. Automated captures and synthetic-input shift tests do not assess
human enjoyment, audio mix quality or a nine-minute natural playthrough.
`--pit-demo-check` runs a full unattended shift at 10× simulation speed, checks
failure/no-reward behaviour and results Retry, then exits. This development-only
check is not a successful human playthrough. Capture modes restore all progression
preferences on normal exit; force-killing a process prevents that cleanup.

## Windows demo build

Install **Windows Build Support (Mono)** for Unity 6000.3.26f1 through Unity Hub, then:

```sh
PIT_PANIC_BUILD_PATH="/absolute/output/WindowsDemo/PitPanic.exe" \
  "/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -projectPath "$PWD" \
  -executeMethod PitPanicPresentationBuild.RunWindowsDemo \
  -quit -logFile /tmp/pit-windows-demo-build.log
```

This builds a non-development x64 Windows demo without capture/debug controls.
Distribute the **entire output folder**, not just `PitPanic.exe`; UnityPlayer.dll,
the data folder and Mono runtime are required. Extract the archive before launching.
See [Playtesting.md](Playtesting.md) for the external test checklist and known limits.
