# Removing chests, printers, and other interactables in Risk of Rain 2 mods

Scope: determine the idiomatic, reliable way to strip chests / 3D printers / shrines from stage
spawn pools, compare the R2API Director APIs against `LevelUpChoices.InteractableSpawnHook`, and
explain why the current `LevelUpChoicesFixes` Harmony/event approach does not work.

Primary sources: the shipped game assemblies (`RoR2.dll` 1.4.x, decompiled locally), the upstream
R2API.Director sources, the vendored `karaeren/LevelUpChoices` sources in `luc/`, and the official
Risk of Thunder modding wiki. Every external claim is cited with a URL.

---

## 1. Verdict

| Approach | Reliable? | Why |
| --- | --- | --- |
| Harmony prefix on `LevelUpChoices.InteractableSpawnHook.OnPrePopulateSceneServer` | **No** | Method is `private` (naming-fragile); a *prefix* runs **before** LUC's own blacklist check, so `BlacklistedSpawns` is always empty of our additions; and the field name `BlacklistedSpawns` does not resolve — see §3. |
| `R2API.DirectorAPI.InteractableActions` / `Helpers.RemoveExistingInteractable` | Correct API, **wrong timing and not currently loadable** | Fires from `ClassicStageInfo.Start`, but the game snapshots the interactable DCCS in `ClassicStageInfo.RebuildCards` during that same `Start`; R2API on-hooks it, so ordering is uncertain. Also R2API.Director is not a declared dependency of this plugin. See §5. |
| **`SceneDirector.onPrePopulateSceneServer`** | **Yes** — recommended | Vanilla event, invoked in `SceneDirector.Start` immediately before `PopulateScene()`. `ClassicStageInfo.interactableCategories` is guaranteed non-null and is *the same instance* the director reads. See §4. |
| `ClassicStageInfo.interactableDccsPool` mutation in `ClassicStageInfo.Start` | Runs, but runs too late | The pool is blended into `interactableCategories` inside `RebuildCards`, which `ClassicStageInfo.Start` calls. Mutating the pool *after* that `Start` (i.e. from an on-hook postfix) has no effect on the current stage. |
| `SceneDirector.onGenerateInteractableCardSelection` | Yes, but narrow | Fires with a **throwaway copy** of the DCCS; ideal for per-stage last-word filtering, but it is a copy, so it cannot be used to persist or inspect state. |
| Post-spawn destruction of already-created interactables | Works but discouraged | Loses director credit accounting and causes visible pop-in; no vanilla event exists for it. |

**The immediate bug is not a design problem — it is a wiring bug.** `PatchInteractables` is dead
code and is never called, so none of the interactable logic in this plugin is installed at all.
See §2.

---

## 2. Root cause: `PatchInteractables` is never called

`IntegrationPatches.Install` in `src/LevelUpChoicesFixes.cs` installs only these patch groups:

```csharp
internal static void Install(Harmony harmony)
{
    PatchInitialize(harmony);
    PatchRoll(harmony);
    PatchRerollAndBanish(harmony);
    PatchLevelSchedule(harmony);
    PatchPause(harmony);
    PatchExperience(harmony);
    QualityRuntime.TryInitialize();
}
```

`PatchInteractables(harmony)` is **absent**, yet its body still exists at `src/LevelUpChoicesFixes.cs:169`.
All of the following are therefore unreachable:

- `InteractablePrefix` (the LUC `BlacklistedSpawns` augmentation for quality chests/printers),
- `InteractableCategoriesPrefix` → `FilterInteractableCategories()` (category filtering),
- `InteractableCreditPrefix` (interactable credit scaling).

Evidence that `FilterInteractableCategories` has **zero call sites** (only its own declaration):

```
$ grep -n "FilterInteractableCategories" src/LevelUpChoicesFixes.cs
(no matches)
```

History confirms the regression: `git log -S "PatchInteractables(" -- src/LevelUpChoicesFixes.cs`
shows the call present in `b8d6837` ("Add LevelUpChoices fixes add-on") and removed in `053ea34`
("Make level-up notification clickable"), where it was replaced in the same diff by
`PatchNotification(harmony)` — an unrelated change that dislodged the call line. The dead method was
retained, so the compiler never complained.

The same commit-era code also stores stale state: `SpawnBlacklistField` and `InteractableCreditField`
are declared (`src/LevelUpChoicesFixes.cs:78-79`) but only consumed by the unreachable methods.

### 2.1 The credit multiplier is independently broken

`InteractableCreditField = AccessTools.Field(typeof(SceneDirector), "interactableCredit")` cannot
resolve. In the shipped game, `SceneDirector.interactableCredit` is an **auto-property**, not a field:

