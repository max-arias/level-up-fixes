# Changelog

## 1.0.3

- Restored Shrine of the Mountain, Halcyon Shrine, Altar of Gold, Shrine of Shaping, Shrine of the Woods, and the Collective Shrine of Combat.
- Corrected the release packaging after the 1.0.2 shrine policy was changed; Collective Shrine of Combat is explicitly removed from LevelUpChoices' private blacklist before stage population.

## 1.0.2

- Fixed item-source removal missing DLC3's Temporary Item Distributor and the equipment-less DLC3 drone scrapper.
- Item sources that spawn outside stage pools are now refused too: Item Qualities' dropped-item barrel and cloaked chest, key lockboxes, and shipping request deliveries.
- Capped equipment barrels at four per stage and kept equipment shops removed. This preserves a small equipment supply without LevelUpChoices' Barrels-category weight redistribution causing extreme density. Item Qualities' equipment barrel remains removed when `Remove Quality Interactables` is enabled.
- Restored Shrine of the Mountain, Halcyon Shrine, Altar of Gold, Shrine of Shaping, Shrine of the Woods, and the Collective Shrine of Combat; the latter is explicitly removed from LevelUpChoices' private blacklist.
- Fixed runs stalling at high levels: the add-on no longer overwrites LevelUpChoices' XP table.
- Fixed queued guaranteed quality sets carrying over while the mod is disabled and being spent after re-enabling.
- Removed the `XP Curve`, `Starting XP`, and `XP Scaling` settings. LevelUpChoices' own `Max Level` and `Enable Level System` settings control the XP curve again, using its auto-tuned progression.

## 1.0.1

- Fixed the probability chart image link in the Thunderstore README.

## 1.0.0

- Added a configurable 4% random quality chance with weighted Uncommon, Rare, Epic, and Legendary results.
- Added guaranteed quality choice sets, defaulting to three quality choices every five levels.
- Guaranteed sets keep the normal progression from white to green and red item rarities as the run advances.
- Added safe rerolls and banishes for quality variants without duplicate base items.
- Added pause/menu cleanup so the game does not remain paused after closing the choice screen.
- Added configurable item blacklists, interactable controls, interactable credit scaling, and XP curves.
- Added optional Risk of Options controls for quality and interactable settings.
- Host/server settings synchronize to clients in multiplayer.
- Improved mod-manager installation and included the plugin's debugging symbols for troubleshooting.
- Replaced granular interactable-preservation settings with one `Remove Quality Interactables` control.
- Removed the reroll refresh and item-choice schedule settings; LevelUpChoices now uses its normal level-up cadence.
- Switched interactable removal to R2API Director's DCCS-pool hook so chests and other item sources are filtered before stage population.
