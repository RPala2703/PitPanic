# Pit Panic — Wall & Break Room Asset Pack

22 individual **transparent PNG** assets, cut out from the approved concept sheet, plus the original image reference and a manifest.

**Includes:** 3 upper-wall sections, 3 lower wood-panel sections, the complete Break Room doorway (sign and lamps baked into it), 2 curtained night windows, 2 wall sconces, 1 framed painting, 1 pillar, sofa, side table with lamp, plant, and 6 trim/border pieces.

## Important technical notes

- These are **separate PNG object sprites**. The source is a generated concept sheet, **not a validated tilemap**.
- **Do not assume** any wall/trim pieces tile seamlessly, or stretch the framed panels arbitrarily. Arrange full modules or ask for additional pieces if the layout needs precise repeatable modules.
- Edges were extracted using transparency masks and lightly cleaned. Inspect at actual game zoom; some AI-generated fringe and alignment may still need hand cleanup.
- Original art is preserved at its native resolution. Unity sprites should use Point filtering, no compression, and consistent world-space sizing; native PNG pixel dimensions are not their intended dimensions in world units.
- Keep existing working gameplay, collision, table positions, animations, and HUD intact.
- Break Room doorway is a visual sprite; its door interaction/collider must remain connected to existing systems. Sofa is a separate PNG.
- Existing curtained windows may already be used in the game; replace only where it improves visuals and do not stack duplicates.
- These are prototype assets to evaluate in Play Mode, **not verified production-ready seamless tiles**.
