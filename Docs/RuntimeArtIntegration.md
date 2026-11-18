# Approved art in the real campaign

## Baseline and preservation

- Base: `three-level-campaign`, `b8650190469552648f83620ba7374c6548076024`.
  This is newer than `main` (`ec0f0de`), which the failed preview used.
- Work branch: `rpala2703-runtime-art-integration`.
- Before editing, restored and SHA256-verified all **197** recovered working files
  from the coordinating session's `runtime-art-recovery/uncommitted-files.tar.gz`.
  The recovered minigame/UI artwork, scripts and rendering settings are retained.
- Protected original worktree `rpala2703-solid-lamp` remains unchanged against all
  197 manifest hashes. No preview scene, preview builder or preview renderer was copied.
- Copied only the **47 original PNGs** from the supplied
  `CharacterCasinoPreview` art folder: 17 characters and 30 props.
  Their image bytes match the source. New Unity metadata uses independent GUIDs.
- No merge or pull request was created.

## Runtime integration

`Assets/Scenes/PitPanicPrototype.unity` is still the playable scene. Its existing
objects, navigation roots, colliders, interaction anchors, dealer stations,
customer seats and minigame components are retained. The art migration changes
existing visual children; it does not place decorative customer objects.

| Surface | Integration |
|---|---|
| Real Penguin player | Existing `PixelCharacterVisual` selects approved Penguin art |
| Tiger, Bear, Fox, Pig | Existing four live dealers, unchanged assignments and energy logic |
| Rabbit, Dog, Cat, Raccoon, Deer, Elephant | All six real spawner prefab references use matching approved sprites |
| Six VIP appearances | Existing `CasinoCustomerAI.SetVip()` selects matching approved VIP art, including High Rollers opening guests |
| Blackjack, Roulette, Cash Desk, Pit Desk | Existing scene SpriteRenderers use approved station art |
| Ten independent seats | Existing `CasinoSeatVisual` chair children use rear-facing Velvet Chair View05 or the supplied stool angle matching the seat direction |
| Existing scenery | Plants, windows/curtains, lamps and sofa use supplied decor |

The former eleven-species customer pool is intentionally restricted to the six
supplied customer/VIP species in both the saved spawner and its generator helper.
All eleven character prefabs are retained. The other five species remain the
real player/staff roles. This prevents a spontaneous staff-species customer from
requesting nonexistent supplied VIP art; it does not change arrival timing,
customer limits, table selection weights, navigation or customer mechanics.

Modified runtime scripts:

- `Assets/Scripts/Characters/PixelCharacterVisual.cs`: owns serialized regular/VIP
  selection, static-mode rendering and central species height targets. Existing
  poses, action timers, sounds and gameplay-facing methods remain functional.
  Title/replay sprite APIs also return the approved standing appearance.
- `Assets/Scripts/Characters/PixelSpriteTextureBinding.cs`: explicit refresh when
  switching sprites makes VIP texture selection correct immediately, not just
  after its next LateUpdate.
- `Assets/Scripts/Characters/CasinoCharacterVisual.cs`: bypasses the old procedural
  body animation only while approved art is selected; gestures still reach the
  gameplay pose API.
- `Assets/Scripts/UI/CasinoPresentationCapture.cs`: recognizes approved VIP
  formalwear instead of requiring a legacy procedural golden aura.

All eleven `Assets/Prefabs/Characters/Character_*Pixel.prefab` assets retain their
old frame/expression/animation references for recovery. No old art was deleted.
Approved mode does not draw the legacy mesh, atlas frames, expressions or aura
over the replacement. VIP clothing is supplied artwork, not a tint substitution.
The user-requested silhouette glow described below is a separate effect behind
the approved VIP sprite, not a return to the old procedural character graphics.

`ApprovedCasinoArtIntegration.Run` is a targeted, idempotent scene/prefab migration.
The existing prefab builder and prototype generator call its configuration hooks,
so later intentional generation retains the integration. **The full procedural
generator was not run for this change**, since it regenerates legacy artwork.

The approved Blackjack artwork already includes the stock chips, tray and shoe;
the previous station illustration is replaced rather than layered beneath it.
Actual round-derived cards, wagers and payouts remain live. Roulette's old
overlaid wheel SpriteRenderer is disabled because its approved table already
contains a wheel; the physical wheel transform and minigame references remain.

## Imports and measurable scale

All supplied images: point filtering, 32 pixels/unit, uncompressed, no mipmaps,
full-rectangle mesh and bottom-center pivot. Aspect ratio is preserved.
Character scale is applied only to the SpriteRenderer child, compensating for
parent scale; gameplay roots, agents and colliders are not enlarged.

| Species | Standing sprite canvas height, world units |
|---|---:|
| Penguin | 2.2 |
| Tiger | 2.2 |
| Bear | 2.24 |
| Fox | 2.2 |
| Pig | 2.357143 |
| Rabbit | 2.4 |
| Dog | 2.5 |
| Cat | 2.3 |
| Raccoon | 2.5 (matched to Dog) |
| Deer | 2.7 |
| Elephant | 3.15 (slightly increased from 3.0) |

