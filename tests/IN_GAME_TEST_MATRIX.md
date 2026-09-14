# In-game verification matrix

Run each case with a disposable Risk of Rain 2 profile containing the pinned `LevelUpChoices` 1.1.3 package and this add-on. For quality cases, use a matching Item Qualities installation; repeat the quality-absence cases without it.

| Case | Procedure | Expected observation |
| --- | --- | --- |
| Quality defaults | Start a run, level repeatedly, record 10,000 promoted choices with a fixed test seed or large sample | Quality promotion is approximately 4%; 300 eligible rolls should average about 12 quality items, with a zero-result run having probability about 0.00048%. Available quality tiers follow normalized 70/20/8/2 weights. |
| Quality chance 0 | Set `Quality Chance=0`, start a fresh run | No quality pickup is offered; LevelUpChoices options, grants, and `SyncItems` remain normal. |
| Quality chance 100 | Set `Quality Chance=100`, leave all tier weights positive | Every eligible base item becomes a quality variant; absent variants never become ordinary alternatives. |
| Missing quality API | Remove Item Qualities, start a run | Add-on logs a fail-closed warning; original LevelUpChoices still rolls and grants items. |
| Luck | Compare equivalent rolls with no Clover and positive Clover/luck | Original item tier/luck behavior remains; quality chance increases only according to the configured quality roll. |
| Reroll and banish | Put a quality item in a slot, reroll it, then banish it | Reroll excludes the slot's canonical base item; banish removes that base from future rolls while the selected quality pickup remains grantable. |
| Issue #1 regression | Run once with `Always Enable Mod=false` and artifact enabled, then with `Always Enable Mod=true` | Artifact/config gating still belongs to LevelUpChoices and works in both supported modes. |
| Issue #2 regression | Join as a non-host client and level the host run | Client receives and displays the host's item options and can select them. |
| Pause teardown | Open choices, press Escape, close the pause menu, return to lobby/menu, and start another run | Choice UI closes and `Time.timeScale` returns to its prior positive value; next run is playable. |
| All-sources toggle (#7) | Set upstream `Remove Chests & Interactables=false`, then true with one `Keep ...` category enabled | False keeps normal sources; true removes configured categories while the selected keep category remains. |
| Quality interactables | With Item Qualities installed, compare `Allow Quality Chests=false` and `true` while upstream removal is enabled | Default false removes `iscQualityChest1`, `iscQualityChest2`, `iscQualityDuplicator`, `iscQualityDuplicatorLarge`, `iscQualityDuplicatorMilitary`, and `iscQualityDuplicatorWild`; true permits those variants to spawn. |
| Risk of Options | Install Risk of Options, open Mod Options, then change a host setting during a run | Quality, schedule, and interactable settings are visible; host changes apply on subsequent relevant level/stage events and synchronize to clients. Without Risk of Options, native config remains functional. |
| Schedule | Set `Item Choices Every N Levels=4` and level 1–8 | Choice tokens/options arrive on levels 4 and 8; unspent options are preserved. |
| Reroll refresh | Set each refresh mode and interval, then level through the interval | Off changes nothing; ToStartingValue resets to upstream starting count; AddOne adds exactly one. |
| Multiplayer config | Host and client use different local values; host starts run | Client uses host/server quality, blacklist, schedule, interactable, and XP values. |
| XP release | Test Exponential and Linear settings in separate fresh runs | Original ExperienceHook remains the only XP hook; its generated table follows the selected curve. |

The current container has no Risk of Rain 2 executable or authenticated Thunderstore team account, so the game cases and online validator submission require execution on the game workstation/account. The package script still performs local manifest checks, archive-root checks, and disposable-profile extraction checks.