```csharp
// RoR2.dll (1.4.x), decompiled
public int interactableCredit { get; set; }
```

An auto-property has no field literally named `interactableCredit`; the compiler emits
`<interactableCredit>k__BackingField`. HarmonyX's `AccessTools.Field(Type, string)` performs only a
plain `GetField(name, all)` search including base types and does **not** synthesize backing-field
names — so the lookup returns `null`:

```csharp
public static FieldInfo Field(Type type, string name)
{
    ...
    var fieldInfo = FindIncludingBaseTypes(type, t => t.GetField(name, all));
    if (fieldInfo is null)
        Logger.Log(...);
    return fieldInfo;
}
```

Source: <https://github.com/BepInEx/HarmonyX/blob/master/Harmony/Tools/AccessTools.cs> (`Field(Type, string)`)
and <https://github.com/pardeike/Harmony/blob/master/Harmony/Tools/AccessTools.cs> (same shape).
Note the `HarmonyMethod`/`__instance` reflection story is different: Harmony *does* resolve
`<name>k__BackingField` for its own field injection, but `AccessTools.Field` does not.

[INFERENCE, grounded on the two sources above] Consequence: `InteractableCreditField` is `null`, the
guard `if (InteractableCreditField != null)` would skip the credit prefix even after
`PatchInteractables` is restored, and `InteractableCreditPrefix` would do nothing. The fix is to use
the property accessor rather than a `FieldInfo`, or to look up `<interactableCredit>k__BackingField`
explicitly.

---

## 3. Why hooking `InteractableSpawnHook` from this plugin is unsound

The vendored dependency source is in-tree at `luc/src/InteractableSpawnHook.cs`. Reconstructed (the
class is `public`, the members are not):

```csharp
public class InteractableSpawnHook : MonoBehaviour
{
    private void Start()
    {
        SceneDirector.onPrePopulateSceneServer += OnPrePopulateSceneServer;
    }

    private static readonly HashSet<string> BlacklistedSpawns =
        new(StringComparer.OrdinalIgnoreCase) { /* iscchest1, iscduplicator, ... */ };

    private void OnPrePopulateSceneServer(SceneDirector director)
    {
        if (!ModConfig.IsModEnabled || !ModConfig.EnableInteractableRemoval.Value) return;
        ...
        if (!BlacklistedSpawns.Contains(originalCards[j].spawnCard.name)) { ... }
    }
}
```

Upstream: <https://github.com/karaeren/LevelUpChoices/blob/master/src/InteractableSpawnHook.cs>.
`ModConfig.EnableInteractableRemoval` is a real LevelUpChoices option
(`luc/src/ModConfig.cs:26,115,241`) — it is LevelUpChoices' own item-source-removal toggle, which is
why this plugin latches onto it.

Five concrete problems with the intended patch (`InteractablePrefix` on
`OnPrePopulateSceneServer`):1. **The hook is `private`.** `AccessTools.Method(typeof(LevelUpChoices.InteractableSpawnHook),
   "OnPrePopulateSceneServer")` matches a `private` method; the plugin does not and cannot declare
   the parameter list in `AccessTools.Method(Type, string)`, so if LUC renames the method (or adds an
   overload) the patch silently stops applying — and the current code only logs when the result is
   `null`, not when the signature changed.
2. **A Harmony *prefix* runs before the original body.** The engine call order is
   prefix → original (`OnPrePopulateSceneServer`) → postfix. `InteractablePrefix` adds names to
   `BlacklistedSpawns` and *then* delegates to a method whose very first act is
   `if (!EnableInteractableRemoval) return;`. With that option disabled, the prefix's mutations are
   discarded. With it enabled, the prefix works only by accident.
3. **State leaks across runs and config toggles.** `BlacklistedSpawns` is a `static readonly`
   process-lifetime `HashSet` owned by another assembly. `InteractablePrefix` adds or removes names
   from it based on the *current* config, but nothing restores it on run end, plugin unload, or a
   mid-stage config change — a toggle only takes effect on the next populate.
4. **Wrong owner.** The plugin is re-implementing the *dependency's* filter to piggyback on its
   blacklist, instead of filtering the stage DCCS itself. Every LUC update can invalidate it.

---

## 4. The vanilla pipeline (ground truth from `RoR2.dll` 1.4.x)

All line references below are from `ilspycmd -t RoR2.SceneDirector` / `-t RoR2.ClassicStageInfo` run
against `Risk of Rain 2_Data/Managed/RoR2.dll` from the installed game.

### 4.1 `SceneDirector.Start` — the hook point

