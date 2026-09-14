# RiskOfOptions — Best Practices & Findings (LevelUpChoicesFixes)

Findings from Rune580's primary sources, compared against this repository's config
(`src/ConfigState.cs`) and plugin integration (`src/RiskOfOptionsIntegration.cs`).
The research worker did not modify source or package files.

## Primary sources

| Source | URL |
|---|---|
| RiskOfOptions repository (README, API docs) | https://github.com/Rune580/RiskOfOptions |
| RiskOfOptions Wiki | https://github.com/Rune580/RiskOfOptions/wiki |
| Usage example plugin (full source read) | https://github.com/Rune580/RiskOfOptions-Example |
| Example plugin registration code | https://github.com/Rune580/RiskOfOptions-Example/blob/master/RiskOfOptions-Example/RiskOfOptionsExamplePlugin.cs |
| Thunderstore package | https://thunderstore.io/c/riskofrain2/p/Rune580/Risk_Of_Options/ |
| NuGet package | https://www.nuget.org/packages/Rune580.Mods.RiskOfRain2.RiskOfOptions |

## Dependency & package identity

- **BepInEx dependency GUID**: `com.rune580.riskofoptions`
  (README: `[BepInDependency("com.rune580.riskofoptions")]`).
- **NuGet package ID** (for csproj `PackageReference`): `Rune580.Mods.RiskOfRain2.RiskOfOptions`.
- **Thunderstore identity**: `Rune580/Risk_Of_Options`.
- Soft-dependency is preferable for this mod: RiskOfOptions is a UI convenience, not a
  functional requirement. The current repo does this correctly —
  `RiskOfOptionsIntegration.PluginGUID == "com.rune580.riskofoptions"`, gated on
  `Chainloader.PluginInfos.ContainsKey(PluginGUID)` before any RiskOfOptions type is
  touched, with the registration wrapped in `try/catch` (log a warning and fall back to
  native BepInEx config). Keeping `RegisterOptions` in a separate method with
  `[MethodImpl(NoInlining | NoOptimize)]` is a sound hardening measure so the JIT never
  hoists RiskOfOptions type references into the soft-dependent caller.

## Current idiomatic API (v2.x)

All registration goes through the static `RiskOfOptions.ModSettingsManager`:

```csharp
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;

ModSettingsManager.AddOption(new CheckBoxOption(boolEntry, new CheckBoxConfig { ... }));
ModSettingsManager.SetModDescription("...");
ModSettingsManager.SetModIcon(sprite);
```

Supported option types and their config classes (from README + example source):

| ConfigEntry type | Option class | Config class |
|---|---|---|
| `bool` | `CheckBoxOption` | `CheckBoxConfig` |
| `float` | `SliderOption` | `SliderConfig` (`min`, `max`) |
| `float` (stepped) | `StepSliderOption` | `StepSliderConfig` (`min`, `max`, `increment`) |
| `int` | `IntSliderOption` | `IntSliderConfig` (`min`, `max`) |
| `float` (free) | `FloatFieldOption` | `FloatFieldConfig` |
| `int` (free) | `IntFieldOption` | `IntFieldConfig` |
| `KeyboardShortcut` | `KeyBindOption` | `KeyBindConfig` |
| `string` | `StringInputFieldOption` | `InputFieldConfig` |
| `Enum` | `ChoiceOption` | `ChoiceConfig` |
| `Color` | `ColorOption` | `ColorOptionConfig` |
| (none) | `GenericButtonOption(name, category, description, buttonText, Action)` | — |

Every `*Config` supports `checkIfDisabled = () => bool`, a delegate evaluated by the
menu to grey out an option (example: `new CheckBoxConfig { checkIfDisabled = Disabled }`).
Note the string option is `StringInputFieldOption` (README calls the feature
"String Input Fields"); plain `InputFieldOption` is not the public name.

**Live updates**: RiskOfOptions writes through to the bound `ConfigEntry`. React to
changes via the standard BepInEx event — `entry.SettingChanged += (sender, args) => { ... }`.
There is no RiskOfOptions-specific change callback; values are live in the entry the
moment the user edits them.

**Category naming**: the second constructor/config argument is the in-menu category
heading (example uses `"General"` / `"Disable"`). Options sharing a category are
grouped under one collapsible header in the mod's options panel.

