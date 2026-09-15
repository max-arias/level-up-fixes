using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using R2API;
using R2API.Networking;
using RoR2;
using R2API.Utils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

namespace TeamTayne.LevelUpChoicesFixes;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency(DirectorAPI.PluginGUID)]
[BepInDependency("karaeren.LevelUpChoices", "1.1.3")]
[BepInDependency(ItemQualitiesGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(RiskOfOptionsIntegration.PluginGUID, BepInDependency.DependencyFlags.SoftDependency)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
public sealed class LevelUpChoicesFixes : BaseUnityPlugin
{
    internal const string PluginGUID = "TeamTayne.LevelUpChoicesFixes";
    internal const string PluginName = "LevelUpChoicesFixes";
    internal const string PluginVersion = "1.0.0";

    internal const string ItemQualitiesGuid = "com.Gorakh.ItemQualities";

    private Harmony _harmony;

    private void Awake()
    {
        Log.Init(Logger);
        ConfigState.Initialize(Config);
        RiskOfOptionsIntegration.TryRegister();
        ConfigState.RegisterNetworkMessage();
        _harmony = new Harmony(PluginGUID);
        IntegrationPatches.Install(_harmony);
        Log.Info("LevelUpChoicesFixes initialized; LevelUpChoices remains the sole level-up system.");
    }

    private void OnDestroy()
    {
        ConfigState.UnregisterNetworkMessage();
        try
        {
            LevelUpChoices.GamePauseManager.ForceReset();
        }
        catch (Exception e)
        {
            Log.Warning($"Pause cleanup failed during plugin teardown: {e.Message}");
        }
        IntegrationPatches.Uninstall();
        _harmony?.UnpatchSelf();
    }
}

internal static class Log
{
    private static BepInEx.Logging.ManualLogSource _source;
    internal static void Init(BepInEx.Logging.ManualLogSource source) => _source = source;
    internal static void Info(object message) => _source?.LogInfo(message);
    internal static void Warning(object message) => _source?.LogWarning(message);
    internal static void Error(object message) => _source?.LogError(message);
}

internal static class IntegrationPatches
{
    private static readonly FieldInfo PlayerStatesField = AccessTools.Field(typeof(LevelUpChoices.LevelUpManager), "playerStates");
    private static readonly FieldInfo CurrentOptionsField = AccessTools.Field(typeof(LevelUpChoices.LevelUpManager.PlayerState), "CurrentOptions");
    private static readonly FieldInfo CurrentSynergiesField = AccessTools.Field(typeof(LevelUpChoices.LevelUpManager.PlayerState), "CurrentSynergies");
    private static readonly FieldInfo SelectionTokensField = AccessTools.Field(typeof(LevelUpChoices.LevelUpManager.PlayerState), "SelectionTokens");
    private static readonly FieldInfo DropTableTiersField = AccessTools.Field(typeof(LevelUpChoices.PlayerDropTable), "_tiers");
    private static readonly FieldInfo DropTableWeightsField = AccessTools.Field(typeof(LevelUpChoices.PlayerDropTable), "_weights");
    private static readonly FieldInfo DropTableTierCountsField = AccessTools.Field(typeof(LevelUpChoices.PlayerDropTable), "_tierCounts");
    private static readonly FieldInfo DropTableLastTokensField = AccessTools.Field(typeof(LevelUpChoices.PlayerDropTable), "_lastCalculatedTokens");
    private static readonly FieldInfo InteractableCreditField = AccessTools.Field(typeof(SceneDirector), "interactableCredit");
    private static readonly Dictionary<NetworkInstanceId, int> PendingGuaranteedQualityBatches = new();
    private static ItemIndex _rerollBaseItem = ItemIndex.None;
    private static ItemIndex _rerollOriginalItem = ItemIndex.None;
    private static Run _pendingQualityRun;
    internal static void Install(Harmony harmony)
    {
        PatchInitialize(harmony);
        PatchRoll(harmony);
        PatchRerollAndBanish(harmony);
        PatchLevelSchedule(harmony);
        PatchPause(harmony);
        PatchExperience(harmony);
        PatchInteractables(harmony);
        QualityRuntime.TryInitialize();
    }

    internal static void Uninstall()
    {
        DirectorAPI.InteractableActions -= FilterInteractablePool;
    }

    private static void PatchInitialize(Harmony harmony)
    {
        MethodInfo method = AccessTools.Method(typeof(LevelUpChoices.PlayerDropTable), "Initialize", new[] { typeof(bool) });
        if (method == null || DropTableTiersField == null || DropTableWeightsField == null || DropTableTierCountsField == null)
        {
            Log.Warning("PlayerDropTable.Initialize seam is unsupported; blacklist and WorldUnique filtering disabled.");
            return;
        }
        harmony.Patch(method, postfix: new HarmonyMethod(typeof(IntegrationPatches), nameof(PlayerDropTableInitializePostfix)));
    }

    private static void PatchRoll(Harmony harmony)
    {
        MethodInfo method = AccessTools.Method(typeof(LevelUpChoices.LevelUpManager), "RollSingleSlot");
        if (!HasParameters(method, typeof(NetworkInstanceId), typeof(LevelUpChoices.LevelUpManager.PlayerState), typeof(List<ItemIndex>)) ||
            method.ReturnType != typeof(ValueTuple<ItemIndex, ItemIndex>))
        {
            Log.Warning("LevelUpManager.RollSingleSlot seam is unsupported; Quality promotion and canonical exclusions disabled.");
            return;
        }
        harmony.Patch(method,
            prefix: new HarmonyMethod(typeof(IntegrationPatches), nameof(RollSingleSlotPrefix)),
            postfix: new HarmonyMethod(typeof(IntegrationPatches), nameof(RollSingleSlotPostfix)));

        MethodInfo rollItems = AccessTools.Method(typeof(LevelUpChoices.LevelUpManager), "RollItemsForPlayer");
        if (HasParameters(rollItems, typeof(NetworkInstanceId)))
            harmony.Patch(rollItems, postfix: new HarmonyMethod(typeof(IntegrationPatches), nameof(RollItemsForPlayerPostfix)));
        else
            Log.Warning("LevelUpManager.RollItemsForPlayer seam is unsupported; guaranteed quality choices disabled.");
    }

    private static void PatchRerollAndBanish(Harmony harmony)
    {
        MethodInfo reroll = AccessTools.Method(typeof(LevelUpChoices.LevelUpManager), "HandlePlayerReroll");
        MethodInfo banish = AccessTools.Method(typeof(LevelUpChoices.LevelUpManager), "HandlePlayerBanish");
        MethodInfo remove = AccessTools.Method(typeof(LevelUpChoices.PlayerDropTable), "Remove", new[] { typeof(ItemIndex) });
        if (!HasParameters(reroll, typeof(NetworkInstanceId), typeof(int)) || !HasParameters(remove, typeof(ItemIndex)))
            Log.Warning("Reroll/banish seams are unsupported; duplicate-item protection disabled.");
        else
        {
            harmony.Patch(reroll,
                prefix: new HarmonyMethod(typeof(IntegrationPatches), nameof(RerollPrefix)),
                postfix: new HarmonyMethod(typeof(IntegrationPatches), nameof(RerollPostfix)));
            harmony.Patch(remove, prefix: new HarmonyMethod(typeof(IntegrationPatches), nameof(RemovePrefix)));
        }
        if (!HasParameters(banish, typeof(NetworkInstanceId), typeof(int)))
            Log.Warning("HandlePlayerBanish seam is unsupported; banish canonicalization disabled.");
    }

    private static void PatchLevelSchedule(Harmony harmony)
    {
        MethodInfo method = AccessTools.Method(typeof(LevelUpChoices.LevelUpManager), "OnLevelUp", new[] { typeof(uint) });
        if (!HasParameters(method, typeof(uint)))
        {
            Log.Warning("LevelUpManager.OnLevelUp seam is unsupported; schedule and level reroll refresh disabled.");
            return;
        }
        harmony.Patch(method, prefix: new HarmonyMethod(typeof(IntegrationPatches), nameof(OnLevelUpPrefix)));
    }

    private static void PatchPause(Harmony harmony)
    {
        MethodInfo enable = AccessTools.Method(typeof(RoR2.UI.PauseScreenController), "OnEnable");
        MethodInfo disable = AccessTools.Method(typeof(RoR2.UI.PauseScreenController), "OnDisable");
        MethodInfo hide = AccessTools.Method(typeof(LevelUpChoices.UI.ItemSelectUI), "Hide");
        if (HasParameters(enable))
            harmony.Patch(enable, prefix: new HarmonyMethod(typeof(IntegrationPatches), nameof(PauseScreenEnablePrefix)));
        if (HasParameters(disable))
            harmony.Patch(disable, postfix: new HarmonyMethod(typeof(IntegrationPatches), nameof(PauseScreenDisablePostfix)));
        if (HasParameters(hide))
            harmony.Patch(hide, postfix: new HarmonyMethod(typeof(IntegrationPatches), nameof(ItemSelectHidePostfix)));
        if (!HasParameters(enable) || !HasParameters(disable) || !HasParameters(hide))
            Log.Warning("Pause/UI seams are unsupported; defensive pause restoration is partially disabled.");
    }
    private static void PatchInteractables(Harmony harmony)
    {
        DirectorAPI.InteractableActions += FilterInteractablePool;

        MethodInfo populate = AccessTools.Method(typeof(SceneDirector), "PopulateScene");
        if (populate != null && InteractableCreditField != null)
            harmony.Patch(populate, prefix: new HarmonyMethod(typeof(IntegrationPatches), nameof(InteractableCreditPrefix)));
        else if (Math.Abs(ConfigState.InteractableCreditMultiplier.Value - 1f) > 0.001f)
            Log.Warning("SceneDirector credit seam is unsupported; credit scaling disabled.");
    }

    private static void FilterInteractablePool(DccsPool interactablesDccsPool, DirectorAPI.StageInfo _)
    {
        if (!LevelUpChoices.ModConfig.IsModEnabled ||
            !LevelUpChoices.ModConfig.EnableInteractableRemoval.Value ||
            !interactablesDccsPool)
            return;

        DirectorAPI.Helpers.ForEachPoolEntryInDccsPool(interactablesDccsPool, poolEntry =>
        {
            if (!poolEntry.dccs)
                return;
            for (int i = 0; i < poolEntry.dccs.categories.Length; i++)
            {
                DirectorCardCategorySelection.Category category = poolEntry.dccs.categories[i];
                DirectorCard[] filteredCards = category.cards.Where(card =>
                    card == null || card.spawnCard == null || !ItemSourceGroups.IsBlocked(card.spawnCard.name)).ToArray();
                if (filteredCards.Length == category.cards.Length)
                    continue;

                if (filteredCards.Length == 0)
                    category.selectionWeight = 0f;
                category.cards = filteredCards;
                poolEntry.dccs.categories[i] = category;
            }
        });
    }

    private static void PatchExperience(Harmony harmony)
    {
        MethodInfo rebuild = AccessTools.Method(typeof(LevelUpChoices.ExperienceHook), "RebuildCustomTable");
        if (rebuild == null)
        {
            Log.Warning("ExperienceHook.RebuildCustomTable seam is unsupported; XP curve controls disabled.");
            return;
        }
        harmony.Patch(rebuild, postfix: new HarmonyMethod(typeof(IntegrationPatches), nameof(RebuildXpPostfix)));
    }

    private static void PlayerDropTableInitializePostfix(LevelUpChoices.PlayerDropTable __instance)
    {
        try
        {
            Dictionary<ItemIndex, ItemTier> tiers = DropTableTiersField.GetValue(__instance) as Dictionary<ItemIndex, ItemTier>;
            Dictionary<ItemIndex, float> weights = DropTableWeightsField.GetValue(__instance) as Dictionary<ItemIndex, float>;
            Dictionary<ItemTier, int> counts = DropTableTierCountsField.GetValue(__instance) as Dictionary<ItemTier, int>;
            if (tiers == null || weights == null || counts == null)
                return;

            List<ItemIndex> removed = new();
            foreach (ItemIndex item in tiers.Keys.ToArray())
            {
                ItemDef def = ItemCatalog.GetItemDef(item);
                if ((QualityRuntime.IsPluginPresent && def != null && def.ContainsTag(ItemTag.WorldUnique)) ||
                    ItemBlacklist.Contains(def?.name))
                    removed.Add(item);
            }
            foreach (ItemIndex item in removed)
            {
                if (tiers.TryGetValue(item, out ItemTier tier))
                {
                    tiers.Remove(item);
                    weights.Remove(item);
                    if (counts.ContainsKey(tier))
                        counts[tier] = Math.Max(0, counts[tier] - 1);
                }
            }
            DropTableLastTokensField?.SetValue(__instance, -1);
            __instance.RecalculateWeights(0);
        }
        catch (Exception e)
        {
            Log.Error($"Player drop table filtering failed closed: {e.Message}");
        }
    }

    private static void RollSingleSlotPrefix(List<ItemIndex> exclude)
    {
        CanonicalizeInPlace(exclude);
        if (_rerollBaseItem != ItemIndex.None && !exclude.Contains(_rerollBaseItem))
            exclude.Add(_rerollBaseItem);
    }

    private static void RollSingleSlotPostfix(
        NetworkInstanceId netId,
        ref ValueTuple<ItemIndex, ItemIndex> __result)
    {
        ItemIndex baseItem = QualityRuntime.ToBase(__result.Item1);
        if (baseItem == ItemIndex.None && _rerollOriginalItem != ItemIndex.None)
            __result = new ValueTuple<ItemIndex, ItemIndex>(_rerollOriginalItem, ItemIndex.None);
        else if (baseItem != ItemIndex.None && ConfigState.QualityEnabled && NetworkServer.active)
            __result = new ValueTuple<ItemIndex, ItemIndex>(
                QualityRuntime.Promote(baseItem, GetPlayerLuck(netId)),
                __result.Item2);
    }

    private static void RollItemsForPlayerPostfix(LevelUpChoices.LevelUpManager __instance, NetworkInstanceId netId)
    {
        if (!ConfigState.QualityEnabled || ConfigState.GuaranteedQualityEveryValue <= 0)
        {
            PendingGuaranteedQualityBatches.Remove(netId);
            return;
        }
        if (!PendingGuaranteedQualityBatches.TryGetValue(netId, out int pendingBatches) || pendingBatches <= 0)
            return;
        object state = GetPlayerState(__instance, netId);
        if (state == null || CurrentOptionsField?.GetValue(state) is not IList options || options.Count == 0)
            return;

        int configuredCount = ConfigState.GuaranteedQualityChoicesValue;
        int guaranteedCount = Math.Min(configuredCount, options.Count);
        float luck = GetPlayerLuck(netId);
        for (int i = 0; i < guaranteedCount; i++)
        {
            if (options[i] is not ItemIndex item)
                continue;
            ItemIndex baseItem = QualityRuntime.ToBase(item);
            options[i] = QualityRuntime.PromoteGuaranteed(baseItem, luck);
        }

        if (guaranteedCount < configuredCount)
            Log.Warning($"Guaranteed quality choice count {configuredCount} was capped at {guaranteedCount} by the upstream option count.");

        if (pendingBatches == 1)
            PendingGuaranteedQualityBatches.Remove(netId);
        else
            PendingGuaranteedQualityBatches[netId] = pendingBatches - 1;

        SyncOptions(__instance, netId);
        UpdateLocalOptions(__instance, netId, options, state);
    }

    private static void RerollPrefix(LevelUpChoices.LevelUpManager __instance, NetworkInstanceId netId, int slotIndex)
    {
        _rerollBaseItem = ItemIndex.None;
        _rerollOriginalItem = ItemIndex.None;
        object state = GetPlayerState(__instance, netId);
        IList options = (IList)CurrentOptionsField?.GetValue(state);
        if (options == null || slotIndex < 0 || slotIndex >= options.Count)
            return;
        _rerollOriginalItem = (ItemIndex)options[slotIndex];
        _rerollBaseItem = QualityRuntime.ToBase(_rerollOriginalItem);
    }

    private static void RerollPostfix()
    {
        _rerollBaseItem = ItemIndex.None;
        _rerollOriginalItem = ItemIndex.None;
    }

    private static void RemovePrefix(ref ItemIndex item) => item = QualityRuntime.ToBase(item);

    private static void OnLevelUpPrefix(LevelUpChoices.LevelUpManager __instance, uint newLevel)
    {
        QueueGuaranteedQualityBatch(newLevel);
    }

    private static void QueueGuaranteedQualityBatch(uint newLevel)
    {
        if (!NetworkServer.active)
            return;
        if (_pendingQualityRun != Run.instance)
        {
            PendingGuaranteedQualityBatches.Clear();
            _pendingQualityRun = Run.instance;
        }

        int interval = ConfigState.GuaranteedQualityEveryValue;
        if (!ConfigState.QualityEnabled || interval <= 0 || newLevel == 0 || newLevel % (uint)interval != 0)
            return;

        foreach (PlayerCharacterMasterController player in PlayerCharacterMasterController.instances)
        {
            if (!player.networkUser)
                continue;
            NetworkInstanceId id = player.networkUser.netId;
            PendingGuaranteedQualityBatches[id] =
                PendingGuaranteedQualityBatches.TryGetValue(id, out int pending) ? pending + 1 : 1;
        }
    }


    private static void PauseScreenEnablePrefix()
    {
        if (!LevelUpChoices.GamePauseManager.IsPausedByUs)
            return;
        try
        {
            LevelUpChoices.UI.ItemSelectUI.Instance?.Hide();
            LevelUpChoices.GamePauseManager.ForceReset();
        }
        catch (Exception e)
        {
            Log.Warning($"Pause handoff cleanup failed: {e.Message}");
            LevelUpChoices.GamePauseManager.ForceReset();
        }
    }

    private static void PauseScreenDisablePostfix()
    {
        if (LevelUpChoices.GamePauseManager.IsPausedByUs)
            LevelUpChoices.GamePauseManager.ForceReset();
    }

    private static void ItemSelectHidePostfix()
    {
        if (LevelUpChoices.GamePauseManager.IsPausedByUs)
            LevelUpChoices.GamePauseManager.ForceReset();
    }



    private static void InteractableCreditPrefix(SceneDirector __instance)
    {
        if (!NetworkServer.active || Math.Abs(ConfigState.InteractableCreditMultiplierValue - 1f) < 0.001f)
            return;
        object value = InteractableCreditField.GetValue(__instance);
        if (value is float credit)
            InteractableCreditField.SetValue(__instance, credit * ConfigState.InteractableCreditMultiplierValue);
        else if (value is int intCredit)
            InteractableCreditField.SetValue(__instance, (int)Math.Max(0, intCredit * ConfigState.InteractableCreditMultiplierValue));
    }

    private static void RebuildXpPostfix(object __instance)
    {
        if (!LevelUpChoices.ModConfig.EnableCustomLevelSystemValue)
            return;
        FieldInfo tableField = AccessTools.Field(__instance.GetType(), "customExperienceTable");
        FieldInfo naturalCapField = AccessTools.Field(__instance.GetType(), "customNaturalLevelCap");
        FieldInfo hardCapField = AccessTools.Field(__instance.GetType(), "customHardExpCap");
        if (tableField == null || naturalCapField == null || hardCapField == null)
            return;

        int maxLevel = Math.Max(2, LevelUpChoices.ModConfig.MaxLevelValue);
        double starting = Math.Max(0d, ConfigState.StartingXp.Value);
        double scaling = Math.Max(0d, ConfigState.ExponentialXpScaling.Value);
        List<ulong> table = new() { 0UL, 0UL };
        double cumulative = 0d;
        for (uint level = 2; level <= maxLevel; level++)
        {
            double step = ConfigState.XpCurve.Value == XpCurveMode.Exponential
                ? starting * Math.Pow(scaling, level - 2)
                : starting * (1d + scaling * (level - 2));
            cumulative = Math.Min(ulong.MaxValue, cumulative + Math.Max(0d, step));
            table.Add((ulong)cumulative);
        }
        tableField.SetValue(__instance, table.ToArray());
        naturalCapField.SetValue(__instance, (uint)(table.Count - 1));
        hardCapField.SetValue(__instance, table[table.Count - 1]);
    }


    private static bool HasParameters(MethodInfo method, params Type[] expected)
    {
        if (method == null)
            return false;
        ParameterInfo[] actual = method.GetParameters();
        if (actual.Length != expected.Length)
            return false;
        for (int i = 0; i < expected.Length; i++)
        {
            if (actual[i].ParameterType != expected[i])
                return false;
        }
        return true;
    }
    private static IEnumerable<DictionaryEntry> EnumerateStates(LevelUpChoices.LevelUpManager manager)
    {
        if (PlayerStatesField?.GetValue(manager) is not IDictionary states)
            yield break;
        foreach (DictionaryEntry state in states)
            yield return state;
    }

    private static object GetPlayerState(LevelUpChoices.LevelUpManager manager, NetworkInstanceId netId)
    {
        if (PlayerStatesField?.GetValue(manager) is IDictionary states && states.Contains(netId))
            return states[netId];
        return null;
    }

    private static void UpdateLocalOptions(
        LevelUpChoices.LevelUpManager manager,
        NetworkInstanceId netId,
        IList options,
        object state)
    {
        if (!NetworkUser.readOnlyLocalPlayersList.Any(user => user.netId == netId))
            return;

        List<PickupIndex> pickups = new();
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i] is ItemIndex item)
                pickups.Add(PickupCatalog.FindPickupIndex(item));
        }

        List<ItemIndex> synergies = CurrentSynergiesField?.GetValue(state) as List<ItemIndex>;
        manager.UpdateAvailableItems(pickups, synergies);
    }


    private static float GetPlayerLuck(NetworkInstanceId netId)
    {
        foreach (PlayerCharacterMasterController player in PlayerCharacterMasterController.instances)
        {
            if (player.networkUser && player.networkUser.netId == netId)
                return player.master ? player.master.luck : 0f;
        }
        return 0f;
    }

    private static void SyncState(LevelUpChoices.LevelUpManager manager, NetworkInstanceId id)
    {
        AccessTools.Method(typeof(LevelUpChoices.LevelUpManager), "SyncState")?.Invoke(manager, new object[] { id });
    }

    private static void SyncOptions(LevelUpChoices.LevelUpManager manager, NetworkInstanceId id)
    {
        AccessTools.Method(typeof(LevelUpChoices.LevelUpManager), "SyncOptions")?.Invoke(manager, new object[] { id });
    }

    private static void CanonicalizeInPlace(List<ItemIndex> items)
    {
        for (int i = 0; i < items.Count; i++)
            items[i] = QualityRuntime.ToBase(items[i]);
    }

    private static class ItemBlacklist
    {
        internal static bool Contains(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            return ConfigState.ItemBlacklistValue.Split(',').Any(raw =>
                string.Equals(raw.Trim(), name, StringComparison.OrdinalIgnoreCase));
        }
    }
}

