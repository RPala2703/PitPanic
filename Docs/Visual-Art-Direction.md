# Pit Panic pixel-art direction

The current scene is a pixel-art pass with a warm casino palette, dark outlines,
point-filtered sprites and an orthographic overhead camera. The sprites are
original and inspired by the readability of character-led pixel games, not
their assets.

`Assets/Art/Pixel` contains the project's generated PNG sprites. The pit boss,
dealers and customers have four facing directions and two walking frames.
Blackjack, roulette, the desks, the break room and the carpet have pixel surfaces;
the roulette wheel sprite follows the existing spinning wheel transform. Columns
and entrances use camera-facing pixel sprites. Blackjack table cards, bet chips,
roulette chips and the carried float tray use point-filtered runtime sprites.
Minigame panels use a matching palette and the Silkscreen pixel font. Card suit
symbols use Unity's built-in font because Silkscreen does not include them.
Silkscreen is copyright 2001 The Silkscreen Project Authors and is distributed
under the SIL Open Font License 1.1; the licence is included alongside the font
in `Assets/Resources/Fonts`.

## Regeneration

Choose **Pit Panic > Generate Prototype Scene** to rebuild the scene, or
**Pit Panic > Run Prototype Validation** to regenerate it and check the gameplay
wiring and pixel sprites. The generator creates missing pixel assets and pixel
character prefabs without overwriting existing ones. PNG import settings use
point filtering, no mipmaps and no compression. Existing PNGs can be edited in a
pixel-art editor; keep their dimensions, pivots and import settings.

Gameplay continues to use the existing X/Z-plane colliders, navigation and
interaction scripts. Legacy render meshes are hidden where replaced, but kept
in the hierarchy to preserve gameplay references. The 3D world-space movement
and minigame logic have not been rewritten.

Play Mode still needs a visual and gameplay review: character occlusion, sprite
scale, frame animation, roulette and blackjack activity, task indicators and
all minigames should be checked at the intended window resolutions.
