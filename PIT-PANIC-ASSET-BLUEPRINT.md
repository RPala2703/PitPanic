# Pit Panic External Art Production Blueprint

Status: planning document only. No artwork, scene, prefab, gameplay code, or project settings were changed to produce this blueprint. Do not run either art generator or the prototype scene generator while replacement art is being planned.

## 1. Scope and protected work

The three campaign levels are **Opening Night**, **High Rollers**, and **Shady Business**. They share `Assets/Scenes/PitPanicPrototype.unity`; `CasinoCampaignPresentation.Apply(level)` selects the level floor art, layout positions, tints, and lighting at runtime. There are not three separate hand-authored level scenes to replace.

Preserve all gameplay, staffing, customer behavior, minigames, dialogue, saves, scoring, results, and progression. The supplied dialogue, title, and pass/fail artwork are not part of this replacement-art task. Do not edit them without a separate approval. This blueprint does not perform the requested rollback; it records how to do it safely if approved.

## 2. Recent visual-pass change inventory

The current branch is `three-level-campaign`, at `b865019` (`Polish Pit Panic campaign presentation`); PR #48 is open. The following are the exact categories of art-pass changes currently present as uncommitted changes:

| Changed files | Count | Change |
|---|---:|---|
| `Assets/Scripts/Editor/PitPanicRichPixelArt.cs` | 1 | Generated sprite artwork: animal silhouettes/details, uniform embroidery, faces, table and station imagery, props, floor, and rug. |
| `Assets/Scripts/Editor/PitPanicOriginalFloorArt.cs` | 1 | Generated the three campaign floors with distinct dark carpet, wall-band, runway, and motif treatments. |
| `Assets/Scripts/UI/CasinoPixelArt.cs` | 1 | Runtime-drawn casino salon backdrop and pixel-art card/chip/effect imagery; includes a sprite sizing overload used to preserve backdrop proportions. |
| `Assets/Scripts/Minigames/CasinoMinigamePresentation.cs` | 1 | Shared minigame backdrop/shade presentation. |
| `Assets/Scripts/Blackjack/BlackjackTableView.cs` | 1 | Blackjack interaction backdrop/shade visibility follows the panel state. |
| `Assets/Scripts/Roulette/RouletteInspectionView.cs` | 1 | Roulette interaction backdrop/shade visibility follows the panel state. |
| `Assets/Art/Pixel/*.png` | 181 | Generated output images: 33 animation atlases (11 base, 11 customer, 11 VIP), 132 character frames, 6 station sprites (`Blackjack`, `BlackjackV2`, `BreakRoom`, `CashDesk`, `PitDesk`, `Roulette`), 8 structure sprites (`Arch`, `Chair`, `Column`, `Curtain`, `Lamp`, `Plant`, `Sofa`, `Window`), `CasinoFloor.png`, and `TableRug.png`. The roulette-wheel PNG is unchanged. |
| `Assets/Resources/Art/Original/World/*.png` | 3 | `OpeningNightFloor.png`, `HighRollersFloor.png`, and `ShadyBusinessFloor.png`. |

No scene, prefab, gameplay/progression source, dialogue/result asset, or texture `.meta` file is in that inventory. The six source-file changes are presentation or art generation; do not revert gameplay systems to roll back their visuals.

Separate worktree changes are unrelated Unity/editor configuration and must be preserved: `.vscode/settings.json`; `Assets/Settings/DefaultVolumeProfile.asset`, `PC_RPAsset.asset`, and `UniversalRenderPipelineGlobalSettings.asset`; `ProjectSettings/ProjectSettings.asset`; and untracked `ProjectSettings/SceneTemplateSettings.json`. The latter settings were recorded as pre-existing to this visual audit.

### Safe rollback plan (not executed)

