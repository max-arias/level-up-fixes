# LevelUpChoicesFixes

A focused compatibility add-on for `karaeren.LevelUpChoices` 1.1.3. It leaves the upstream level-up manager, UI, artifact, XP hook, item-grant path, and network messages in place and adds only validated Harmony integrations.

## Release order

The settings are grouped in the intended testable release order:

1. **Correctness** — Item Qualities promotion, canonical no-duplicate rerolls, pause teardown recovery, and item blacklist.
2. **Tokens and schedule** — level-based reroll refresh and choices every N levels.
3. **Interactables** — the upstream `Remove Chests & Interactables` setting remains the all-sources switch; this package adds per-category keep controls, credit scaling, and an opt-in exception for Item Qualities chests.
4. **XP** — optional exponential or linear XP curve controls, applied by replacing the original hook's generated table rather than adding another XP hook.

The all-sources switch is owned by LevelUpChoices. Set its `Remove Chests & Interactables` option to `false` to keep normal sources; the per-category `Keep ...` settings only refine behavior when that upstream switch is `true`.

## Item Qualities

Item Qualities is a soft runtime integration. If its plugin or validated `QualityCatalog` API is absent, only this add-on's quality promotion is disabled and normal LevelUpChoices behavior continues.

The host's settings are authoritative and are synchronized to clients at run start. Initial quality defaults are:

- Enable Quality Integration: `true`
- Quality Chance: `4%`
- Uncommon / Rare / Epic / Legendary weights: `70 / 20 / 8 / 2`

Weights are relative and normalized over variants that actually exist for the selected base item. A missing variant is never substituted with an ordinary item. The original LevelUpChoices roll still performs tier, luck, similarity, and exclusion selection first; this package promotes the resulting base item afterward. World-unique quality variants are removed from each original player drop table, so variants cannot compete as ordinary choices.

Base identity is retained for similarity, rerolls, exclusions, and banishes. The final quality `ItemIndex` remains in LevelUpChoices' existing option list, so its normal pickup, grant, and `SyncItems` paths are used.
When Item Qualities is installed, its `iscQualityChest1`, `iscQualityChest2`, `iscQualityDuplicator`, `iscQualityDuplicatorLarge`, `iscQualityDuplicatorMilitary`, and `iscQualityDuplicatorWild` cards are treated as item sources by the upstream removal hook. `Allow Quality Chests` defaults to `false`, so quality chests and quality printers are removed with the other item-giving interactables. Set it to `true` only if those variants should remain.

By default, every 5th level queues a guaranteed-quality choice set. The next generated set contains 3 quality choices; their base item rarity is still rolled from the current LevelUpChoices token progression, and their quality tier is sampled from the configured quality weights. A queued milestone waits if an earlier choice set remains unspent.

## Risk of Options

Risk of Options is an optional integration. When installed, the supported live-safe settings appear in its Mod Options screen under Quality, Schedule, and Interactables. Native BepInEx configuration remains available when Risk of Options is absent. In multiplayer, host/server settings are disabled for clients and host changes synchronize to connected clients during a run.

## Configuration

All settings are BepInEx server settings in the `Server` section. The item blacklist is a comma-separated list of `ItemDef.name` values and defaults to `DefensiveMicrobots`. Reroll refresh can be disabled, reset to the upstream starting count, or increment by one at a configured interval. `Item Choices Every N Levels` preserves unspent choices and grants new choices only at the selected interval.

Quality compensation defaults to `Guaranteed Quality Every N Levels = 5` and `Guaranteed Quality Choice Count = 3`. Set the interval to `0` to disable guaranteed quality sets. The choice count is capped by the upstream LevelUpChoices option count. The normal Quality Chance remains `4%`; guaranteed sets bypass that roll but retain random quality-tier selection. Base item rarity remains controlled by the normal LevelUpChoices token-weight curve, so higher base rarities become more likely as `UsedTokens` increases.

## Installation

Install the package with a Risk of Rain 2 Thunderstore manager. It requires `LevelUpChoices` 1.1.3 and `R2API Networking` 1.0.3; both are declared in `manifest.json` and installed automatically. Risk of Options is optional and is not bundled or declared as a required package dependency. Do not copy `LevelUpChoices.dll` or Item Qualities into this package; both are dependencies.

## AI-generated content

This package contains AI-assisted code. Select the Thunderstore `AI Generated` category when publishing if that category is available for the Risk of Rain 2 community. The package must pass the manual in-game and multiplayer matrix before upload; the repository currently has build and pure behavior verification only.

## Verification matrix

The repository's pure behavior checks cover absent integration, 0% and 100% chance, default distribution, luck, zero weights, missing variants, reroll exclusion, canonical banish, issue #1 artifact/config regression, and issue #2 multiplayer option synchronization. In-game verification must still cover the host/client run, Item Qualities absent, pause/Escape/menu teardown, issue #4 same-item reroll, and the upstream all-sources toggle.