VIP and ordinary variants share the same species target. These are full sprite
canvas dimensions; transparent border pixels are not represented as ink height.
Blackjack width is 6.2 units, Roulette 6.0, Cash Desk 4.0, Pit Desk 4.4;
independent chair height is 1.3. Thus ordinary customer heights are about
37-48% of Blackjack width and 1.8-2.3 times chair height, with Dog and Elephant
deliberately substantial. The existing camera projection is unchanged.

The four dealers now use Penguin's apparent height as their reference. Penguin,
Tiger and Fox have 60 opaque pixel rows in their 64-row canvases; Pig has 56,
so its slightly larger canvas target compensates for transparent padding.
Bear retains a small 2% bulk allowance. Character art, animation/state logic,
root transforms and colliders are unchanged by this size correction.

## Casino cleanup follow-up

The carpet portion of this earlier cleanup is superseded by the supplied
burgundy flooring below; its wall, chair and furniture changes are retained.

The latest integrated scene, not an earlier layout, received a visual-only pass:

- Each campaign carpet reuses the brighter motif-free **16x16 region at (270,100)**
  of its existing floor PNG. Only a sprite rectangle is selected; no PNG pixels,
  procedural texture, replacement artwork or placeholder shape are generated.
  The original 38x22 world-space backdrop footprint and three level palettes remain.
  Baked top wall panels and bottom planks no longer appear across the floor.
  Six small serialized Sprite assets reference rectangles of the original PNGs;
  the clean appearance is saved in the actual scene as well as selected at runtime.
- The north wall is a separate visual-only backing using the existing PNG's
  wall strip at (0,276), size 608x76. Windows align to that backing; duplicate
  corner curtain pictures are hidden. Two old northern column
  images use the imported wall-sconce image; southern column images are hidden.
  There is **no separate replacement wall or doorway PNG** in the supplied pack.
  The current wall strip is reused, not invented; old procedural entrance images are
  hidden, while the actual arrival/exit anchors and navigation remain.
- The obsolete generated break-room block is hidden. The existing imported sofa
  and side-table/lamp are aligned at the rear of the actual rest area, with the
  sign above them and its front approach left open. Dealer rest anchors are intact.
- Independent Blackjack chairs use the rear-facing View05, facing their tables.
  Roulette stools use the supplied side/front/back view matching each real seat.
  Chair visual children align to existing sitting positions, and the original
  pull-out animation remains enabled. No seat or betting point was moved.
- BJ1, BJ2 and ROULETTE signs are outside table surfaces. Play Mode assertions
  compare projected label rectangles against projected table-sprite rectangles.
- Old generated table rugs, entrance pictures and room block are hidden rather
  than deleted; all corresponding gameplay objects remain.

Cleanup evidence is separate from the earlier integration evidence:

`/Users/rahulpala/.copilot/session-state/753e4550-8d41-464b-adaf-1a9686ebf1df/files/casino-cleanup/`

`integration-final.log` records saved-backdrop migration/compilation;
`visible-art-final-retry.log` records the final three-level live-art pass and six
fresh `level-N-live-floor/service.png` captures.
`full-shift.log` records the complete gameplay regression after the layout/scale
changes (before saving the same carpet crop and adding the visual-only wall).
`build.log` records a successful Unity 6000.3.26f1 macOS standalone build of the
final scene, including the saved wall and carpet sprites. The app is
`Pit Panic.app` in the same evidence folder.
The same entry-point commands documented below apply with this evidence directory.
The cleanup snapshot is `before-source.tar.gz` with hashes in `before.json`.
`cleanup-audit.json` confirms unchanged PNG bytes, physics components, existing
scene gameplay scripts and gameplay-anchor transforms. Only one visual-only
north-wall object (five components) was added; no scene object was removed.
The live-art pass additionally checks dealer opaque-image heights within 3%
of Penguin, real chair/seat alignment and unobstructed table labels.
An initial final-capture launch crashed in Unity Editor's UIElements/TextCore
text-job code before the game test began (`visible-art-final.log`). Relaunching
with `-job-worker-count 2` passed. The pre-existing Editor Search indexing
exception remains visible in startup logs; neither issue is hidden as a game pass.

## Verification and evidence

Unity version: **6000.3.26f1**. Evidence folder:

`/Users/rahulpala/.copilot/session-state/753e4550-8d41-464b-adaf-1a9686ebf1df/files/runtime-art-evidence/`

- `integration-pool-final.log`: final batch compilation and saved scene/prefab
  migration, including the corrected six-species customer pool, passed.
- `full-shift-boundaries.log`: complete gameplay Play Mode regression passed
  after correcting the baseline approach/boundary assumptions, before the final
  customer-pool correction.
- `full-shift-pool-final.log`: **final-after-pool-change complete gameplay
  regression passed** (`FULL SHIFT INTERACTION TEST PASSED`), covering movement,
  SHUFFLE, Roulette SPIN/COVER/payouts, cash-out/float packing and delivery,
  betting/settlement/tips, crew assignments/exhaustion/breaks, tutorial,
  pause/restart, results, saves and all three levels.