internal static class ItemSourceGroups
{
    private static readonly string[] Chests = { "isccasinochest", "isccategorychestdamage", "isccategorychesthealing", "isccategorychestutility", "iscchest1", "iscchest1stealthed", "iscchest2", "iscgoldchest", "isclunarchest", "isccategorychest2damage", "isccategorychest2healing", "isccategorychest2utility" };
    internal static readonly string[] QualityChests = { "iscQualityChest1", "iscQualityChest2" };
    internal static readonly string[] QualityPrinters = { "iscQualityDuplicator", "iscQualityDuplicatorLarge", "iscQualityDuplicatorMilitary", "iscQualityDuplicatorWild" };
    private static readonly string[] Printers = { "iscduplicator", "iscduplicatorlarge", "iscduplicatormilitary", "iscduplicatorwild" };
    private static readonly string[] Shrines = { "iscshrineblood", "iscshrinebloodsandy", "iscshrinebloodsnowy", "iscshrinechance", "iscshrinechancesandy", "iscshrinechancesnowy", "iscshrinecleanse", "iscshrinecleansesandy", "iscshrinecleansesnowy", "iscshrinecombat", "iscshrinecombatsandy", "iscshrinecombatsnowy", "iscshrinerestack", "iscshrinerestacksandy", "iscshrinerestacksnowy" };
    private static readonly string[] Shops = { "isctripleshop", "isctripleshoplarge" };
    private static readonly string[] Scrappers = { "iscscrapper" };

    internal static bool IsBlocked(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        return Contains(Chests, name) || Contains(Printers, name) || Contains(Shrines, name) ||
            Contains(Shops, name) || Contains(Scrappers, name) ||
            ConfigState.RemoveQualityInteractablesValue &&
            QualityRuntime.IsPluginPresent &&
            (Contains(QualityChests, name) || Contains(QualityPrinters, name));
    }

    private static bool Contains(string[] names, string name)
    {
        foreach (string candidate in names)
        {
            if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

}