1. Make an external backup of the worktree and capture `git status --short` plus `git diff --binary` before touching anything. Review the current paths again at rollback time; new art may have arrived since this inventory.
2. Do not reset the branch, revert a commit, regenerate art, or run the scene generator. Do not touch the unrelated settings above.
3. If rollback is approved, restore only the identified art-pass PNGs and the six visual-only source changes to the `b865019` baseline, using the then-current reviewed path list. Existing `.meta` files are unchanged, so leave importer GUIDs/settings intact.
4. Inspect the resulting diff and verify only those art outputs and visual-presentation hunks were removed; confirm scenes, prefabs, gameplay, UI dialogue/results, and settings are unchanged. If any of the listed files acquired later unrelated edits, restore only reviewed visual hunks rather than the whole file.

The rollback is intentionally not performed as part of this deliverable: it would remove the current art-pass work before replacement art is ready.

## 3. Existing visual-system audit

| Visual family | Current implementation and controlling surface | Replacement boundary |
|---|---|---|
| Level floor | `PitPanicPrototypeGenerator.CreateFloor` creates a floor collider and a `SpriteRenderer` surface. `CasinoCampaignPresentation.Apply` selects one of three floor sprites. Current floor artwork is a single sprite, not a Tilemap. | Replace only the visual sprite/renderer or its source art. Preserve the floor collider, dimensions, floor bounds, and NavMesh. |
| Walls and room dressing | `PitPanicPrototypeGenerator.CreateRichScenery` places window, lamp, curtain, sofa, and plant sprites from `Assets/Art/Pixel/Structure_*.png`. Optional KayKit decorative prefabs are placed outside the walkable area. | Replace renderers/sprite references. Do not move scenery into walkable space or remove optional-prefab guards. |
| Tables and stations | `PitPanicArtPrefabLibrary` builds primitive/mesh art prefabs. The scene generator instantiates them under a `VisualRoot`; visible mesh renderers are disabled for the pixel-surface overlay. `CreateBlackjackTable`, `CreateRouletteTable`, `CreateCashierStation`, `CreatePitDesk`, and `CreateBreakRoom` add separate gameplay components and interaction bounds. | Swap visual children only. Retain the root, interaction bounds/colliders, NavMesh modifiers, seats, bet positions, dealer stations, destinations, attention, and minigame components. |
| Character bodies | Character prefabs contain primitive mesh rigs, but the mesh renderers are disabled and their visible child is a `SpriteRenderer`. `PixelCharacterVisual` slices the animation texture and drives poses/directions. `PixelBillboard` faces sprites toward the camera. | Replace sprite sources/prefab visual child only. Keep character roots, rigs/components, colliders, navigation, seat occupancy, animation state, and world positions. |
| Character animation | `PitPanicRichPixelArt.GenerateCharacters` writes per-species animation atlases. Current atlas is 2880×4096: 48×64 cells, 60 columns, 64 rows (16 poses × 4 directions). Atlas import is readable, Point-filtered, uncompressed, no mipmaps, max size 4096; runtime frame PPU is 30. | Match cell size, grid, pose order, direction order, and frame slots until an explicit code change is approved. |
| Chairs and small world props | Prefab-library chair/furniture art and generated chair sprites coexist. `PixelWorldProps` creates cards, chips, and float trays at runtime. | Supply separate sprite art while retaining prop transforms, tray/chip ownership, and world interaction points. |
| UI | Unity UGUI `Image`, `Text`, and `Button` objects are assembled in code. `CasinoHudStyle` supplies shared panel/button treatment; `CasinoPixelArt` procedurally draws casino icons, chips, cards, and backdrops. Minigame UI uses `CasinoMinigamePresentation`. | Replace image/sprite references or shared skin assets. Keep Canvas hierarchy, anchors, `RectTransform`s, button callbacks, input actions, and minigame state. |
| Minigames | Blackjack, roulette inspection/spin/cover, shuffle, and cash-desk pack/cash-out interfaces are built by their respective runtime scripts and shared UI helpers. | Skin existing controls and backgrounds; do not change game rules, control flow, timing, scoring, or payout behavior. |
| HUD and in-world indicators | Management HUD, station prompts, attention/task badges, speech bubbles, patience/energy indicators, tip popups, and VIP effects are created by UI/world-space code. | Supply icon/panel sprites and restrained effect sprites; preserve text, task state, sorting, event triggers, and interaction ranges. |
| Lighting and visual effects | The generator adds directional and point lights. `CasinoAmbientGlow` pulses lamp sprite tint; `CasinoPerformanceDirector` animates tip, chip, sparkle, and popup effects. Some VFX sprites are runtime-generated. | Adjust only with approved art. Preserve light objects needed by gameplay readability, animation timing, and VFX triggers. |
| Title, dialogue, results | Existing title-screen, portrait/dialogue, and six pass/fail results assets/UI are separate existing surfaces. | Protected: leave untouched in this rebuild. |
| Tilemaps | No Tilemap or TilemapRenderer was found. | Do not convert the playable floor to a Tilemap merely to import new art. Optional non-colliding decorative tile layers would be a later, separately approved implementation choice. |

