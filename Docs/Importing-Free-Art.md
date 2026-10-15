# Importing free art packs

Menu: **Pit Panic → Import Free Art Assets**.

Supported packs (download manually; none are committed to this repository):

| Pack | Expected file |
| --- | --- |
| Casino FREE (ITHappy Studios) | `.unitypackage` containing "casino" and "free" |
| Quaternius Universal Base Characters | `.unitypackage` or `.zip` containing "universal", "base", "character" |
| KayKit Furniture Bits | `.zip`/`.unitypackage` containing "furniture" and "bits" |
| KayKit Character Animations | `.zip`/`.unitypackage` containing "character" and "animation" |

The tool searches `~/Downloads`, `<project>/ArtSource` and one folder you choose. It matches on keywords, not exact filenames.

- `.unitypackage` files use Unity's package importer; `.zip` files are extracted to `Assets/ThirdParty/<Pack>` without overwriting existing files.
- Imports are recorded by SHA-256 hash in `UserSettings`, so the same package is not imported twice.
- Optional: set imported character models to Humanoid rigs.
- Non-URP materials are reported and URP copies are created in `Assets/Art/ImportedMaterialsURP`; originals are untouched.
- The summary lists counts only, not asset names, so NoAI-tagged content is never written to logs.
- `Assets/ThirdParty`, `ArtSource` and `*.unitypackage` are git-ignored. Check each licence before changing that.

Status: the importer exists, but the pack visuals are **not yet wired into the scene generator**; that needs the imported prefabs to be chosen in the Editor.
