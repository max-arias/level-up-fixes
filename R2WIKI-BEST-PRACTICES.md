# R2Wiki and Thunderstore best-practice review

Reviewed the R2Wiki sitemap and the pages relevant to this code-only BepInEx plugin: first-mod setup, cookbook/releasing, assembly references, configuration, hooks, soft dependencies, networking, server/client behavior, testing, debugging, Unity version, and package-install rules. The package rules below also use the current Thunderstore and r2modman documentation.

## Findings applied

- **Thunderstore ZIP root:** `icon.png`, `README.md`, and `manifest.json` must be at the ZIP root; `CHANGELOG.md` is supported. Names are case-sensitive. The icon must be exactly 256x256 PNG. Source: [Thunderstore Creating a Package](https://wiki.thunderstore.io/mods/creating-a-package).
- **BepInEx route:** `plugins` is an override folder. r2modman installs files from it under `BepInEx/plugins/<Author-ModName>/`; a package should not add another mod-specific directory because that creates an unnecessary extra level. Source: [r2modman package structure](https://github.com/ebkr/r2modmanPlus/wiki/Structuring-your-Thunderstore-package), [Thunderstore packaging](https://wiki.thunderstore.io/mods/packaging-your-mods.md).
- **Plugin identity:** BepInEx discovers the assembly from `[BepInPlugin(GUID, Name, Version)]`. A C# namespace is compiled metadata, not a Thunderstore ZIP directory. This project uses `TeamTayne.LevelUpChoicesFixes` as its C# namespace, `TeamTayne.LevelUpChoicesFixes` as its plugin GUID, and `LevelUpChoicesFixes` as its assembly/package name. Source: [First Mod](https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/Getting-Started/First-Mod/), [Configuration](https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/C%23-Programming/Configuration/).
- **Dependencies:** Runtime dependencies belong in both the plugin's `[BepInDependency]` attributes and `manifest.json`. R2API Networking specifically documents `[BepInDependency(NetworkingAPI.PluginGUID)]`; this project now declares it and the exact Thunderstore dependency `RiskofThunder-R2API_Networking-1.0.3`. Source: [R2API.NetworkingAPI](https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/C%23-Programming/Networking/R2API.NetworkingAPI/), [Thunderstore manifest](https://wiki.thunderstore.io/mods/creating-a-package.md#dependencies).
- **Soft dependency isolation:** A soft dependency is marked with `SoftDependency`; code that requires its API must be isolated and guarded. This project uses reflection against Item Qualities and fails closed when the plugin/API is absent. Source: [Soft Dependency](https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/C%23-Programming/Mod-Compatibility%3A-Soft-Dependency/).
- **Hooks:** Preserve the original behavior and hook chain; use narrow seams, null checks, and failure logging. This project uses Harmony postfix/prefix patches, keeps original LevelUpChoices state/network paths, checks unsupported seams, and unpatches its Harmony instance on teardown. Source: [Hooking](https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/C%23-Programming/Hooking/), [On Hook](https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/C%23-Programming/Hooking/On-Hook/).
- **Networking:** Server authority, deterministic serialization order, explicit registration, and client/server guards are required. The config sync message follows `INetMessage` serialization/deserialization order and host-authoritative behavior. Source: [R2API.NetworkingAPI](https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/C%23-Programming/Networking/R2API.NetworkingAPI/), [Server/client mods](https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/C%23-Programming/Networking/Server-side-and-client-side-mods/).
- **Configuration:** BepInEx `ConfigEntry<T>` values are bound from the plugin's `Config`; this project binds server settings in one `Server` section and clamps values at use sites. Source: [Configuration](https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/C%23-Programming/Configuration/).
- **Build references:** Game assemblies should come from the BepInEx/RoR2 NuGet ecosystem, not be redistributed. The project targets Unity's documented Risk of Rain 2 version (`2021.3.33`), references stripped/publicized game packages, and does not package game or dependency DLLs. Source: [Assembly References](https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/C%23-Programming/Assembly-References/), [Unity Version](https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/Unity-Version/).
- **Debug symbols:** The First Mod and debugging guides place the PDB next to the DLL during debugging. The release package now includes the PDB beside the plugin DLL. Source: [First Mod](https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/Getting-Started/First-Mod/), [Debugging Your Mods](https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/C%23-Programming/Debugging-Your-Mods/).

## Cookbook versus current installer behavior

The R2Wiki cookbook shows a generic inner mod folder in its illustrative package tree. The current r2modman packaging guide documents that BepInEx override folders already receive the `Author-ModName` installation directory and explicitly warns that mod-specific subfolders are not used. The package follows the current installer rule: the archive has `BepInEx/plugins/LevelUpChoicesFixes.dll` directly, not `BepInEx/plugins/LevelUpChoicesFixes/LevelUpChoicesFixes.dll`.

## AI-generated content rule

Thunderstore's global rules say AI-generated assets/code should use the `AI Generated` category when that category exists, and packages should be tested before upload with READMEs that accurately describe current functionality. Source: [Thunderstore Global Rules](https://wiki.thunderstore.io/moderation/global-rules.md#ai-generated-content).

The package README now discloses AI-assisted code and says to select that category. The package must not be uploaded until the manual in-game and multiplayer matrix passes; build and pure behavior checks alone are not in-game validation. The category is an upload-side setting, not a supported `manifest.json` field.

## Resulting package shape

```text
icon.png
README.md
manifest.json
CHANGELOG.md
plugins/
  LevelUpChoicesFixes.dll
  LevelUpChoicesFixes.pdb
```

For this project the ZIP uses the explicit equivalent route:

```text
BepInEx/plugins/LevelUpChoicesFixes.dll
BepInEx/plugins/LevelUpChoicesFixes.pdb
```

The manager maps that route to `BepInEx/plugins/TeamTayne-LevelUpChoicesFixes/`. The C# namespace is already present inside the DLL; it must not be represented by another ZIP directory.