### Generator and prefab entry points

- `Assets/Scripts/Editor/PitPanicPrototypeGenerator.cs`: scene-generation entry point and the floor, stations, scenery, lighting, and camera setup. **It can rewrite `PitPanicPrototype.unity`; do not run it during this art-only preparation.**
- `Assets/Scripts/Editor/PitPanicRichPixelArt.cs`: writes generated character, station, prop, floor, and wheel sprites under `Assets/Art/Pixel/`.
- `Assets/Scripts/Editor/PitPanicOriginalFloorArt.cs`: writes the three campaign floors under `Assets/Resources/Art/Original/World/`.
- `Assets/Scripts/Editor/PitPanicArtPrefabLibrary.cs`: builds character, table, cash-desk, pit-desk, break-room, float-tray, and column prefab visuals.
- `Assets/Scripts/Customers/CasinoCampaignPresentation.cs`: chooses level layout, floor art, tints, and light settings at runtime.
- `Assets/Scripts/Characters/PixelCharacterVisual.cs` and `PixelWorldProps.cs`: consume animation frames and create world props.
- `Assets/Scripts/UI/CasinoPixelArt.cs`, `CasinoHudStyle.cs`, and `Minigames/CasinoMinigamePresentation.cs`: runtime UI art, skin, and common layout helpers.

## 4. Shared art standard (proposed)

This is a target specification for newly created art, not a claim about the current assets.

| Property | Proposed standard |
|---|---|
| Mood and palette | Warm, colourful, charming luxury: cream/ivory highlights, walnut and warm rose/burgundy, jade/emerald felt, restrained brass/gold, and readable species-specific fur colours. Keep floor contrast medium-light. Avoid horror-dark values, harsh neon, noisy micro-patterns, and low-contrast text. |
| Pixel density | Use **32 pixels per Unity world unit** for new world-art files. This is a proposed harmonisation target; current assets use several PPUs (notably 16 for campaign floors, 30 for character frames, 32 for station sprites). Update visual local scales deliberately during integration to preserve current world-space bounds. |
| Camera and perspective | Keep the existing fixed orthographic, three-quarter casino view and screen-facing character billboards. Draw world sprites for the same overhead/three-quarter view; do not mix side-on character views or a diamond/isometric map projection. Keep table tops, floor axes, and silhouettes readable. |
| Outlines and shading | Consistent 1–2 px dark-warm outline at the 32-PPU master scale; selective interior linework. Use a compact 3–4 tone ramp per material, with a warm key-light highlight from upper-left and a restrained lower/right shadow. Keep faces readable at gameplay size. |
| World grid | 32×32 px ground tile = 1×1 Unity unit at 32 PPU. Use clean tile edges, broad low-frequency floor motifs, and no distracting repeating high-contrast carpet pattern. Existing walkable bounds remain authoritative. |
| Character scale | Standard frame 48×64 px at 32 PPU (about 1.5×2 world units), bottom-centre pivot; preserve the current 48×64 cell contract. Character identity must read from silhouette, face, outfit, and palette at normal camera distance. |
| World sprite imports | PNG RGBA, Sprite (2D and UI), Point filtering, no mipmaps, no compression, no unintended alpha fringe. Atlas cells must have exact pixel boundaries and transparent padding consistent across a family. Keep stable `.meta` GUIDs when replacing an existing source file. |
| UI scale | Design at a 1600×900 reference CanvasScaler resolution. Use Point filtering and integer-like scale steps; build panel/button sprites with 9-slice borders where they must resize. Use high contrast, generous padding, and minimum legible labels. |
| Animation | Preserve a stable canvas and pivot across every frame. No frame-to-frame camera, scale, or baseline drift. Reuse shared timing and pose names; effects must not obscure faces, task indicators, or control labels. |