- `visible-art-pool-final.log`: **final-after-pool-change actual visible-Editor
  Play Mode art test passed**, including isolated synthetic WASD input and real
  Penguin movement on each level, the exact saved six-species pool, pixel import
  settings,
  approved sprite **and URP `_MainTex`** assertions after repeated LateUpdates,
  all six ordinary and all six VIP real-spawner species, navigation, seating,
  live service, all four staff identities, and all three campaign layouts.
- `level-1-live-floor.png`, `level-1-live-service.png`,
  `level-2-live-floor.png`, `level-2-live-service.png`,
  `level-3-live-floor.png`, `level-3-live-service.png`: 1920x1080
  `ScreenCapture.CaptureScreenshot` frames taken during active Play Mode.
  These are not gallery scenes or edit-mode `Camera.Render` images.
  Final frame numbers: level 1 = 179 / 198; level 2 = 443 / 462;
  level 3 = 643 / 662. All three final floor images show unobscured gameplay,
  not a fullscreen minigame. Existing world labels/task markers remain visible.

Entry points:

```text
Unity -batchmode -quit -projectPath <worktree> -executeMethod ApprovedCasinoArtIntegration.Run -logFile <log>
Unity -batchmode -projectPath <worktree> -executeMethod FirstShiftPlayModeValidation.Run -logFile <log>
PIT_PANIC_ART_EVIDENCE=<folder> Unity -projectPath <worktree> -executeMethod ApprovedArtPlayModeValidation.Run -logFile <log>
```

The visible art test uses the real spawner's species selection, real agents and
real seats. It does not instantiate screenshot-only customers or disable table
gameplay. It backs up and restores progression preferences. Visible capture needs
a presented Game View: the initial headless attempt correctly failed when
`ScreenCapture` produced no frame.

## Validation defects found while integrating

The original comprehensive runner could hang forever inside a nested iterator
or a scaled wait despite a stored assertion failure. It now traverses nested
iterators and has an independent Update-time deadline/error guard, restores
preferences and removes injected input devices on exit.

The baseline gameplay explicitly allows the approach lessons at stages 1 and 3
before the corresponding task is ready, but baseline tests required SHUFFLE/COVER
readiness at those approach stages and assumed the next popup was immediate.
The tests now assert staffed approach stations and await the real safe
deal/round boundary. **Actual task-stage readiness checks at stages 2 and 4 and
all minigame/crew/progression assertions remain.** No production tutorial logic
was altered to satisfy a test.

A visible run reproduced an immediate VIP sprite/property-block mismatch on a
newly spawned Deer before the binding component's first following LateUpdate.
The explicit texture refresh fixes this transition. This proves that specific
race; it does not prove that texture order was the sole cause of the old failed
preview implementation.

An additional isolated-input run found the original eleven-species pool could
randomly assign VIP status to a staff species without approved VIP artwork.
This is fixed by the six-species customer pool, not by substituting or tinting
unsupported art.

Unity also logs a pre-Play `UnityEditor.Search.SearchDatabase` indexing
`ArgumentOutOfRangeException` in this environment. It occurs before the game test
listener and is an Editor indexing issue, not a clean game-log claim. Gameplay
errors are not filtered or suppressed by the test.

## Deliberate limitations

### Full-body source/import audit

All **17** current approved standing PNGs (six regular customers, six VIPs,
four dealers and Penguin) from the user-supplied corrected character pack were inspected and force-reimported using
`ApprovedCasinoArtIntegration.ReimportStandingCharacters`.
The corrected PNGs replaced the previously cropped source cutouts byte for
byte; only the 48x64 game-resolution variants were installed. Their high-res
variants, previews and roster sheet are kept in the source package, not used
as character sprites. All 17 corrected game images are 48x64 and include shoe/
foot pixels in their bottom four rows.

The character-only import hook explicitly enforces Single Sprite, Full Rect,
32 PPU, bottom-center pivot (24,0), no NPOT resizing, no mipmaps, no extrusion,
point filtering and uncompressed default/Standalone imports. GUIDs and source
PNGs are preserved per supplied source file and Unity meta GUIDs are unchanged,
so existing prefab references resolve to the corrected images without changing
the scene layout.

The audit compares each imported texture against a fresh decode of its PNG:
every alpha value and every fully opaque pixel matches. It also checks the
full (0,0,48,64) source/texture rectangle, four mesh vertices, six triangle
indices, complete 0..1 UVs and full canvas bounds. Live validation checks those
same bounds on all regular/VIP/player/dealer renderers after repeated LateUpdates,
Simple draw mode, no SpriteMask, full canvas inside the camera frame, and foot
pivots above the floor. The real spawner, navigation, seating and all three
campaign layouts passed again.

Live validation also independently asserts Raccoon and Dog have equal target
heights and Elephant uses the new 3.15-unit target (5% above the previous 3.0).
The corrected PNGs are the user's recut exports from the original roster;
the style and clothing are retained.

Evidence:
`/Users/rahulpala/.copilot/session-state/753e4550-8d41-464b-adaf-1a9686ebf1df/files/full-body-import/`

