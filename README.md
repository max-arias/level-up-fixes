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

## Verification

Pure behavior checks:

```bash
python3 -m unittest discover -s tests -v
```

Manual game and multiplayer verification is documented in [`tests/IN_GAME_TEST_MATRIX.md`](tests/IN_GAME_TEST_MATRIX.md).