## 5. External asset manifest

All dimensions and import settings in this section are proposed production targets. They describe deliverables for later approval/integration, not files created here. Unless noted otherwise, world images are transparent PNG sprites at 32 PPU with Point filtering, no mipmaps, and no compression. Pivots are expressed as normalized coordinates from the bottom-left; sorting values refer to the current approximate world ordering and must be confirmed against the live scene after import.

### Environment and reusable world art

| Asset family / suggested filenames | Purpose and target source size | Pivot, sorting, frames | Use and reuse |
|---|---|---|---|
| `World/Floor_OpeningNight.png`, `Floor_HighRollers.png`, `Floor_ShadyBusiness.png` | Full rectangular, opaque floor/background, 1024×704 px (32×22 world units at 32 PPU). Include broad floor zones and unobtrusive edge trim; avoid baking tables or gameplay markers into the floor. | Centre `(0.5, 0.5)`; background order about `-100`; static. | One unique floor per level, selected by `CasinoCampaignPresentation`; dimensions must match floor bounds. |
| `World/Tiles_Floor_32.png` | Optional reusable 32×32 px source tiles: clean field, border/edge, corner, door threshold, and a few subtle carpet variants. Keep source art tileable. | Tile bottom-left `(0,0)` anchor or Unity Tile pivot `(0.5,0.5)`; non-colliding background order `-100`; static. | Reusable in all levels only if a later tile-layer approach is approved. Current scene has no Tilemap; full floor images are the compatible path. |
| `World/WallPanel_32x64.png`, `WallEdge_32.png`, `Door_128.png` | 32×64 px wall modules and 128×128 px doorway/entrance art with matching trim. | Bottom-centre for wall/door; background behind characters (approx. `-80`); static. | Reuse structures across levels; vary accent colour/props only where approved. Preserve entrance and boundary logic. |
| `World/Window_64x96.png`, `Curtain_64x128.png`, `Lamp_64x96.png`, `Plant_64x96.png`, `Sofa_128x64.png`, `Column_64x128.png` | Transparent scenery sprites at the named dimensions; sizes can be doubled in source while retaining the same pixel geometry. Keep silhouettes uncluttered. | Bottom-centre `(0.5,0)`; scenery behind characters (approx. `-5`); static; lamp may have a separate glow sprite. | Reusable across all three levels. Current placements are visual-only and outside/around walkable areas. |
| `World/Shadow_Soft_64.png` | 64×32 px soft pixel-compatible oval shadow; optional, restrained alpha. | Centre; below characters/props (approx. `-20`); static. | Reusable for characters and furniture if the replacement art needs grounded silhouettes; do not add large dark overlays. |

### Tables, service stations, furniture, and casino props

