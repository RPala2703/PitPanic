# First shift

Open `Assets/Scenes/PitPanicPrototype.unity` in Unity and press Play. Choose
**New Game** for the short introduction and guided shift, or **Skip Training** on the introduction to start without
the lessons. The on-screen objective points to the relevant station. Use WASD
to move, E to open a table and play its available task, and P to pause when no interaction
is open. The tutorial can also be skipped using the HUD button.
Q remains a compatibility shortcut to the same table interface. When carrying
a float tray, E delivers the tray before opening a table. Escape closes an
interface or cancels a minigame without trapping movement.

The animated full-screen title presentation uses its own canvas scenery,
pauses the simulation and hides the gameplay camera. **Settings** provides
a persisted sound toggle and fullscreen toggle; **Quit** exits the standalone
(or stops Play Mode in the Editor). It is not a second gameplay scene.
During the dealer lesson, BJ1's dealer is deliberately made tired so the
player can practise assigning cover and sending a dealer on break.
Dealers lose 0.5 energy per second during active service and half that while
waiting at a table. From full energy, the 30% break warning arrives after
about 2 minutes 20 seconds of busy work, or 4 minutes 40 seconds of idle duty.
Break recovery remains unchanged, so rotating cover matters during the shift.

The shift duration defaults to nine minutes and can be changed on the
**First Shift Controller** in the scene. When time runs out, arrivals stop and
the results appear after the current interaction closes. Table profit is settled
stakes minus gross returns; float deliveries are transfers, not income.
Satisfaction reflects the share of spawned customers who did not leave because
their patience expired. Missed tasks include pending minigames, unfinished
float deliveries and tables awaiting dealer cover; a float's packing prompt is
not counted a second time. Failed or abandoned attempts are listed separately.

Tutorial completion and the highest unlocked shift are stored locally in
Unity PlayerPrefs. **Continue** resumes at the highest unlocked shift; **Next
Shift** reloads the casino with the next shift number. Later shifts currently use
the same floor and scheduling rather than bespoke content.

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
use the ten-species character pool with distinct outfits and species details.
Working dealer visuals step toward the felt while their collision/navigation
positions remain outside the table, preserving access and break-room routes.

PACK! allows **15 seconds to read the order plus 2 seconds per chip**, without
the old ten-second cap. A wrong sequence chip costs half a second, and cancelled
or timed-out orders remain retryable. This applies to float packing and customer
cash-outs.

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

Capture mode records the title, introduction, casino and table interfaces, then exits. It only
exists in Editor/development builds and is inactive without the explicit flag.
It keeps rendering when unfocused, and does not complete or save tutorial progress.
The screenshots are rendered frames, not evidence of human playtesting.
