using System;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using UnityEngine.Networking;

namespace TeamTayne.LevelUpChoicesFixes;

internal static class RiskOfOptionsIntegration
{
    internal const string PluginGUID = "com.rune580.riskofoptions";

    internal static void TryRegister()
    {
        if (!Chainloader.PluginInfos.ContainsKey(PluginGUID))
            return;

        try
        {
            RegisterOptions();
            Log.Info("Risk of Options settings registered.");
        }
        catch (Exception e)
        {
            Log.Warning($"Risk of Options integration failed; native config remains available: {e.Message}");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void RegisterOptions()
    {
        ModSettingsManager.SetModDescription(
            "LevelUpChoices remains the sole level-up system. Host/server settings are authoritative during multiplayer runs.");

        ModSettingsManager.AddOption(new CheckBoxOption(ConfigState.EnableQualityIntegration, new CheckBoxConfig
        {
            category = "Quality",
            checkIfDisabled = ServerOptionDisabled
        }));
        ModSettingsManager.AddOption(new CheckBoxOption(ConfigState.AllowQualityChests, new CheckBoxConfig
        {
            category = "Quality",
            checkIfDisabled = ServerOptionDisabled
        }));
        ModSettingsManager.AddOption(new SliderOption(ConfigState.QualityChance, new SliderConfig
        {
            category = "Quality",
            min = 0f,
            max = 100f,
            FormatString = "{0:0.#}%",
            checkIfDisabled = ServerOptionDisabled
        }));
        ModSettingsManager.AddOption(new SliderOption(ConfigState.UncommonQualityWeight, new SliderConfig
        {
            category = "Quality",
            min = 0f,
            max = 100f,
            FormatString = "{0:0.#}",
            checkIfDisabled = ServerOptionDisabled
        }));
        ModSettingsManager.AddOption(new SliderOption(ConfigState.RareQualityWeight, new SliderConfig
        {
            category = "Quality",
            min = 0f,
            max = 100f,
            FormatString = "{0:0.#}",
            checkIfDisabled = ServerOptionDisabled
        }));
        ModSettingsManager.AddOption(new SliderOption(ConfigState.EpicQualityWeight, new SliderConfig
        {
            category = "Quality",
            min = 0f,
            max = 100f,
            FormatString = "{0:0.#}",
            checkIfDisabled = ServerOptionDisabled
        }));
        ModSettingsManager.AddOption(new SliderOption(ConfigState.LegendaryQualityWeight, new SliderConfig
        {
            category = "Quality",
            min = 0f,
            max = 100f,
            FormatString = "{0:0.#}",
            checkIfDisabled = ServerOptionDisabled
        }));

        ModSettingsManager.AddOption(new ChoiceOption(ConfigState.RerollRefreshOnLevel, new ChoiceConfig
        {
            category = "Schedule",
            checkIfDisabled = ServerOptionDisabled
        }));
        ModSettingsManager.AddOption(new IntSliderOption(ConfigState.RerollRefreshEveryNLevels, new IntSliderConfig
        {
            category = "Schedule",
            min = 1,
            max = 100,
            formatString = "Every {0} level(s)",
            checkIfDisabled = ServerOptionDisabled
        }));
        ModSettingsManager.AddOption(new IntSliderOption(ConfigState.ItemChoicesEveryNLevels, new IntSliderConfig
        {
            category = "Schedule",
            min = 1,
            max = 100,
            formatString = "Every {0} level(s)",
            checkIfDisabled = ServerOptionDisabled
        }));

        ModSettingsManager.AddOption(new CheckBoxOption(ConfigState.PreserveChests, new CheckBoxConfig
        {
            category = "Interactables",
            checkIfDisabled = ServerOptionDisabled
        }));
        ModSettingsManager.AddOption(new CheckBoxOption(ConfigState.PreservePrinters, new CheckBoxConfig
        {
            category = "Interactables",
            checkIfDisabled = ServerOptionDisabled
        }));
        ModSettingsManager.AddOption(new CheckBoxOption(ConfigState.PreserveShrines, new CheckBoxConfig
        {
            category = "Interactables",
            checkIfDisabled = ServerOptionDisabled
        }));
        ModSettingsManager.AddOption(new CheckBoxOption(ConfigState.PreserveShops, new CheckBoxConfig
        {
            category = "Interactables",
            checkIfDisabled = ServerOptionDisabled
        }));
        ModSettingsManager.AddOption(new CheckBoxOption(ConfigState.PreserveScrappers, new CheckBoxConfig
        {
            category = "Interactables",
            checkIfDisabled = ServerOptionDisabled
        }));
        ModSettingsManager.AddOption(new CheckBoxOption(ConfigState.PreserveCleansePools, new CheckBoxConfig
        {
            category = "Interactables",
            checkIfDisabled = ServerOptionDisabled
        }));
        ModSettingsManager.AddOption(new StepSliderOption(ConfigState.InteractableCreditMultiplier, new StepSliderConfig
        {
            category = "Interactables",
            min = 0f,
            max = 5f,
            increment = 0.1f,
            FormatString = "{0:0.0}x",
            checkIfDisabled = ServerOptionDisabled
        }));
    }

    private static bool ServerOptionDisabled()
    {
        return NetworkClient.active && !NetworkServer.active;
    }
}
