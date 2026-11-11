# First shift

Open `Assets/Scenes/PitPanicPrototype.unity` in Unity and press Play. Choose
**Start Shift 1** for the guided shift, or **Skip Tutorial** to start without
the lessons. The on-screen objective points to the relevant station. Use WASD
to move, E to interact, Q to inspect a table, and P to pause when no interaction
is open. The tutorial can also be skipped using the HUD button.
During the dealer lesson, BJ1's dealer is deliberately made tired so the
player can practise assigning cover and sending a dealer on break.

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
