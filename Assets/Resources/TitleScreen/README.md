# Pit Panic — Title Screen Asset Pack

Generated from YOUR supplied exterior artwork and YOUR supplied staff portrait grid.
The original exterior artwork is preserved unchanged in Background/. Nothing is regenerated on the building or cast.

## Contents
- Background/Title_Casino_Exterior_1672x941.png — supplied exterior at its native 1672 × 941 resolution. Logo is already baked in. No buttons are baked in.
- Portraits/*.png — 30 transparent 256 × 256 character expressions: Penguin, Tiger, Bear, Fox, Pig × Neutral, Happy, Determined, Shocked, Angry, Smug.
- Door/Doorway_Warm_Reveal_FullCanvas.png — a simple warm portal layer for the brief menu transition.
- Door/Door_Left_Closed_*.png and Door_Right_Closed_*.png — precise cutouts from your image, in both matching full-canvas and tight-cropped versions. See manifest.json for image-space bounds.
- FX/Marquee_Light_Pulse_FullCanvas.png — a low-opacity duplicate emission/light pulse mask. Use it sparingly.
- FX/Sparkle_*.png — OPTIONAL small subtle glints. Avoid if they conflict with the original art direction.
- Reference/Portrait_Sheet_Original.png — unchanged portrait sheet.
- Reference/Preview_DoorsClosed_Alignment.png and Preview_DoorsAlmostOpen.png — layering previews (not runtime assets).
- manifest.json — source coordinates and portrait naming.

## Critical integration notes
1. **Do not use the closed-door background alone during opening.** Overlay the warm doorway portal ON TOP of the existing background, then the left/right door cutouts on top of the portal. Doors initially align with the background image. Shrink/move each leaf toward its OUTER hinge until the portal shows through. Once the doors open, fade into the Level Select screen. (Using the two tight-cropped door sprites simplifies hinge animation.)
2. Door panels are simple rectangular extracted sprites, not independently re-painted hinged doors. Inspect visually in Unity. For a subtle, brief opening transition they should be adequate; if the look is poor, use an iris/light wipe instead of forcing mismatched hinge geometry.
3. Use the real existing in-game Penguin sprites and animations for the arrival. **Do not turn portrait images into walking sprites.**
4. The marquee mask should be an alpha-pulsing overlay (gentle intensity), not an aggressive blinking neon effect.
5. Functional menu buttons and dialogue must be Unity UI. The original background has the PIT PANIC title baked into the artwork; never draw a duplicate logo over it.
6. Use a 1672×941 reference-canvas in your scene or scale all full-canvas overlays uniformly under the same aspect-fit RectTransform.
7. The portrait sheet is not perfectly gridded; the individual portraits were extracted and normalised. Check the sprites for any edge artefacts in Unity, and trim manually if needed.
8. Do not claim the exterior animation or dialogue is implemented by this ZIP: these are art assets and reference guides for the Copilot integration task.