- `fixed-pack-before.tar.gz` and `fixed-pack-before.json`: source preservation
  snapshot immediately before copying the user's corrected pack.
- `fixed-pack-audit.json`: all 17 installed images are byte-identical to the
  supplied 48x64 files and all existing sprite GUIDs are unchanged.
- `reimport-final.log`: all 17
  post-reimport full-image checks passed.
- `play-mode-final.log`: all 17 standing appearances checked live, source-canvas
  screen bounds and foot heights logged; three-level live-art pass.
- `full-shift-final.log`: complete tutorial, minigame, crew and progression
  regression after the corrected standing-art and scale changes.
- `build-final.log`: successful Unity 6000.3.26f1 macOS standalone build.
- `Pit Panic.app`: current build from the corrected images and final scale targets;
  it was launched and left open. `player.log` is its runtime log and
  `standalone-current-window.png` shows the running application.
- Six `level-N-live-floor/service.png` screenshots are actual Play Mode captures.
  Existing dialogue, HUD and other actors can occlude an actor; they were not
  hidden to stage evidence. The source previews distinguish occlusion from
  artwork that genuinely ends above the feet.
- `../fixed-characters-source.png`: the supplied package contact sheet.

Only the 17 standing PNGs, two species canvas-height values, import/Play Mode
validation and this report changed in this follow-up. No scene layout, gameplay
logic, character prefab references, PNG artwork style or animation behavior
changed.

### Supplied burgundy casino flooring

The latest playable scene now uses the unchanged
`PitPanic_Burgundy_Floor_Pack/Burgundy_Casino_Floor_Tile_Atlas.png`.
Its SHA256 is
`bd725ed8db01e8af2d6ff9c254f915258c3445dcf366aa6392b96f4196ed8d65`.
The imported PNG is byte-identical to the supplied file. No texture pixels,
patterns, masks or replacement artwork were generated.

The atlas is **not a regular tileset**. These individually inspected rectangles
use top-left PNG coordinates; saved Sprite assets convert to Unity's bottom-left
origin:

| Crop | PNG rectangle (x, y, width, height) | Use |
|---|---|---|
| Carpet | 1004, 510, 160, 160 | Quiet dark burgundy, not the prominent all-over motifs |
| Runner | 280, 951, 103, 64 | Red central aisle with two continuous supplied gold rails |
| Corner | 26, 512, 14, 14 | Outer frame corner; flipped for the other three corners |
| BorderHorizontal | 164, 512, 64, 14 | Narrow perimeter gold edge |
| BorderVertical | 26, 560, 14, 64 | Matching side edge |

All five sprites reference the original atlas at 32 PPU, FullRect, point
filtering, no mipmaps, no compression or NPOT resizing. Unity continuous tiled
SpriteRenderers retain native pixel proportions at unit scale instead of
stretching the images across the room. Base carpet keeps its original 38x22
world footprint. The 103/32-unit-wide runner connects the existing cashier
and Roulette Z positions; it shortens for the unchanged Level 3 layout. Its
open ends terminate beneath the existing counters/table. Gold edges/corners
frame the floor without using the atlas's incompatible large curved/junction
illustrations.

The whole atlas panels contain gutters and nonmatching motifs; blindly repeating
them would produce seams. The selected carpet joins average 2.67/255 RGB-channel
difference, and the runner's vertical join 3.27/255, comparable to their native
fabric variation. The live validator enforces each carpet/runner join below
4/255 and the captures were inspected for visible gaps or broken rails. No
replacement image is needed for this straight aisle and rectangular frame.
Future curved/junction routes would need separately authored, edge-matched tiles;
the unused illustrative panels should not be assumed interchangeable.

`BurgundyFloorIntegration.Run` imports these crops and saves only the floor
visuals in `Assets/Scenes/PitPanicPrototype.unity`. The existing campaign
presentation configures them consistently on all three levels. Nine added floor
objects contain only Transform, SpriteRenderer and texture binding, with no
colliders or gameplay components. Sorting is base -100, border -99, runner -98,
behind the existing wall (-95), furniture and actors. A serialized-block audit
confirms **all existing non-floor blocks byte-identical**: walls, furniture,
characters, UI, navigation, collisions and interaction points were not changed.
Only the old carpet Transform/renderer and floor-parent child list differ.

Evidence directory:
`/Users/rahulpala/.copilot/session-state/753e4550-8d41-464b-adaf-1a9686ebf1df/files/burgundy-floor/`

- `integration-final.log`: supplied artwork imported and actual scene saved.
- `play-mode-final-success.log`: three-level live movement, real customer
  navigation/seating, floor source/footprint/density/layering/aisle/seam checks,
  existing character/furniture checks and six 1920x1080 gameplay captures pass.
  Earlier attempts stopped at the synthetic-input assertion when the Editor
  lost focus; the harness now temporarily routes synthetic input to Game View
  and restores the original routing settings. Gameplay input code is unchanged.
- `level-1-live-floor.png` through `level-3-live-floor.png` and their
  `level-N-live-service.png` companions: actual Play Mode screenshots.
