# Changelog

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