```csharp
private void Start()
{
    if (!NetworkServer.active) return;                       // server authority
    rng = new Xoroshiro128Plus(Run.instance.stageRng.nextUint);
    float num = 0.5f + (float)Run.instance.participatingPlayerCount * 0.5f;
    ClassicStageInfo component = SceneInfo.instance.GetComponent<ClassicStageInfo>();
    if ((bool)component)
    {
        interactableCredit = (int)((float)component.sceneDirectorInteractibleCredits * num);
        ...
        Debug.LogFormat("Spending {0} credits on interactables...", interactableCredit);
    }
    SceneDirector.onPrePopulateSceneServer?.Invoke(this);     // <-- fires here
    PopulateScene();
    SceneDirector.onPostPopulateSceneServer?.Invoke(this);
}
```

Three properties matter:

- The event fires **before** any interactable card is selected and before `interactableCredit` is
  spent — so removing cards here changes both the selection pool and the credit burn in one shot.
- `Start` returns immediately on clients (`!NetworkServer.active`), so the event is
  **server-only by construction**. Handlers still conventionally guard with `NetworkServer.active`
  because the static event can be invoked by other code paths.
- `Start` (and therefore the event) runs *after* `ClassicStageInfo.Start` → after `RebuildCards`.

### 4.2 `SceneDirector.GenerateInteractableCardSelection` — the exact object we must edit

```csharp
private WeightedSelection<DirectorCard> GenerateInteractableCardSelection()
{
    DirectorCardCategorySelection directorCardCategorySelection =
        ScriptableObject.CreateInstance<DirectorCardCategorySelection>();
    if ((bool)ClassicStageInfo.instance && (bool)ClassicStageInfo.instance.interactableCategories)
    {
        directorCardCategorySelection.CopyFrom(ClassicStageInfo.instance.interactableCategories);
    }
    SceneDirector.onGenerateInteractableCardSelection?.Invoke(this, directorCardCategorySelection);
    WeightedSelection<DirectorCard> result =
        directorCardCategorySelection.GenerateDirectorCardWeightedSelection();
    UnityEngine.Object.Destroy(directorCardCategorySelection);   // note: destroyed
    return result;
}
```

`PopulateScene()` calls this **first**, then spends `interactableCredit` in a loop calling
`SelectCard(weightedSelection, interactableCredit)`.

Key consequences:

- The DCCS that is actually read is **`ClassicStageInfo.instance.interactableCategories`**.
- It is **copied** into a fresh throwaway instance, handed to `onGenerateInteractableCardSelection`,
  converted to a `WeightedSelection<DirectorCard>`, and then the throwaway is destroyed. A handler of
  `onGenerateInteractableCardSelection` therefore edits only a transient copy.
- Editing `ClassicStageInfo.instance.interactableCategories` **in place** — before
  `PopulateScene()` — is what the upstream dependency does and what this plugin attempted.

### 4.3 `ClassicStageInfo.RebuildCards` — where `interactableCategories` is assigned

```csharp
private void Start()
{
    RebuildCards();
    RunArtifactManager.onArtifactEnabledGlobal += OnArtifactEnabled;
    RunArtifactManager.onArtifactDisabledGlobal += OnArtifactDisabled;
}

internal void RebuildCards(DirectorCardCategorySelection forcedMonsterCategory = null,
                           DirectorCardCategorySelection forcedInteractableCategory = null)
{
    ...
    if (forcedInteractableCategory != null)
    {
        forcedInteractableCategory.OnSelected(this);
        interactableCategories = forcedInteractableCategory;
    }
    else if ((bool)interactableDccsPool)
    {
        if (modifiableMonsterCategories.expansionsInEffect != null
            && modifiableMonsterCategories.expansionsInEffect.Count > 0)
        {
            interactableCategories = DCCSBlender.GetBlendedDCCS(
                interactableDccsPool.poolCategories[0], ref xoroshiro128Plus4,
                this, contentSourceMixLimit, modifiableMonsterCategories.expansionsInEffect);
        }
        else
        {
            interactableCategories = DCCSBlender.GetBlendedDCCS(
                interactableDccsPool.poolCategories[0], ref xoroshiro128Plus4,
                this, contentSourceMixLimit);
        }
    }
}
```

And `DCCSBlender.GetBlendedDCCS` allocates a brand-new object:

```csharp
public static DirectorCardCategorySelection GetBlendedDCCS(DccsPool.Category dccsPoolCategory,
    ref Xoroshiro128Plus rng, ClassicStageInfo stageInfo,
    int contentSourceMixLimit = 2, List<ExpansionDef> acceptableExpansionList = null)
{
    DirectorCardCategorySelection blendedDCCS =
        ScriptableObject.CreateInstance<DirectorCardCategorySelection>();
    ...
}
```

Therefore:

