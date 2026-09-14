# Changelog

## 1.0.0

- Added the planned correctness, token/schedule, interactable, and XP integration releases behind independent settings.
- Added fail-closed Item Qualities integration with host-authoritative quality chance and relative tier weights.
- Added canonical reroll, exclusion, and banish handling without replacing LevelUpChoices state or networking.
- Added pause teardown recovery, item blacklist, level-based rerolls, scheduled choices, per-category source controls, credit scaling, and XP curve controls.
- Declared the R2API Networking runtime dependency and applied the documented `NetworkingAPI.PluginGUID` dependency attribute.
- Corrected the BepInEx package route so the manager can install the DLL under its standard author/package namespace.
- Included the PDB alongside the release DLL for supported debugging workflows.
- Added optional Risk of Options registration for live-safe quality, schedule, and interactable settings.
- Added host-to-client synchronization when server settings change during a run.
- Added `Allow Quality Chests`, defaulting to `false`, so Item Qualities chest cards follow LevelUpChoices interactable removal.
- Embedded the package icon in the plugin and registered it with Risk of Options.
- Extended the default Quality interactable suppression to Item Qualities chest and printer variants.
