using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RoR2;
using UnityEngine.Networking;
using UnityEngine;

namespace TeamTayne.LevelUpChoicesFixes;

internal static class QualityRuntime
{
    private static readonly object Sync = new();
    private static MethodInfo _toBase;
    private static MethodInfo _getQualityTier;
    private static Type _qualityTierType;
    private static bool _usable;
    private static bool _loggedUnavailable;
    private static bool _loggedPromotionState;
    private static bool _loggedNoVariants;
    private static bool _loggedPromotionEntry;

    internal static bool IsPluginPresent =>
        BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(LevelUpChoicesFixes.ItemQualitiesGuid);
    internal static bool IsAvailable => _usable;

    internal static void TryInitialize()
    {
        lock (Sync)
        {
            if (_usable || !BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(LevelUpChoicesFixes.ItemQualitiesGuid))
                return;
            try
            {
                Assembly assembly = BepInEx.Bootstrap.Chainloader.PluginInfos[LevelUpChoicesFixes.ItemQualitiesGuid].Instance.GetType().Assembly;
                Type catalog = FindCatalogType(assembly);
                _getQualityTier = catalog?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(method => method.Name == "GetQualityTier" &&
                        method.ReturnType.IsEnum &&
                        method.GetParameters().Length == 1 &&
                        method.GetParameters()[0].ParameterType == typeof(ItemIndex));
                _qualityTierType = _getQualityTier?.ReturnType;
                _toBase = _qualityTierType == null ? null : catalog?.GetMethod(
                    "GetItemIndexOfQuality",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(ItemIndex), _qualityTierType },
                    null);
                _usable = _toBase != null && _getQualityTier != null && _qualityTierType != null;
                if (!_usable)
                    LogUnavailable("ItemQualities API signatures were not found.");
                else
                    Log.Info("ItemQualities QualityCatalog API validated.");
            }
            catch (Exception e)
            {
                _usable = false;
                LogUnavailable($"ItemQualities API validation failed: {e.Message}");
            }
        }
    }

    internal static ItemIndex ToBase(ItemIndex item)
    {
        if (item == ItemIndex.None)
            return item;
        TryInitialize();
        if (!_usable)
            return item;
        try
        {
            object none = Enum.ToObject(_qualityTierType, -1);
            return (ItemIndex)_toBase.Invoke(null, new[] { (object)item, none });
        }
        catch (Exception e)
        {
            Disable($"Canonical item conversion failed: {e.Message}");
            return item;
        }
    }

    internal static ItemIndex Promote(ItemIndex baseItem, float luck)
    {
        if (!ConfigState.QualityEnabled || !NetworkServer.active)
            return baseItem;
        TryInitialize();
        if (!_loggedPromotionEntry)
        {
            Log.Info($"Quality promotion reached: apiReady={_usable}, enabled={ConfigState.QualityEnabled}, server={NetworkServer.active}.");
            _loggedPromotionEntry = true;
        }
        if (!_usable || baseItem == ItemIndex.None)
            return baseItem;

        try
        {
            float chance = QualityRollPolicy.EffectiveChance(ConfigState.QualityChanceValue, luck);
            if (!QualityRollPolicy.ShouldPromote(chance, UnityEngine.Random.value))
                return baseItem;

            float[] configuredWeights = ConfigState.QualityWeights;
            List<int> availableTiers = new();
            List<float> availableWeights = new();
            for (int tier = 0; tier < configuredWeights.Length; tier++)
            {
                ItemIndex variant = Variant(baseItem, tier);
                if (variant != baseItem && variant != ItemIndex.None && configuredWeights[tier] > 0f)
                {
                    availableTiers.Add(tier);
                    availableWeights.Add(configuredWeights[tier]);
                }
            }

            if (!_loggedPromotionState)
            {
                Log.Info($"Quality promotion active: chance={chance:0.##}%, luck={luck:0.##}, eligible quality tiers={availableTiers.Count}.");
                _loggedPromotionState = true;
            }
            if (availableTiers.Count == 0)
            {
                if (!_loggedNoVariants)
                {
                    Log.Warning($"ItemQualities returned no variants for base item {baseItem}.");
                    _loggedNoVariants = true;
                }
                return baseItem;
            }

            int selected = QualityRollPolicy.SelectWeightedTier(availableTiers, availableWeights, UnityEngine.Random.value);
            if (selected < 0)
                return baseItem;
            return Variant(baseItem, selected);
        }
        catch (Exception e)
        {
            Disable($"Quality promotion failed closed: {e.Message}");
            return baseItem;
        }
    }

    private static ItemIndex Variant(ItemIndex baseItem, int tier)
    {
        object qualityTier = Enum.ToObject(_qualityTierType, tier);
        return (ItemIndex)_toBase.Invoke(null, new[] { (object)baseItem, qualityTier });
    }

    private static Type FindCatalogType(Assembly preferred)
    {
        Type catalog = preferred?.GetType("ItemQualities.QualityCatalog", false);
        if (catalog != null)
            return catalog;

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly == preferred)
                continue;
            catalog = assembly.GetType("ItemQualities.QualityCatalog", false);
            if (catalog != null)
                return catalog;
        }
        return null;
    }

    private static void Disable(string message)
    {
        _usable = false;
        LogUnavailable(message);
    }

    private static void LogUnavailable(string message)
    {
        if (_loggedUnavailable)
            return;
        _loggedUnavailable = true;
        Log.Warning($"ItemQualities integration disabled: {message}");
    }
}

internal static class QualityRollPolicy
{
    internal static float EffectiveChance(float configuredChance, float luck)
    {
        float chance = Math.Max(0f, Math.Min(100f, configuredChance));
        int rolls = Math.Max(1, 1 + Mathf.FloorToInt(Math.Max(0f, luck)));
        return 100f * (1f - Mathf.Pow(1f - chance / 100f, rolls));
    }

    internal static bool ShouldPromote(float chance, float sample)
    {
        return chance >= 100f || (chance > 0f && sample < chance / 100f);
    }

    internal static int SelectWeightedTier(IReadOnlyList<int> tiers, IReadOnlyList<float> weights, float sample)
    {
        if (tiers == null || weights == null || tiers.Count == 0 || tiers.Count != weights.Count)
            return -1;
        float total = 0f;
        for (int i = 0; i < weights.Count; i++)
            total += Math.Max(0f, weights[i]);
        if (total <= 0f)
            return -1;
        float cursor = Math.Max(0f, Math.Min(0.99999994f, sample)) * total;
        for (int i = 0; i < weights.Count; i++)
        {
            cursor -= Math.Max(0f, weights[i]);
            if (cursor < 0f)
                return tiers[i];
        }
        return tiers[tiers.Count - 1];
    }
}