- `scene-and-source-audit.json`: unchanged source PNG and floor-only scene scope.
- `build.log`: successful latest macOS standalone, `Pit Panic.app`.

Reproduce the live check from the worktree with
`PIT_PANIC_ART_EVIDENCE=<evidence-directory> Unity -job-worker-count 2 -projectPath "$PWD" -executeMethod ApprovedArtPlayModeValidation.Run -logFile <log>`.
Build with
`PIT_PANIC_BUILD_PATH="<evidence-directory>/Pit Panic.app" Unity -batchmode -quit -projectPath "$PWD" -executeMethod PitPanicPresentationBuild.Run -logFile <build-log>`.
`Unity` here is
`/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity`.
The known pre-Play Editor Search indexing exception remains in the Editor log;
it is not a game script failure or a claim that the entire Editor log is clean.

### Supplied wall and break-room artwork

The latest playable scene replaces the earlier campaign-image top wall with
complete supplied modules from `PitPanic_Wall_And_BreakRoom_Assets`. The pack's
README and manifest were reviewed before installation. Only 13 wall/window/door/
trim PNGs are imported under `Assets/Resources/Art/BurgundyWalls`; all are
byte-identical to their source files. The optional sofa, palm, side table and
painting were deliberately **not** installed over the existing furniture.

The original tall layout and subsequent doorway/window arrangements are
superseded by the user's final direction: **no windows or Break Room doorway**.
Only five matching complete `Wainscot_WoodGold_A` panels remain visible as one
shallow burgundy-and-gold backdrop. All upper panels, pillars, separate trims,
windows and doorway sprites remain hidden and recoverable. The four floating
wall-side table/lamp sprites are also hidden; the actual sofa-side lamp, sofa and
all other furniture are unchanged. Staff rest destinations and behaviour remain
intact: removing doorway artwork does not remove a gameplay system.

These are complete modules, not a seamless tileset. Renderers are Simple/
FullRect with uniform scaling and point-filtered, uncompressed imports. The
five base panels use 7.6-world-unit spacing, matching visible widths while
overlapping only the supplied two-pixel transparent side padding. Source PNGs
are neither cropped nor repainted. Their common lower edge remains above 80%
of screen height (bottom-origin), leaving Roulette's base on carpet. Render
order -95 stays behind furniture and actors; the camera, floor and HUD did not
move.

`BurgundyWallIntegration.Run` imports and saves the real scene; its
`ApplyToCurrentScene` hook is also called by the existing targeted migration.
The campaign no longer re-enables or swaps the old backdrop on level changes.
The old backdrop, four windows and two legacy wall sconces are disabled, as
is the old break-room label Canvas. Already hidden curtains remain hidden.
The original art and objects remain recoverable.

For this layout correction, every serialized scene block outside the existing
wall-module SpriteRenderers/Transforms and the four wall-lamp enabled flags was
preserved byte-for-byte against the pre-correction scene. The audit found 18
changed blocks out of 971, with no objects or components added or removed.
Flooring, runner, chairs, other furniture, characters, labels, HUD, stations,
colliders, navigation, gameplay and rest destinations are unchanged.

Evidence directory:
`/Users/rahulpala/.copilot/session-state/753e4550-8d41-464b-adaf-1a9686ebf1df/files/wall-backdrop/`

- `integration-final.log`: successful installation on the actual playable scene.
- `play-mode-final.log`: three-level live movement, spawns and seating, five
  matching shallow panels, exclusion of windows/doorway/floating lamps, full
  image bounds, aspect, texture bindings and existing floor/character/furniture
  checks pass.
- `level-1-live-floor.png`, `level-2-live-floor.png`,
  `level-3-live-floor.png`: real 1920x1080 Play Mode overview captures;
  `level-3-live-service.png` provides a second Level 3 view after its ordinary
  nonblocking flavour dialogue has disappeared.
- `scene-audit.json`: final exact wall-only scene changes.
- `build-final.log`, `Pit Panic.app`: refreshed macOS standalone.

Reproduce installation with
`Unity -batchmode -quit -projectPath "$PWD" -executeMethod BurgundyWallIntegration.Run -logFile <log>`.
Live and standalone commands use the same entry points documented above, with
this evidence directory. The earlier full-shift regression is retained in
`burgundy-walls/full-shift.log`. The supplied pack is explicitly prototype
art: any native extraction fringe is retained rather than repainted, and it
does not provide arbitrary seamless wall lengths. The installed complete-module
arrangement needs no invented gap-filler artwork. The pre-Play Unity Editor
Search indexing exception remains separately disclosed; it is not a game-script
error or a claim that the entire Editor log is error-free.

Final validation/build commands (Unity 6000.3.26f1):

```sh
UNITY=/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity
EVIDENCE=/Users/rahulpala/.copilot/session-state/753e4550-8d41-464b-adaf-1a9686ebf1df/files/wall-backdrop
PIT_PANIC_ART_EVIDENCE="$EVIDENCE" "$UNITY" -job-worker-count 2 \
  -projectPath "$PWD" -executeMethod ApprovedArtPlayModeValidation.Run \
  -logFile "$EVIDENCE/play-mode-final.log"
PIT_PANIC_BUILD_PATH="$EVIDENCE/Pit Panic.app" "$UNITY" -batchmode -quit \
  -job-worker-count 2 -projectPath "$PWD" \
  -executeMethod PitPanicPresentationBuild.Run -logFile "$EVIDENCE/build-final.log"
```