- `interactableCategories` is **non-null and fully populated** once `ClassicStageInfo.Start` has run.
  The `!ClassicStageInfo.instance?.interactableCategories` early-return in this plugin's
  `FilterInteractableCategories` is a correct guard for a stage without interactables, not a timing
  problem.
- `interactableCategories` is a per-stage **`CreateInstance` copy**, not the shared asset. Mutating
  it in place does **not** corrupt the original stage asset and does **not** leak into other stages or
  other runs. This is exactly why upstream R2API backs up and restores the original
  (`BackupOrRestoreClassicStageInfoToOriginalState`) — see §5.
- `interactableCategories` is marked `[ShowFieldObsolete]` / "Deprecated. Use MonsterDccsPool
  instead." in `ClassicStageInfo`, and `interactableDccsPool` is `private` with an `internal`
  accessor (`internal DccsPool GetInteractableDccsPool => interactableDccsPool;`). Despite the
  deprecation label, `interactableCategories` remains the field the director actually consumes, so
  filtering it is the stable public surface.
- `RebuildCards` re-runs on artifact enable/disable (`OnArtifactEnabled` / `OnArtifactDisabled` when
  the MixEnemy or SingleMonsterType artifact toggles), which **reassigns** `interactableCategories`.
  Any filter applied once at stage load is discarded in that case — filter on every populate, not once.

Doc cross-reference: `SceneDirector` is documented as the stage owner of "AI spawning, Combat groups,
Interactable spawns, player spawns, credits/RNG/Object placement"
(<https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/C%23-Programming/Game-Code/Common-Components%2C-Events-and-Methods/>).

---

## 5. Comparison with R2API.Director

R2API.Director exposes three events and a helper layer:

```csharp
public static event Action<StageSettings, StageInfo>? StageSettingsActions;
public static event Action<DccsPool, List<DirectorCardHolder>, StageInfo>? MonsterActions;
public static event Action<DccsPool, StageInfo>? InteractableActions;
```

Source: <https://github.com/risk-of-thunder/R2API/blob/master/R2API.Director/DirectorAPIexternal.cs>.
The event is documented as "Event used to edit the pool of interactables that can spawn in a given
stage. First parameter is the `ClassicStageInfo.interactableDccsPool`, which is used to select a
`DirectorCardCategorySelection`" (same file). The module README lists `InteractableActions` as one of
its three intended entry points:
<https://github.com/risk-of-thunder/R2API/blob/master/R2API.Director/README.md>.

The removal helper delegates to that event and matches card names **lowercased**:

```csharp
public static void RemoveExistingInteractable(string? interactableName)
{
    DirectorAPI.SetHooks();
    RemoveExistingInteractable(interactableName, null);
}

public static void RemoveExistingInteractable(string? interactableName,
    Predicate<DirectorCardCategorySelection> predicate)
{
    DirectorAPI.SetHooks();
    StringUtils.ThrowIfStringIsNullOrWhiteSpace(interactableName, nameof(interactableName));
    var interactableNameLowered = interactableName.ToLowerInvariant();

    InteractableActions += (interactablesDccsPool, currentStage) =>
    {
        RemoveExistingInteractable(interactablesDccsPool, interactableNameLowered, predicate);
    };
}

private static void RemoveInteractableFromPoolEntry(string interactableNameLowered,
    DccsPool.PoolEntry poolEntry, Predicate<DirectorCardCategorySelection> predicate)
{
    if ((predicate != null && predicate(poolEntry.dccs)) || predicate == null)
    {
        for (int i = 0; i < poolEntry.dccs.categories.Length; i++)
        {
            var cards = poolEntry.dccs.categories[i].cards.ToList();
            cards.RemoveAll((card) => card != null && card.spawnCard &&
                card.spawnCard.name?.ToLowerInvariant() == interactableNameLowered);
            poolEntry.dccs.categories[i].cards = cards.ToArray();
        }
    }
}
```

Source: <https://github.com/risk-of-thunder/R2API/blob/master/R2API.Director/DirectorAPIhelpers.cs>.
Note `InteractableNames` in the same file is the canonical, authoritative list of vanilla
`InteractableSpawnCard` names — **all lowercase** (`"iscchest1"`, `"iscgoldchest"`,
`"iscduplicator"`, `"iscshrinechance"`, ...). This is the reference the dependency's own comment
points at:

```csharp
private static readonly HashSet<string> BlacklistedSpawns = new(StringComparer.OrdinalIgnoreCase)
{
    // https://github.com/risk-of-thunder/R2API/blob/master/R2API.Director/DirectorAPIhelpers.cs
    ...
};
```

### 5.1 Why `InteractableActions` is the wrong hook *here*