| Asset family / suggested filenames | Purpose and target source size | Pivot, sorting, frames | Use and reuse |
|---|---|---|---|
| `Stations/BlackjackTable.png` | 192×96 px transparent table surface/shape; include felt, rail, card slots and clear dealer/player sides, but no cards that need independent animation. | Centre; table surface approx. `-10`, ground rug/decal approx. `-90`; static. | Reuse in all levels at the existing blackjack table transform. Preserve table collider, seat positions, dealer point, and refill/task bounds. |
| `Stations/RouletteTable.png`, `RouletteWheel_Base.png` | Table 192×96 px; wheel 64×64 px source sprite with a separately rotatable wheel/ball region. Separate non-rotating table from animated wheel. | Table centre; wheel pivot exact centre `(0.5,0.5)`, visible above table; wheel frame(s) static art with rotation handled by existing runtime. | Reuse across levels. Keep wheel transform, spin axis, betting positions, and result logic. |
| `Stations/CashDesk.png`, `PitDesk.png` | Each about 192×96 px transparent art at matching current station bounds; counter front/top must read at the existing camera. | Centre/bottom-centre as required by the existing root; station art approx. `-10`; static. | Reuse across levels; retain gameplay bounds and customer/dealer waypoints. |
| `Stations/BreakRoom.png` | About 192×128 px transparent room vignette with counter/seat grouping. | Bottom-centre; scenery behind seated characters, approx. `-5`; static. | Reuse the same room and four rest positions in all levels. |
| `Furniture/Chair_Dealer.png`, `Chair_Customer.png`, `Stool.png` | About 64×96 px each; distinguish dealer chair, customer seat, and roulette/table stool. | Bottom-centre; world-prop order near `-5`; static (chair pull-out remains transform-driven). | Reuse at all tables and desks; preserve `CasinoSeatVisual` chair reference and seat logic. |
| `Props/DealerTray.png`, `ChipTray.png`, `CardShoe.png`, `CashFloatTray.png` | 64×32–96×64 px, individual transparent props with enough contrast from felt/table. | Bottom-centre or tabletop centre; above the table surface; static. | Reuse at appropriate stations. Keep tray/float destinations and chip objects as gameplay-owned transforms. |
| `Props/Card_Back.png`, `Card_Hearts_A.png` … `Card_Spades_K.png` | Card back and 52 faces, each 24×32 px (or 2× source export), consistent rank/suit marks. | Centre; tabletop order above the table art; static source sprites. | Shared across blackjack/roulette where applicable. Keep actual card values, dealing, and UI rendering logic independent of artwork. |
| `Props/Chip_<denomination>.png` | One 32×32 px top-view chip for each denomination currently used; distinct edge stripe and centre mark. | Centre; tabletop/VFX order above table; static. | Shared world and UI chips. Preserve denomination-to-color mapping and amount values. |
| `Props/ChipStack_*.png`, `BetMarker_*.png`, `RouletteMarker_*.png` | 32×32 to 64×64 px denominations/stack silhouettes and table markers. | Centre; above felt; static. | Reusable across roulette, blackjack, float display, and cash desk. Do not bake variable counts or values into the table art. |

### Characters and animation

The current species roster is Penguin, Tiger, Pig, Fox, Rabbit, Bear, Dog, Cat, Raccoon, Elephant, and Deer. Penguin is the player; Tiger, Pig, Fox, and Bear are the four dealers. Customer and VIP roles use the broader roster.

| Asset family / suggested filenames | Purpose and target source size | Pivot, sorting, frames | Use and reuse |
|---|---|---|---|
| `Characters/Animation_<Species>.png` | One 2880×4096 px atlas per species: 48×64 px frame cells, 60 columns × 64 rows. Match current row order exactly: 16 `CasinoAnimationPose` values, each with 4 directions; 60 frame slots per pose/direction. Target PPU 32. | Every frame bottom-centre `(0.5,0)`; character order around default character sorting (validate overlap by world depth); looping/action sequences. | Player Penguin and dealer species use this common base family. Keep every cell canvas, baseline, facing, and timing stable. |
| `Characters/Animation_Customer_<Species>.png` | Same atlas size/layout and frame/Pivot contract, with customer clothing/palette. | As above; 16 poses × 4 directions × 60 slots. | Reusable in all levels for the customer roster. |
| `Characters/Animation_VIP_<Species>.png` | Same atlas size/layout with visually distinct but restrained luxury clothing/accessories and readable species identity. | As above; retain gold VIP glow as a separate effect, not baked into every frame. | Reusable VIP roster. Preserve the existing VIP classification/effect logic. |
| `Characters/Character_<Species>_<Direction>_<Expression>.png` | 48×64 px compatibility/fallback stills; four directions and any currently consumed expressions. | Bottom-centre; same layer/order as animated character; static. | Optional only if a runtime fallback/portrait uses these paths. Confirm callers before retiring. |
| `Characters/DealerUniform_Details.png` (optional source guide only) | Not a runtime sprite; a shared outfit consistency sheet if external production needs it. | Not applicable. | Keep four dealer uniforms related; do not replace dialogue portrait art with this sheet. |