### Roulette chairs around the betting layout

The four corner stools were replaced with three evenly spaced stools on the
near edge of the betting layout and one at the right end, farthest from the
wheel. The three near stools use the supplied rear-facing `CasinoStool_View05`;
the end stool uses the supplied left-facing `CasinoStool_View03`. Sprites remain
independent, point-filtered and uniformly scaled to the existing 1.3-unit height.
No new or modified artwork is used.

The four existing `CasinoSeat` objects retain their identities, references,
reservation/service behaviour and chair pull-out animation. Their approach,
sitting/facing and betting anchors move together so customers actually sit on
the relocated stools rather than at the old corners. In table-local coordinates,
the near sitting anchors are `(0,0,-2.05)`, `(1.25,0,-2.05)` and `(2.5,0,-2.05)`,
facing forward/away from the camera; the end anchor is `(3.35,0,-.35)`, facing
left. Approach anchors remain one unit outward from their sitting anchors.
The table, wheel, dealer, interaction collider and campaign table positions
are unchanged. The scene audit records only 20 Roulette seating Transform/
SpriteRenderer block changes, no additions/removals, and no unrelated changes
against the final panel-only wall scene.

`ApprovedCasinoArtIntegration.ArrangeRouletteChairs` is the targeted saved-scene
migration; the same helpers are used by the existing art-integration/generator
path. It does not rerun character, floor, wall or furniture installation.

Evidence:
`/Users/rahulpala/.copilot/session-state/753e4550-8d41-464b-adaf-1a9686ebf1df/files/roulette-chairs/`

- `integration.log`: targeted migration succeeded.
- `play-mode.log`: all three campaign layouts pass chair orientation, spacing,
  chair/sitting alignment and complete navigation-route checks. All four real
  Roulette seats were occupied by real spawner customers on Levels 1 and 3;
  Level 2 retains its normal opening VIP sequence. Existing art/floor/wall checks
  remain enabled. The pre-Play Editor Search indexing exception persists.
- `level-1-roulette-chairs.png`, `level-2-roulette-chairs.png`,
  `level-3-roulette-chairs.png`: actual Play Mode captures before test arrivals
  fill the chairs. `level-1-live-floor.png` and `level-3-live-floor.png` show
  live customers occupying the new layout.
- `scene-audit.json`: exact changed-block list.
- `build.log`, `Pit Panic.app`: fresh macOS standalone with this chair layout.

Reproduce the targeted migration with
`"$UNITY" -batchmode -quit -job-worker-count 2 -projectPath "$PWD" -executeMethod ApprovedCasinoArtIntegration.ArrangeRouletteChairs -logFile "$EVIDENCE/integration.log"`.
Use the live validation and build commands above with `EVIDENCE` set to this
directory; its final validation log is named `play-mode.log`.

### Dramatic final ten seconds

The latest playable build adds `CasinoShiftFinale` to the existing shift
controller at runtime. It uses the existing Silkscreen pixel font, gold text,
burgundy shadow and a nonblocking overlay for 10 through 1. Each number pops
and fades; the last three are slightly larger/warmer with a higher, stronger
tick. Two original synthesized cues extend the existing `CasinoSoundscape`
system. The global ticks remain audible even when a minigame owns audio focus.
There are no new environment graphics, imported-art edits or scene/layout changes.

The established nine-minute timer and pause rules are unchanged. Movement and
real task completion continue throughout the countdown. At zero, the existing
`FinishShift` guard closes the running shift immediately and snapshots its real
statistics exactly once, including existing unfinished-task rules. It uses the
same satisfaction, pass/fail, star and progression calculations as before.
It no longer waits for an interaction to close: that old grace period conflicts
with the explicitly requested immediate freeze.

`DealerAssignmentManager.SetPaused(true)` sets `Time.timeScale` to zero;
the shared `CasinoShiftController.GameplayFrozen` flag also stops unscaled UI/
visual updates and late/input work that ordinary time scaling would not stop.
Active minigame coroutines are stopped without abandoning or resolving their
tasks. Input routing, movement and minigame entry/completion are blocked. The
overlay intercepts pointer input and temporarily disables EventSystem keyboard
navigation, preventing an already selected task button from submitting after
zero. The world stays frozen throughout results; retry/progression reload the
scene using the existing routes.

At zero, TIME'S UP! plays a soft low impact, with a damped 0.3-second camera
shake of at most 0.055 world units. Its two-second hold and 0.45-second fade use
unscaled time, followed by the **existing** `CasinoResultsScreen` reveal. Its
original illustrations, music, explanation, totals and progression buttons are
retained. A second finish call has no effect; final task/tip/stat values are
captured before the hold rather than recalculated after it.

Evidence directory:
`/Users/rahulpala/.copilot/session-state/753e4550-8d41-464b-adaf-1a9686ebf1df/files/final-countdown/`