## Lifecycle / registration requirements

1. Bind all `ConfigEntry`s in the plugin's `Awake()` first, then call
   `ModSettingsManager.AddOption(...)` for each — the example plugin does exactly this
   inside `Awake()`. Registration must complete before the main-menu options panel is
   opened; RiskOfOptions snapshots the mod list when the menu scene builds.
2. `SetModDescription` / `SetModIcon` are optional metadata calls; description is plain
   text, icon is a `Sprite`.
3. Soft-dependents should register from `Awake()` (or `Start()`) behind the
   `Chainloader.PluginInfos` guard described above — the example repo (a hard dep)
   registers unconditionally, which is *not* the pattern a soft-dependent should copy.
4. Do not register options later than startup (e.g., mid-run); late registrations are
   not the documented pattern and risk missing the panel snapshot.

The current repo satisfies all four: `RiskOfOptionsIntegration.TryRegister()` is
soft-gated, wrapped in try/catch, and is invoked from plugin startup after config
binding.

## Recommended exposure for LevelUpChoicesFixes

Risk of Rain 2 level-up choice generation is **server-authoritative**: the host's
config governs a multiplayer run. The integration encodes this —
`ServerOptionDisabled()` returns true when `NetworkClient.active && !NetworkServer.active`
(i.e., a pure client), and every exposed option uses that delegate as `checkIfDisabled`.
The mod description also tells users that host/server settings are authoritative during
multiplayer runs. That is the correct conservative posture.

Conservative recommendation — expose only settings whose value is read when a level-up
choice is generated or when the affected event fires, so a host's mid-run edit applies
to the very next level-up without restart:

**Live-editable by the host (recommended for exposure):**
- `EnableQualityIntegration` (bool → CheckBoxOption)
- `AllowQualityChests` (bool → CheckBoxOption)
- `QualityChance`, `UncommonQualityWeight`, `RareQualityWeight`, `EpicQualityWeight`,
  `LegendaryQualityWeight` (float → SliderOption with bounded `min`/`max`)
- `RerollRefreshOnLevel` (enum `RerollRefreshMode` → ChoiceOption — maps cleanly to a
  dropdown since it is a three-value enum: Off / ToStartingValue / AddOne)
- `RerollRefreshEveryNLevels`, `ItemChoicesEveryNLevels` (int → IntSliderOption with
  sensible `min`/`max` bounds)

**Initialization-/stage-scoped (expose only with "applies next stage" caveats, or keep
out of the menu):**
- `PreserveChests`, `PreservePrinters`, `PreserveShrines`, `PreserveShops`,
  `PreserveScrappers`, `PreserveCleansePools` (bool) and
  `InteractableCreditMultiplier` (float → StepSliderOption with a fixed `increment`)
  — these shape interactable/scene credit behavior that is consumed when a stage is
  generated. A mid-run change cannot retroactively alter the current stage; exposing
  them without a caveat invites "it didn't do anything" reports. The conservative
  options are (a) leave them native-BepInEx-config-only, or (b) expose them but state
  in the option description that changes take effect from the next stage onward.

**Never client-editable during a multiplayer run:** all of the above — enforce with
`checkIfDisabled = ServerOptionDisabled` on every exposed option, matching the
example repo's `Disabled()` delegate pattern.

## Comparison: current repo vs. upstream guidance

Matches best practice:
- Soft-dependency guard + try/catch + separate registration method (stronger than the
  example repo's hard-reference pattern, appropriate for an optional UI dep).
- `ModSettingsManager.SetModDescription` called first with an honest
  host-authoritative disclaimer.
- Correct option/config type pairings (`CheckBoxOption`/`CheckBoxConfig`,
  `SliderOption`/`SliderConfig`, `StepSliderOption`/`StepSliderConfig`,
  `IntSliderOption`/`IntSliderConfig`, `ChoiceOption`/`ChoiceConfig`), all with
  explicit ranges where applicable.
- Enum-based setting (`RerollRefreshMode`) exposed as `ChoiceOption` — the documented
  way to render an enum dropdown.

No divergences from the documented API were found in the integration file; the only
advisory items are the stage-scoped caveat for the `Preserve*`/credit-multiplier group
and confirming `SettingChanged` handlers exist wherever a live value is cached instead
of read per level-up.
