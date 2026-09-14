# Troubleshooting and expected behavior

Use this checklist when playing with LevelUpChoicesFixes. The host should install the same mod versions as every client. Item Qualities is required for quality variants; Risk of Options is optional.

| Situation | What to check | Expected behavior |
| --- | --- | --- |
| No quality items appear | Install Item Qualities, enable `Enable Quality Integration`, and start a new run | Ordinary offered items have the configured random quality chance. With defaults, the chance is `4%`. |
| Guaranteed quality choices | Leave `Guaranteed Quality Every N Levels=5` and `Guaranteed Quality Choice Count=3` | Every fifth level queues a new choice set with three quality choices. Existing choices already on screen are not replaced. |
| Want to disable guarantees | Set `Guaranteed Quality Every N Levels=0` | Only the ordinary random quality chance remains. |
| Want more or fewer guaranteed choices | Change `Guaranteed Quality Choice Count` | The next guaranteed set uses the configured number, up to the number of choices provided by LevelUpChoices. |
| Higher-rarity quality items | Continue selecting items and watch `UsedTokens` increase | Guaranteed quality choices use the normal LevelUpChoices rarity progression, so green and red base items become more likely later. |
| Quality rarity distribution | Adjust the four quality weights | The weights are relative and normalized over quality variants that exist for the selected item. |
| Quality chests and printers | Adjust `Remove Quality Interactables` and LevelUpChoices' item-source removal setting | By default, quality chest and printer sources are removed with other item sources. |
| Multiplayer settings | Change quality, interactable, blacklist, and XP settings on the host and start or continue a run | The host's quality, blacklist, interactable, and XP settings apply to all players. |
| Quality plugin absent | Start a run without Item Qualities installed | LevelUpChoicesFixes disables only quality features; ordinary LevelUpChoices choices continue to work. |
| Menu or pause cleanup | Close the choice screen through Escape, the pause menu, or by ending a run | The choice screen closes and the game resumes normally. |

For a bug report, include the mod versions, host/client setup, configuration values, and relevant `BepInEx/LogOutput.log` messages.
