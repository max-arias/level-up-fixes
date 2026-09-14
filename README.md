# level-up-fixes

`LevelUpChoicesFixes` is a TeamTayne add-on for **Risk of Rain 2** that extends and fixes issues around the original [karaeren/LevelUpChoices](https://github.com/karaeren/LevelUpChoices) mod.

It intentionally runs on top of `karaeren.LevelUpChoices` 1.1.3. It does not replace or duplicate the original level-up manager, UI, artifact, XP hook, item-grant path, or network message set.

## Contents

- `src/` — independently written Harmony and runtime integration code.
- `ThunderstoreContent/` — Thunderstore package metadata and content.
- `tests/` — pure behavior checks and the manual in-game QA matrix.
- `build/` — built assembly and package archive.

## Build

The project compiles against the upstream `LevelUpChoices.dll` API assembly without bundling it. Obtain a matching upstream checkout at `./luc` before building:

```bash
git clone https://github.com/karaeren/LevelUpChoices.git luc
```

Then restore and build:

```bash
/tmp/dotnet/dotnet restore src/LevelUpChoicesFixes.csproj
/tmp/dotnet/dotnet build src/LevelUpChoicesFixes.csproj --configuration Release
```

The package includes the plugin DLL and its PDB directly under the BepInEx plugin route:
`BepInEx/plugins/LevelUpChoicesFixes.dll` and `BepInEx/plugins/LevelUpChoicesFixes.pdb`.
The mod manager installs that route into its author/package directory (`TeamTayne-LevelUpChoicesFixes`).

## Package

```bash
python3 build/package.py
```

The package is created at `build/TeamTayne-LevelUpChoicesFixes-1.0.0.zip` with the required files at its archive root.

## Diagnostic chart

Generate the default LevelUpChoices and Item Qualities probability chart:

```bash
python3 build/quality_chance_chart.py
```

The SVG output is written to [`build/quality-chance-chart.svg`](build/quality-chance-chart.svg). Dashed lines show the expected 4% quality promotion and its `70 / 20 / 8 / 2` tier split.

## Guaranteed quality choice sets

When Item Qualities is installed, the add-on can replace periodic level-up choice sets with guaranteed quality choices. The default is one set every `5` levels with `3` quality choices. The normal LevelUpChoices drop table still selects each base item, so white/green/red probabilities continue to rise with `UsedTokens`; only the quality promotion chance is bypassed for the guaranteed set. Each resulting quality variant still uses the configured `70 / 20 / 8 / 2` quality-tier weights.

Set `Guaranteed Quality Every N Levels` to `0` to disable the milestone sets, or change `Guaranteed Quality Choice Count` to control how many choices in each set are forced to quality. If a previous choice set is still unspent, the milestone is queued until the next set is generated. Both settings are server-authoritative and appear in Risk Of Options when installed.
