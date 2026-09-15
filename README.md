# LevelUpChoicesFixes

`LevelUpChoicesFixes` adds compatibility fixes and configurable quality support to [LevelUpChoices](https://github.com/karaeren/LevelUpChoices) for **Risk of Rain 2**.

## Requirements

- [LevelUpChoices 1.1.3](https://thunderstore.io/package/erenkara/LevelUpChoices/)
- R2API Director 3.1.0
- R2API Networking 1.0.3
- Optional: [Item Qualities](https://thunderstore.io/package/Gorakh/ItemQualities/)
- Optional: [Risk of Options](https://thunderstore.io/package/RiskofThunder/RiskOfOptions/)

Install the package with a Thunderstore-compatible mod manager. R2API Director and R2API Networking are installed as dependencies. Item Qualities is only needed for the quality features.

## Opening the choice menu

Use the configured toggle-menu key to open or close the item choices when the notification shows that you have unused tokens.


## Quality integration

With Item Qualities installed, LevelUpChoices can offer quality variants directly in its normal item choices.

Default settings:

- Random quality chance: `4%` per offered item
- Guaranteed quality set: every `5` levels
- Choices in a guaranteed set: `3`
- Quality weights: Uncommon / Rare / Epic / Legendary = `70 / 20 / 8 / 2`
- Quality chests, printers, and equipment distributors: disabled when LevelUpChoices removes item sources

Random quality rolls preserve the normal LevelUpChoices item rarity. Guaranteed sets do the same: early choices are usually white, while green and red base items become more likely as you select more items. The quality tier is then selected randomly from the configured quality weights.

At a guaranteed level, the next newly generated choice set contains three quality choices. If an earlier choice set is still waiting to be selected, the guarantee is queued rather than replacing choices already on screen.

## Configuration

All settings are server/host settings in the `Server` section of the BepInEx configuration file. Quality and interactable-credit settings also appear in Risk of Options when it is installed.

Important quality settings:

| Setting | Default | Description |
| --- | ---: | --- |
| Enable Quality Integration | `true` | Enable quality variants in level-up choices. |
| Quality Chance | `4%` | Random quality chance for ordinary offered items. |
| Guaranteed Quality Every N Levels | `5` | Queue a guaranteed quality choice set. Set to `0` to disable. |
| Guaranteed Quality Choice Count | `3` | Number of guaranteed quality choices. |
| Uncommon / Rare / Epic / Legendary Quality Weight | `70 / 20 / 8 / 2` | Relative quality-tier weights. |
| Remove Quality Interactables | `true` | Remove Item Qualities chests, printers, and equipment distributors when LevelUpChoices removes item sources. |

Additional native configuration:

| Setting | Default | Description |
| --- | ---: | --- |
| Item Blacklist | `DefensiveMicrobots` | Comma-separated item names removed from every LevelUpChoices player pool. |
| Interactable Credit Multiplier | `1.0` | Multiplies the original interactable credit budget. |
| XP Curve | `Exponential` | Selects the custom XP curve shape. |
| Starting XP | `20` | XP required for the first custom level step. |
| XP Scaling | `1.55` | Exponential multiplier, or linear additive rate when XP Curve is `Linear`. |

Host settings are authoritative in multiplayer and synchronize to connected clients when a run starts or settings change.

## Other fixes

- Prevents quality variants from causing duplicate-item reroll and banish problems.
- Recovers cleanly when the choice menu is closed through the pause screen or when a run ends.
- Adds configurable item blacklists, quality-interactable removal, interactable credit scaling, and XP curves.

## Probability chart

The repository includes a [quality chance chart](ThunderstoreContent/quality-chance-chart.svg) showing the default LevelUpChoices rarity progression alongside the 4% random quality rate, quality-tier weights, and the default guaranteed-quality cadence.

## Development and releases

Build locally with:

```bash
dotnet build src/LevelUpChoicesFixes.csproj --configuration Release
python3 quality_chance_chart.py
python3 scripts/package.py
```

Generated DLLs, PDBs, and ZIP packages are ignored by Git. Pushing a version tag matching
`vMAJOR.MINOR.PATCH` runs the GitHub Actions release workflow and attaches the generated ZIP to a
GitHub release.

## Support

Report issues with the mod versions, configuration values, host/client setup, and relevant `BepInEx/LogOutput.log` messages:

https://github.com/max-arias/level-up-fixes/issues