R2API invokes the events from `ClassicStageInfo` — specifically from
`DirectorAPIinternal.ApplyChanges`, which runs after `PortToNewSystem()`:

```csharp
private static void ApplyChangesOnStart(On.RoR2.ClassicStageInfo.orig_Start orig,
    ClassicStageInfo classicStageInfo)
{
    classicStageInfo.PortToNewSystem();
    classicStageInfo.ApplyChanges();
    orig(classicStageInfo);
}

internal static void ApplyChanges(this ClassicStageInfo classicStageInfo)
{
    var stageInfo = GetStageInfo(classicStageInfo);
    BackupOrRestoreClassicStageInfoToOriginalState(classicStageInfo, stageInfo);
    ApplyMonsterChanges(classicStageInfo, stageInfo);
    ApplyInteractableChanges(classicStageInfo, stageInfo);
    ApplySettingsChanges(classicStageInfo, stageInfo);
}

private static void ApplyInteractableChanges(ClassicStageInfo classicStageInfo, StageInfo stageInfo)
{
    if (InteractableActions != null)
        foreach (Action<DccsPool, StageInfo> item in InteractableActions.GetInvocationList())
            item(classicStageInfo.interactableDccsPool, stageInfo);
}
```

Source: <https://github.com/risk-of-thunder/R2API/blob/master/R2API.Director/DirectorAPIinternal.cs>.
Critically, `ApplyChanges` is invoked from a **prefix** on `ClassicStageInfo.Start` — i.e. R2API
mutates `interactableDccsPool` **before** `RebuildCards()` blends it into `interactableCategories`.
That is what makes `InteractableActions` a *valid* hook for R2API users.

Consequences for `LevelUpChoicesFixes`:

- Adopting `InteractableActions` would mean adding a hard dependency on `R2API.Director`. This plugin
  currently declares only `R2API.Networking` (`src/LevelUpChoicesFixes.csproj`,
  `ThunderstoreContent/manifest.json`), so `DirectorAPI` is not in its compile surface and its types
  are not guaranteed to be loaded at runtime. Adding a submodule dependency for one filter is heavier
  than subscribing to a vanilla static event.
- `InteractableActions` operates on the **pool**, i.e. it removes cards from
  `poolEntry.dccs.categories[i].cards`. The plugin's own quality filtering is expressible that way,
  but it must then run for every pool entry and every DCCS, whereas `onPrePopulateSceneServer` edits
  the single blended DCCS the director actually read. The latter is simpler and stricter.
- R2API backs up and restores the original stage state around every event
  (`BackupOrRestoreClassicStageInfoToOriginalState` → `BackupClassicStageInfoToOriginalState` /
  `RestoreClassicStageInfoToOriginalState`), including
  `originalClassicStageInfo.interactableDccsPoolCategories`. A plugin that mutates the DCCS itself
  and does **not** restore must rely on the fact that `interactableCategories` is a per-stage
  `CreateInstance` (established in §4.3). Keep that reliance explicit.

### 5.2 Deprecation context

`interactableCategories` carries `[ShowFieldObsolete]` and the tooltip "Deprecated. Use
MonsterDccsPool instead." The modern structure is `interactableDccsPool` → per-expansion DCCS, which
the game blends via `DCCSBlender`; the RoR2 1.3.7 API changelog documents the DCCS-blending rework and
the new `contentSourceMixLimit` / `contentSourceMixLimitOverride` fields:
<https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/Updating-Your-Mods/Updating-Core-API-Changelog-1.3.7/>.
The same R2API sources show the pool is a **DccsPool whose first `poolCategories[0]`** is the standard
interactable pool (`Helpers.InteractablePoolCategories.Standard`, weight 1f) —
`DirectorAPIexternal.cs` documents that a vanilla `interactableDccsPool` "usually contains ... 1
`DccsPool.poolCategories`".

Even so, the *consumer* (`SceneDirector.GenerateInteractableCardSelection`) still reads
`ClassicStageInfo.instance.interactableCategories`. Filtering that field is therefore both supported
and precisely scoped, and it avoids depending on the pool's expansion-conditional structure.

---

## 6. Recommendation

### 6.1 The hook

Subscribe to the vanilla static event `SceneDirector.onPrePopulateSceneServer`, filter
`ClassicStageInfo.instance.interactableCategories` in place, and guard on `NetworkServer.active`.
This is what the dependency does (`luc/src/InteractableSpawnHook.cs`) and it is the only hook whose
ordering relative to `PopulateScene()` is guaranteed by `SceneDirector.Start` itself.

Do **not** patch `InteractableSpawnHook.OnPrePopulateSceneServer`; the existing
`FilterInteractableCategories` implementation is already correct — it just needs to be called.

### 6.2 Exact code shape

