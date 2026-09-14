using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using BepInEx;
using BepInEx.Bootstrap;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using UnityEngine.Networking;

namespace TeamTayne.LevelUpChoicesFixes;

internal static class RiskOfOptionsIntegration
{
    private const string IconResourceName = "TeamTayne.LevelUpChoicesFixes.icon.png";
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
        Sprite icon = LoadIcon();
        if (icon != null)
            ModSettingsManager.SetModIcon(icon);
        else
            Log.Warning("Risk of Options icon resource was not found or could not be decoded.");

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
        ModSettingsManager.AddOption(new IntSliderOption(ConfigState.GuaranteedQualityEveryNLevels, new IntSliderConfig
        {
            category = "Quality",
            min = 0,
            max = 100,
            formatString = "{0}",
            checkIfDisabled = ServerOptionDisabled
        }));
        ModSettingsManager.AddOption(new IntSliderOption(ConfigState.GuaranteedQualityChoiceCount, new IntSliderConfig
        {
            category = "Quality",
            min = 1,
            max = 10,
            formatString = "{0}",
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
            formatString = "{0}",
            checkIfDisabled = ServerOptionDisabled
        }));
        ModSettingsManager.AddOption(new IntSliderOption(ConfigState.ItemChoicesEveryNLevels, new IntSliderConfig
        {
            category = "Schedule",
            min = 1,
            max = 100,
            formatString = "{0}",
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

    private static Sprite LoadIcon()
    {
        Assembly assembly = typeof(RiskOfOptionsIntegration).Assembly;
        using (Stream stream = assembly.GetManifestResourceStream(IconResourceName))
        {
            if (stream == null)
                return null;

            using (MemoryStream buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, buffer.ToArray(), true))
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                texture.name = "LevelUpChoicesFixesIcon";
                return Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }
        }
    }

    private static bool ServerOptionDisabled()
    {
        return NetworkClient.active && !NetworkServer.active;
    }
}