Keep the existing pose identifiers/order: `Idle`, `Walk`, `Deal`, `Collect`, `Pay`, `Celebrate`, `Disappointed`, `Tired`, `Rest`, `Carry`, `Sit`, `Examine`, `Shuffle`, `SeatedIdle`, `Anticipate`, `Present`. The current animation code indexes 60 frame slots for each pose and direction. An artist need not invent 60 distinct drawings for every action: use sensible held/repeated frames where motion is naturally limited, but deliver the full compatible cell grid unless the animation importer is changed and separately tested.

### HUD, minigame, and interaction UI

| Asset family / suggested filenames | Purpose and target source size | Pivot, sorting, frames | Use and reuse |
|---|---|---|---|
| `UI/Panel_Main_9Slice.png`, `Panel_Inset_9Slice.png`, `Tooltip_9Slice.png` | 512×256 px, 256×128 px, and 256×96 px transparent pixel panels. Define 9-slice borders (roughly 24 px) that remain clean when stretched. | Centre; UI Canvas hierarchy order; static. | Shared HUD and minigame panels, not results/dialogue panels. |
| `UI/Button_Primary_9Slice.png`, `Button_Secondary_9Slice.png`, `Button_Disabled_9Slice.png` | 256×64 px each; consistent cap/border size and legible text-safe centre. | Centre; Canvas button layers; 3 visual states. | Shared by gameplay controls and minigames. Button labels/callbacks remain runtime text and code. |
| `UI/Icon_*.png` | Individual 32×32 or 64×64 px icons: blackjack, roulette, cash desk, pit desk, task, customer, VIP, coin/tip, warning, energy, patience, and interaction. | Centre; UI Canvas; static. | Shared management HUD, task badges, prompts, and menus; do not include state text in the icon. |
| `UI/Blackjack_*.png` | Blackjack table felt/board, card backs/faces and control decoration; 512×256 px board art plus separate cards/icons as needed. | UI anchors managed by existing Canvas; static except card UI state. | Blackjack inspection/interaction and shuffle minigame. Do not bake totals, outcomes, or rules into images. |
| `UI/Roulette_*.png` | Roulette table board, betting-grid background and wheel decoration; 512×256 px board, with wheel art separated. | Board centre; wheel centre pivot for runtime spin; static art. | Roulette inspection, cover, spin, and sweep/payout presentations. |
| `UI/CashDesk_*.png`, `UI/PitDesk_*.png` | Pack/cash-out and management/CCTV panel accents, each 512×256 px or modular components. | Canvas anchors; static artwork. | Cash desk and pit desk interfaces. Retain live values, task instructions, and controls as text/buttons. |
| `UI/Prompt_Key_*.png`, `UI/Indicator_*.png` | 32×32–64×64 px keycap/prompt, station task marker, attention badge, and progress/patience/energy frame. | Centre; above world UI and HUD; static or simple 2–4-frame indicator. | Interaction prompts, task readiness, speech/task badges, patience and energy meters. Keep current input and state logic. |
| `UI/Panel_*.png` for minigames | Modular panel and decorative dividers at the reference 1600×900 layout; pixel borders should scale with 9-slicing. | Canvas order controlled by existing views; static. | Reuse for all gameplay minigames. Exclude title, dialogue, and pass/fail/results art from this family. |

### Level-specific dressing, lighting, and effects