Verbatim, the existing (correct) filter body already in `src/LevelUpChoicesFixes.cs`:

```csharp
private static void FilterInteractableCategories()
{
    if (!LevelUpChoices.ModConfig.IsModEnabled || !LevelUpChoices.ModConfig.EnableInteractableRemoval.Value ||
        !ClassicStageInfo.instance?.interactableCategories)
        return;

    DirectorCardCategorySelection selection = ClassicStageInfo.instance.interactableCategories;
    for (int i = 0; i < selection.categories.Length; i++)
    {
        DirectorCardCategorySelection.Category category = selection.categories[i];
        DirectorCard[] filteredCards = category.cards.Where(card =>
            card.spawnCard != null && !ItemSourceGroups.IsBlocked(card.spawnCard.name)).ToArray();
        if (filteredCards.Length == category.cards.Length)
            continue;

        if (filteredCards.Length == 0)
            category.selectionWeight = 0f;
        category.cards = filteredCards;
        selection.categories[i] = category;
    }
}
```

Replace `PatchInteractables` with an event subscription installed from `Install`:

```csharp
internal static void Install(Harmony harmony)
{
    // ... existing groups ...
    // Remove PatchInteractables(harmony) entirely; the LUC-prefix seam and the
    // SceneDirector.PopulateScene prefix are superseded by the vanilla event.
    SubscribeInteractables();
    // ...
}

private static bool _interactablesSubscribed;

private static void SubscribeInteractables()
{
    if (_interactablesSubscribed)
        return;
    SceneDirector.onPrePopulateSceneServer += OnPrePopulateSceneServer;
    _interactablesSubscribed = true;
}

private static void UnsubscribeInteractables()
{
    if (!_interactablesSubscribed)
        return;
    SceneDirector.onPrePopulateSceneServer -= OnPrePopulateSceneServer;
    _interactablesSubscribed = false;
}

private static void OnPrePopulateSceneServer(SceneDirector director)
{
    if (!NetworkServer.active)
        return;
    FilterInteractableCategories();
}
```

Rationale for each decision:

- **Static event, not Harmony** — the game raises it itself; there is no signature to break and no
  `AccessTools.Method` name lookup that can fail silently. Unsubscribe in plugin teardown
  (`UnsubscribeInteractables`) to avoid a stale handler after a domain reload; guard with the
  idempotence flag because a static event accumulates handlers.
- **`NetworkServer.active` guard** — `SceneDirector.Start` already returns early for clients, so this
  is defence in depth and makes the server-authority contract explicit. `SceneDirector` is
  server-authoritative; the wiki describes the director as owning spawn logic, and vanilla's own
  `Start` gate is the authority boundary.
- **Filter `interactableCategories` in place** — it is a per-stage `CreateInstance` (§4.3), so no
  backup/restore is required, and it is the exact object
  `SceneDirector.GenerateInteractableCardSelection` copies from.
- **Keep the `EnableInteractableRemoval` gate** — that is the dependency's own option and matching it
  preserves existing user expectations; do not re-read the private `BlacklistedSpawns` set.
- **Delete `InteractablePrefix`, `SetQualityInteractableAllowed`, `TryGetSpawnBlacklist`, and
  `SpawnBlacklistField`.** They exist only to serve the removed LUC-prefix seam. After the change,
  `ItemSourceGroups.IsBlocked` is the single source of truth for what is removed, which also removes
  the duplicate-list race described in §6.4.
- **Credit multiplier**: replace `InteractableCreditField` reflection with the property accessor.
  Since `interactableCredit` is generated (`public int interactableCredit { get; set; }`), use
  `AccessTools.Property(typeof(SceneDirector), nameof(SceneDirector.interactableCredit))` with
  `GetSetMethod(true)`/`GetValue`, or scale `ClassicStageInfo.sceneDirectorInteractibleCredits` via
  `DirectorAPI`-style settings. Scaling the credit *before* `SceneDirector.Start` reads it is
  cleaner than a prefix on `PopulateScene`, because `Start` computes
  `interactableCredit = sceneDirectorInteractibleCredits * (0.5f + players * 0.5f)` and `PopulateScene`
  applies `onPopulateCreditMultiplier` afterwards.

### 6.3 Blacklist names

- Matching is `StringComparer.OrdinalIgnoreCase` in the dependency and
  `ToLowerInvariant()` in R2API, so **casing never causes a miss**; it is a consistency issue only.
- Names must equal `SpawnCard.name` of the registered `InteractableSpawnCard`, exactly as
  `R2API.Director.DirectorAPIhelpers.InteractableNames` enumerates them (all lowercase, e.g.
  `iscgoldchest`, `iscduplicatorlarge`, `iscshrinechancesandy`):
  <https://github.com/risk-of-thunder/R2API/blob/master/R2API.Director/DirectorAPIhelpers.cs>.