- `play-mode-final.log`: final visible Play Mode regression passed. The pass/fail
  fixtures seed the existing controller's task totals to isolate the outcome;
  the final-second fixture completes a real SHUFFLE through its actual stack
  inputs at **0.460 seconds remaining**, increasing tasks from 3 to 4 and passing.
  A separate real unfinished SHUFFLE stays frozen at zero with its timer/cards
  unchanged. Exact 10..1 sequence, tick audio (including urgent pitch), movement,
  pause/resume, input blocking, frozen actors/dealer energy/timers/arrivals,
  immutable final totals/stars/unlocks/credits and retry reset are asserted.
  Observed holds were approximately 1.93-2.02 seconds from test observation
  (the overlay itself uses exactly 2.0 unscaled seconds).
- `countdown-5.png`, `times-up.png`: actual 1920x1080 Play Mode screenshots,
  not arranged art scenes or pre-Play camera renders.
- `full-shift.log`: `FULL SHIFT INTERACTION TEST PASSED`, covering the existing
  tutorial, minigames, accounting, staff cover/rest, progression, failure/retry
  and missed-task mechanics. The old interaction-close assertions were narrowly
  updated for the intentional freeze/hold timing. The final targeted run above
  additionally covers the subsequent keyboard-navigation freeze hardening.
- `build.log`: macOS standalone `Build Finished, Result: Success.`
- `Pit Panic.app`, `player.log`: freshly built standalone opened and its exact
  process activated; a 1512x977 on-screen game window was verified. An already
  running older app was not terminated.

The pre-Play Unity Editor Search indexing `ArgumentOutOfRangeException` still
appears before the game-test listener starts. No game-script error was suppressed;
the standalone startup log has no game exception. The original source-art and
dirty baseline remain preserved except for the explicitly coupled freeze guards
and shift/test/documentation changes. No commit, merge or PR was made.

```sh
UNITY=/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity
EVIDENCE=/Users/rahulpala/.copilot/session-state/753e4550-8d41-464b-adaf-1a9686ebf1df/files/final-countdown
PIT_PANIC_COUNTDOWN_EVIDENCE="$EVIDENCE" "$UNITY" -job-worker-count 2 \
  -projectPath "$PWD" -executeMethod FinalCountdownPlayModeValidation.Run \
  -logFile "$EVIDENCE/play-mode-final.log"
"$UNITY" -batchmode -job-worker-count 2 -projectPath "$PWD" \
  -executeMethod FirstShiftPlayModeValidation.Run -logFile "$EVIDENCE/full-shift.log"
PIT_PANIC_BUILD_PATH="$EVIDENCE/Pit Panic.app" "$UNITY" -batchmode -quit \
  -job-worker-count 2 -projectPath "$PWD" \
  -executeMethod PitPanicPresentationBuild.Run -logFile "$EVIDENCE/build.log"
open -n "$EVIDENCE/Pit Panic.app" --args -screen-fullscreen 0 -logFile "$EVIDENCE/player.log"
```

### VIP golden outline glow

Approved-art VIPs now have a soft gold aura following the outside of their own
silhouette. It is derived from each existing VIP sprite's alpha, with four pixels
of padding and a distance falloff. Opaque body/shoe pixels are excluded from the
glow mask, preserving the corrected full-body artwork. No PNGs are altered.

Each VIP image's glow is cached and shared, rather than recalculated per frame
or customer. A child SpriteRenderer inherits the actor's scale, movement and
billboarding, uses the same material with its own URP texture binding, and draws
one sorting order behind the actor. A subtle alpha pulse does not move or stretch
the outline. Regular customers and staff have no glow; clearing VIP status hides it.
Legacy animated characters retain their existing aura path.

Evidence:
`/Users/rahulpala/.copilot/session-state/753e4550-8d41-464b-adaf-1a9686ebf1df/files/vip-gold-glow/`

`play-mode.log` passes all three real campaign layouts and all six VIP species,
including golden exterior-mask pixels, untouched opaque body/feet, alignment,
sorting, URP texture binding, regular-character exclusion and VIP off/on toggles.
Six `level-N-live-floor/service.png` images show the effect during actual Play
Mode with real customer agents. The known pre-Play Editor Search indexing
exception remains separately logged. `build.log` records the refreshed macOS
standalone build; `Pit Panic.app` is the latest build including the glow.

- The supplied standing sprites are static, forward-facing art. Walking,
  sitting, dealing and reactions retain their actual gameplay states but do not
  substitute legacy animated frames. New directional/action drawings were not
  invented.
- The floor Roulette wheel is baked into the approved illustration. Its live
  gameplay pivot, ball, round simulation and minigame remain; the duplicate
  legacy wheel layer is not drawn over it.
- Other supplied chair angles and decor files remain imported and available;
  the real floor uses the selected independent chair/stool views and existing
  scenery locations rather than displaying all 30 prop files as a gallery.
- Existing live labels, HUD and task markers can temporarily cover an actor;
  they were not hidden to stage screenshots.

