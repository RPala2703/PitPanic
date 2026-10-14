# Pit Panic visual direction

## Art direction

Pit Panic is a friendly, stylised animal casino with readable silhouettes and a warm, premium-playful finish. The art is original to this project and uses rounded proportions, clear facial features, and strong species shapes rather than realistic humans or copied character designs.

| Use | Colour |
| --- | --- |
| Table felt | Emerald `#0B6048` |
| Carpet and upholstery | Burgundy `#542333` |
| Wood | Walnut `#59351F` |
| Metal and trim | Warm brass `#D5A747` |
| UI panels | Deep green `#092C25` |
| UI copy | Warm ivory `#F5E8C8` |

Maintain the existing overhead camera, open walkways, and bright enough exposure to keep station objectives readable. Props and lighting must not obscure paths, character silhouettes, or attention indicators.

For gameplay-value clarity, the blue cash-desk float chip is £10; blackjack and roulette payout chips retain their existing £2.50 value so exact blackjack payouts remain representable.

## Prefab and regeneration policy

- `Assets/Prefabs/Characters` contains reusable animal character prefabs.
- `Assets/Prefabs/Casino` contains reusable station-art prefabs, including rounded custom-mesh table shells and a segmented 37-pocket roulette wheel.
- `Assets/Materials/PitPanic/Art` and `Assets/Art/Textures` contain reusable art materials and procedural prototype textures.
- `Assets/Scenes/PitPanicPrototype.unity` contains gameplay roots, colliders, navigation, interaction zones, and references. Render-only prefab children are grouped under `VisualRoot`.
- The scene generator creates only *missing* starter prefabs. It must not replace existing prefab assets; artists can replace or edit those assets without changing gameplay code.
- Gameplay anchors (dealer, customer, bet, interaction, attention, and navigation positions) stay separate from render-only meshes.

## Current asset status

The project currently has no licensed animal meshes, authored animation clips, custom font, or original audio recordings. The generated animal prefabs and casino props are an intentionally cohesive greybox art pass built from reusable custom meshes, mesh primitives, and project-authored materials; they are not presented as final bespoke character art. Each species has a distinct silhouette and face for prototype readability. The latest presentation pass uses a restrained burgundy carpet, lighter neutral fill lighting, brass-backed station signs, a compact task/staffing HUD, and shared minigame panels with activity-specific accent colours.

### Bespoke asset manifest

| Asset | Needed | Notes |
| --- | --- | --- |
| Animal character set | 10 game-ready low-poly models: penguin, tiger, pig, fox, rabbit, bear, dog, cat, raccoon, elephant | Consistent scale, separate head/body/accessory bones, clean silhouettes from the fixed camera, commercial-use rights |
| Character animation set | Idle, walk, turn, sit/stand, dealer work, celebrate, disappointed, impatient, rest, carry tray | Shared rig where practical; readable short loops |
| Casino station set | Blackjack table, roulette table/wheel, cashier counter, pit desk, break-room furniture | Separate render meshes from Unity gameplay anchors/colliders |
| Card and chip artwork | Four card suits/ranks and reversible card backs; four denomination chip faces | Original vector/texture source files and transparent exports |
| UI type and icon set | Display and body fonts, animal portraits, station/task icons | Include redistribution/commercial licence evidence |
| Audio set | Ambience/music, cards, chips, wheel, UI, steps, reactions, success/failure, delivery | Licensed or recorded originals, normalized and non-repetitive |
| VFX set | Card/chip trails, pocket highlight, task ping, payout/delivery feedback | Small, lightweight URP effects; avoid gameplay-obscuring bloom |

Record the source, creator, licence, and import settings for every third-party asset before adding it to the project. No external or unlicensed assets are bundled with the current prototype.