- `ItemSourceGroups.QualityChests` / `QualityPrinters` in this repo use a mixed casing style
  (`"iscQualityChest1"`, `"iscQualityDuplicatorLarge"`) that does not appear in the vanilla
  `InteractableNames` list — expected, since those cards come from Item Qualities, not vanilla.
  Because of the ignore-case comparers this is harmless, but the strings must still match the
  `name` Item Qualities assigns to its spawned cards. **[UNVERIFIED]** — confirm against
  Item Qualities' registered `InteractableSpawnCard.name` values (e.g. the `iq/` sources or a
  runtime dump) before relying on the quality filter.

### 6.4 Stage timing caveats

1. **`ClassicStageInfo.Start` → `RebuildCards()` must have already run.** `interactableCategories` is
   assigned there. `SceneDirector.Start` runs later in the stage load, so `onPrePopulateSceneServer`
   is always after it. Do not try to filter from a `ClassicStageInfo.Start` prefix — at that point
   `RebuildCards` has not run and `interactableCategories` is stale/null.
2. **`interactableDccsPool` is a dead end for this timing.** It is `private`; mutating it after
   `RebuildCards` has blended it changes nothing for the current stage.
3. **Artifact toggles re-run `RebuildCards`.** `OnArtifactEnabled` / `OnArtifactDisabled` call
   `RebuildCards()` for the MixEnemy and SingleMonsterType artifacts, which re-creates
   `interactableCategories` from the pool and **discards any earlier filter**. That is a second,
   independent reason to filter on every populate rather than once per stage.
4. **`onGenerateInteractableCardSelection` receives a destroyed copy.** A handler may edit it, but
   must not cache it or expect mutations to persist.
5. **`interactableCategories` is per-stage.** Because `DCCSBlender.GetBlendedDCCS` uses
   `ScriptableObject.CreateInstance`, in-place edits do not leak into other stages or subsequent
   runs — unlike mutating a DCCS asset directly, which is precisely the bug R2API's
   `BackupOrRestoreClassicStageInfoToOriginalState` exists to work around.

### 6.6 Multiplayer / server authority

- `SceneDirector.Start` returns unless `NetworkServer.active`, so spawn removal is inherently a
  host-side operation; clients never build the interactable selection.
- Both the vanilla path and the dependency's `MonoBehaviour` handler are server-driven; nothing in
  this design requires a network message. Client-side filters would desync (client sees an
  interactable the host never spawned, or vice versa).
- The existing `InteractableCreditPrefix` already guards with `!NetworkServer.active`, confirming the
  intended authority model. Preserve that guard in the replacement.
- Repeat the `NetworkServer.active` check inside the handler even though vanilla already gates it:
  `SceneDirector.Start` is not provably the only invoker of the static event across game versions.

---

## 7. Summary of findings

1. **Bug**: `PatchInteractables(harmony)` is never invoked from `Install` (removed in commit
   `053ea34`). `FilterInteractableCategories`, `InteractableCategoriesPrefix`,
   `InteractablePrefix`, and `InteractableCreditPrefix` are unreachable; `grep` finds zero call sites
   for `FilterInteractableCategories`.
2. **Secondary bug**: `AccessTools.Field(typeof(SceneDirector), "interactableCredit")` is `null`
   because `interactableCredit` is an auto-property; HarmonyX `AccessTools.Field` does not resolve
   `<...>k__BackingField`. Credit scaling would remain dead even after restoring the call.
3. **Design flaw**: patching `LevelUpChoices.InteractableSpawnHook.OnPrePopulateSceneServer` couples
   this plugin to a dependency's `private` method and, as a prefix, runs before that method's own
   `EnableInteractableRemoval` gate.
4. **Recommended hook**: `SceneDirector.onPrePopulateSceneServer` (vanilla, server-only, fires in
   `SceneDirector.Start` immediately before `PopulateScene()`), filtering
   `ClassicStageInfo.instance.interactableCategories` in place.
5. **R2API alternative**: `DirectorAPI.InteractableActions` /
   `Helpers.RemoveExistingInteractable` is the correct *idiomatic* answer for a mod that already
   depends on R2API.Director; it is viable here only after adding that submodule dependency, and it
   hooks `ClassicStageInfo.Start` rather than `SceneDirector.Start`.
6. **Caveats**: artifact toggles re-run `RebuildCards` and discard one-shot filters; the DCCS is
   per-stage so no backup/restore is needed; match `SpawnCard.name` (ignore-case, vanilla names
   lowercase); spawn removal is host-only.

---

## Sources