No requested gameplay integration remains unimplemented. Both final test
commands exited successfully, preferences/input devices were restored, and
the protected source and carried recovery files were rechecked. Incidental
Unity UI Text max-size reserialization was restored to the scene baseline.

### Original three-pose directional Penguin walk cycle (superseded)

The supplied idle and two walking sheets were RGB images with a checkerboard
baked into the pixels and no alpha channel. Each 512x768 quadrant was extracted
in front/right/back/left order. An 8-connected edge matte removed the exterior
checkerboard while preserving enclosed pale pixels in the face and uniform.
All twelve resulting PNGs share a 363x587 transparent canvas, bottom-center
feet pivot, 256 PPU, point filtering, full-rect bounds and no mipmaps. No
artwork was resized or redrawn.

Only the real `Character_PenguinPixel` player prefab uses the assets. In each
facing direction, movement cycles through three distinct poses:
`step A -> directional idle -> step B -> directional idle -> repeat`.
Each beat holds for 0.2 seconds. Stopping immediately retains the matching
directional idle, and vertical settling does not change facing. The mapping is
S/front, D/right, W/back and A/left. The player keeps its existing
2.2-world-unit standing height and foot anchor. The Play Mode validation
measures source-alpha bounds against camera-projected bounds for all twelve
sprites; source and rendered ratios must match within 2%. Other species,
production movement physics, colliders, interaction points, and gameplay
animations are unchanged.

The original two-step-pose assets, which share the same canvas and pivot as the
idle art, are reused; no additional artwork was generated.

Evidence directory:
`/Users/rahulpala/.copilot/session-state/753e4550-8d41-464b-adaf-1a9686ebf1df/files/penguin-walk-three-pose/`

The updated Play Mode run verifies A/idle/B/idle/A transitions and each beat's
duration in all four directions, then checks stop-to-idle, projected aspect,
the real Pit Desk interaction and final-zero freeze. It captures the three
directional stop-idle poses, a zoomed appearance and the final freeze from the
live playable scene; the log records the observed walk-pose sequence and
durations. The fixture temporarily disables non-floor solid colliders for the
movement checks and restores them before interaction and freeze checks.
`play-mode.log` records each of the four observed cycles. `floor-*-idle.png`,
`zoomed-front-idle.png`, and `penguin-time-up.png` are Play Mode captures.
`build.log` records the successful macOS build, and the standalone app was
opened at:
`/Users/rahulpala/.copilot/session-state/753e4550-8d41-464b-adaf-1a9686ebf1df/files/penguin-walk-three-pose/Pit Panic Three Pose.app`

### Four-frame directional Penguin walk cycle

The playable Penguin uses the supplied 16 transparent PNGs unchanged. The
prefab maps S to south, D to east, W to north and A to west, playing frames
`01 -> 02 -> 03 -> 04 -> 01` at 180 ms per frame while moving. Stopping holds
frame `03` for the last facing: both feet are planted in that pose. Walking
again restarts the cycle at `01`.

The walk frames use full 256x256 bounds, a shared bottom-center pivot,
point filtering, and no compression or mipmaps. The existing uniform
sprite scaling and foot anchor are unchanged, preserving each frame's
aspect ratio and offsets within its original canvas. The title entrance
uses the same north-facing walk cycle and holds the matching planted pose
when stationary.

Run `PenguinDirectionalWalkPlayModeValidation.Run` in a visible Unity Editor
with `PIT_PANIC_PENGUIN_EVIDENCE` set to a capture folder. It checks the
saved prefab in Play Mode without re-importing or reassigning its sprites.

### Four-frame directional dealer walk cycles

Tiger, Pig and Bear each use their 16 supplied PNGs at the same 180 ms cadence as
Penguin. They face the direction of travel while walking and return to the
planted front-facing frame (south `03`) when stopped. Walking restarts at
frame `01`.
Dealer movement, assignments and rest behavior are unchanged. The live art
validation checks their prefab bindings, full sprite bounds and URP textures.

### Standing sprite edges and diagonal facing

The 17 approved standing PNGs retain their 48x64 canvases, pivots, and
import settings. Exterior pale matte and residual matte between the shoes
were cleaned in the assets, without editing the opaque face and uniform
details or replacing the VIP gold glow.

The Penguin still uses actual horizontal movement to advance the walk
animation. Facing uses the controller's directional input: while two axes
are held, it keeps the current facing if that direction is still held.
With no matching facing it chooses the vertical direction. Releasing one
key selects the remaining direction without changing movement or collisions.

Evidence: `files/character-edge-diagonal/` in this session. The
`standing-before.png` and `standing-after.png` comparisons use identical
burgundy backdrops. `diagonal-final.log` checks W+D in both key orders,
diagonal movement, and the transition to the remaining direction.

The main menu uses the playable Penguin's front idle pose and sizes its UI
image from the sprite's aspect ratio at a fixed height and foot anchor. After
New Game or Continue, it turns toward the doors and plays the same
four-frame north-facing cycle during the entrance walk.

## Employee Handbook

See `Docs/EmployeeHandbook.md` for the data-driven handbook (main menu / level select), its unlock rules, save keys and Play Mode validation.
