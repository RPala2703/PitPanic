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

## HUD and minigame style

The floor HUD is a compact, top-right task card: the highest-priority task,
remaining task count and float status stay visible. The + button expands the
card to show the next task and dealer summary; - collapses it again. A gently
pulsing warm badge calls attention to pending work; green
means the floor is clear. Task names and station labels stay short and actionable.

Minigames use dark plum framing, stepped pixel corners and a large coloured title
badge. Teal identifies DEAL!, gold roulette service and mint PACK!. Gameplay
areas remain visually distinct from the frame, with bright, high-contrast
instructions, large chip targets and tactile hover/press states. The shared
reference canvas is 1600 x 900; use approximately 30–34 units for titles,
20–25 for primary status, 16–19 for body and action text, and reserve smaller
type for dense board labels. Keep suits in the symbol font and preserve point
filtering on pixel assets.

Roulette inspection now gives the betting grid most of the table space, and the
SWEEP! and PAY! grids use larger number cells. Roulette screens share an original
point-filtered pixel wheel, with the same European pocket order as the spin
logic; cards, chips and title sparkles are original runtime-generated pixel art.
The wheel, ball, cards and payout chips use the existing game animations, while
subtle sparkles animate independently of the interaction targets.

Service screens lead with a short action and a stage-specific pixel burst title
that pops on transitions. PACK! shows the next few cash-out chips rather than
an entire long sequence, while preserving the exact order. Roulette inspection starts with a concise live summary; MORE
INFO reveals bets, history and float. Dealer controls keep break and assignment
actions visible while shortening status copy. A point-filtered, animated orange
exclamation badge marks each station with outstanding attention requests; it
remains visible until all reasons for that station are resolved.

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