Game assemblies (local, decompiled with `ilspycmd`):

- `RoR2.dll` 1.4.x — `RoR2.SceneDirector` (`Start`, `GenerateInteractableCardSelection`,
  `PopulateScene`, `interactableCredit`), `RoR2.ClassicStageInfo` (`Start`, `RebuildCards`,
  `interactableCategories`, `interactableDccsPool`, `GetInteractableDccsPool`), `RoR2.DCCSBlender`
  (`GetBlendedDCCS`). Path:
  `Risk of Rain 2_Data/Managed/RoR2.dll`.
- NuGet `RiskOfRain2.GameLibs` 1.4.1-r.0 (`RoR2.dll` reference assembly) —
  <https://www.nuget.org/packages/RiskOfRain2.GameLibs>

Repository sources:

- `karaeren/LevelUpChoices` — `src/InteractableSpawnHook.cs` (upstream of `luc/src/`):
  <https://github.com/karaeren/LevelUpChoices/blob/master/src/InteractableSpawnHook.cs>
- `karaeren/LevelUpChoices` — `src/LevelUpChoices.cs` (component registration),
  `src/ModConfig.cs` (`EnableInteractableRemoval`):
  <https://github.com/karaeren/LevelUpChoices>
- Local `luc/src/InteractableSpawnHook.cs`, `luc/src/LevelUpChoices.cs`, `luc/src/ModConfig.cs`,
  `src/LevelUpChoicesFixes.cs`, `src/LevelUpChoicesFixes.csproj`, `ThunderstoreContent/manifest.json`.

R2API:

- `R2API.Director/DirectorAPIhelpers.cs` (`RemoveExistingInteractable`,
  `InteractableNames`, `AddNewInteractable`):
  <https://github.com/risk-of-thunder/R2API/blob/master/R2API.Director/DirectorAPIhelpers.cs>
- `R2API.Director/DirectorAPIinternal.cs` (`SetHooks`, `ApplyChangesOnStart`, `ApplyChanges`,
  `ApplyInteractableChanges`, `BackupOrRestoreClassicStageInfoToOriginalState`,
  `PortToNewInteractableSystem`):
  <https://github.com/risk-of-thunder/R2API/blob/master/R2API.Director/DirectorAPIinternal.cs>
- `R2API.Director/DirectorAPIexternal.cs` (`InteractableActions`, `MonsterActions`,
  `StageSettingsActions` documentation):
  <https://github.com/risk-of-thunder/R2API/blob/master/R2API.Director/DirectorAPIexternal.cs>
- `R2API.Director/README.md` (supported events):
  <https://github.com/risk-of-thunder/R2API/blob/master/R2API.Director/README.md>
- R2API.Director package page:
  <https://thunderstore.io/c/riskofrain2/p/RiskofThunder/R2API_Director/>

Harmony:

- HarmonyX `AccessTools.Field(Type, string)` (no backing-field fallback):
  <https://github.com/BepInEx/HarmonyX/blob/master/Harmony/Tools/AccessTools.cs>
- Harmony (pardeike) `AccessTools.Field(Type, string)`:
  <https://github.com/pardeike/Harmony/blob/master/Harmony/Tools/AccessTools.cs>

Risk of Thunder modding wiki:

- Core API changelog 1.3.7 (DCCS blending, `contentSourceMixLimit`):
  <https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/Updating-Your-Mods/Updating-Core-API-Changelog-1.3.7/>
- Common components, events and methods (`SceneDirector` responsibilities):
  <https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/C%23-Programming/Game-Code/Common-Components%2C-Events-and-Methods/>
- Interactables (DirectorAPI registration flow, `DirectorCardHolder`, `InteractableSpawnCard`):
  <https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/Assets/Interactables/>
- Stages: <https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/Assets/Stage/>

## 8. Implementation addendum

The initial recommendation above was based on the vanilla `SceneDirector` event. The shipped
profile log and the installed Item Qualities assembly added two further facts:

1. R2API Director and `LevelUpChoicesFixes` now load successfully in the profile.
2. Item Qualities adds `iscQualityEquipmentBarrel` (displayed in-game as a temporary item
   distributor) from its `ClassicStageInfo.RebuildCards` hook, after the Director pool callback.

The implementation therefore uses both supported stages of the pipeline: the R2API
`InteractableActions` callback filters the DCCS pool, and a `SceneDirector.PopulateScene` prefix
filters the final `interactableCategories` selection after Item Qualities has appended its custom
cards. `iscQualityEquipmentBarrel` is included in the quality-removal group.

The profile log confirms the previous package/import issue was fixed:

```text
Loading [R2API.Director 3.1.0]
Loading [LevelUpChoicesFixes 1.0.0]
```