| Asset family / suggested filenames | Purpose and target source size | Pivot, sorting, frames | Use and reuse |
|---|---|---|---|
| `World/OpeningNight_Accent_*.png`, `HighRollers_Accent_*.png`, `ShadyBusiness_Accent_*.png` | A small set of optional wall banners, window trim, lampshades, and potted accents, typically 32×64 to 128×128 px. Keep level identity in a few accents rather than a different visual language. | Bottom-centre; behind characters, approx. `-5`; static. | Level-specific variants; all core tables, characters, stations, props, and UI remain shared. |
| `FX/Shadow_Soft_*.png`, `FX/Glow_Warm_*.png` | 32–128 px soft pixel-edged ground shadows and subtle warm glow masks with transparent alpha. | Centre; beneath the associated object or behind its sprite; static textures with existing code-driven tint/pulse. | Reuse across all levels. Keep lighting subtle and do not replace actual `Light` components if they are still needed. |
| `FX/Sparkle_*.png`, `Tip_Chip_Flight.png`, `Attention_Burst.png` | 16–64 px sparkle, tip/chip, and attention sprites; 2–4 frames for a small loop if needed. | Centre; existing VFX sorting orders (sparkles/popups above characters); animated by existing effect code. | Shared across levels and VIP/tip/task effects. No new particles or gameplay triggers in this art phase. |

## 6. Three-level reuse plan

| Shared across all levels | Level-specific |
|---|---|
| Penguin player, four dealers, customer/VIP species and animation families; blackjack and roulette tables; chairs; cash desk; pit desk; break room; card/chip/float props; common wall/window/plant/lamp set; minigame UI, HUD, icons, prompts, and restrained effects. | Three floor illustrations and a small set of coordinated accent/decoration variants for Opening Night, High Rollers, and Shady Business. |

Keep visual variations as palette/accessory variants or separate decoration sprites; do not fork gameplay prefabs or minigame interfaces per level.

## 7. Import and integration plan (later, after external art is approved)

1. Receive approved, original PNG source files with the filenames/cell layouts in this manifest. Keep dialogue, title, and results artwork in its current location and untouched.
2. Before importing, snapshot the worktree and preserve the current source artwork. Replace only intended visual asset paths; preserve `.meta` files/GUIDs wherever an existing asset is replaced in place.
3. Set world sprites to Sprite (2D and UI), Point filter, no mipmaps, no compression, appropriate max texture size, and the agreed 32 PPU target. Set animation atlases readable with max size 4096. Set UI images to sliced mode only for panels/buttons designed for 9-slice.
4. Update visual prefab children/source references—not scene roots or gameplay components. Keep colliders, `TableInteractionZone`, NavMesh modifiers, seat/dealer transforms, task/attention components, `PixelCharacterVisual`, and input/UI callback wiring intact.
5. Keep the existing world extents, camera, scale and sort relationships. Replace the three floor sprite selections through `CasinoCampaignPresentation`; retain colliders/NavMesh and all level layout positions.
6. Do not use the scene generator for asset iteration: it calls the art generators and rewrites `PitPanicPrototype.unity`. Add a non-destructive asset-reference path or a narrowly scoped preview/validation method first, then regenerate only with explicit approval and a clean, backed-up scene.
7. Validate imports and object references; compare every level at gameplay camera scale; verify 16:9 and alternate aspect ratios for UI; check sprites do not cover faces or task markers.
8. Run Unity compilation and the existing gameplay/campaign validations, then test all three levels, minigames, customer/staff movement and animations, save/progression, and results. Any failure must be fixed in the visual integration without changing game rules.

## 8. Verification record for this document

- Repository status was inspected and the file categories above were counted.
- Source inspection confirmed the shared scene, runtime campaign presentation, generator/prefab boundaries, 48×64 character cells, 60-frame atlas layout, 16 poses, and the absence of Tilemaps.
- No Unity generator, scene generator, build, or gameplay test was run for this documentation-only change.
- No art, scene, prefab, gameplay, or project-setting files were changed for this blueprint.
