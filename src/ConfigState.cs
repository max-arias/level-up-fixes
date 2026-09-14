using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine.Networking;

namespace TeamTayne.LevelUpChoicesFixes;

internal enum RerollRefreshMode
{
    Off,
    ToStartingValue,
    AddOne
}

internal enum XpCurveMode
{
    Exponential,
    Linear
}

internal static class ConfigState
{
    private const string ServerSection = "Server";
    private static bool _hasServerOverride;
    private static string _serverBlacklist = string.Empty;

    internal static ConfigEntry<bool> EnableQualityIntegration { get; private set; }
    internal static ConfigEntry<bool> RemoveQualityInteractables { get; private set; }
    internal static ConfigEntry<float> QualityChance { get; private set; }
    internal static ConfigEntry<float> UncommonQualityWeight { get; private set; }
    internal static ConfigEntry<float> RareQualityWeight { get; private set; }
    internal static ConfigEntry<float> EpicQualityWeight { get; private set; }
    internal static ConfigEntry<float> LegendaryQualityWeight { get; private set; }
    internal static ConfigEntry<int> GuaranteedQualityEveryNLevels { get; private set; }
    internal static ConfigEntry<int> GuaranteedQualityChoiceCount { get; private set; }
    internal static ConfigEntry<string> ItemBlacklist { get; private set; }
    internal static ConfigEntry<RerollRefreshMode> RerollRefreshOnLevel { get; private set; }
    internal static ConfigEntry<int> RerollRefreshEveryNLevels { get; private set; }
    internal static ConfigEntry<int> ItemChoicesEveryNLevels { get; private set; }
    internal static ConfigEntry<float> InteractableCreditMultiplier { get; private set; }
    internal static ConfigEntry<XpCurveMode> XpCurve { get; private set; }
    internal static ConfigEntry<float> StartingXp { get; private set; }
    internal static ConfigEntry<float> ExponentialXpScaling { get; private set; }

    internal static bool QualityEnabled => Value(EnableQualityIntegration);
    internal static bool RemoveQualityInteractablesValue => Value(RemoveQualityInteractables);
    internal static float QualityChanceValue => ClampPercent(Value(QualityChance));
    internal static float[] QualityWeights => new[]
    {
        Math.Max(0f, Value(UncommonQualityWeight)),
        Math.Max(0f, Value(RareQualityWeight)),
        Math.Max(0f, Value(EpicQualityWeight)),
        Math.Max(0f, Value(LegendaryQualityWeight))
    };
    internal static int GuaranteedQualityEveryValue => Math.Max(0, Value(GuaranteedQualityEveryNLevels));
    internal static int GuaranteedQualityChoicesValue => Math.Max(1, Value(GuaranteedQualityChoiceCount));
    internal static string ItemBlacklistValue => _hasServerOverride ? _serverBlacklist : ItemBlacklist.Value;
    internal static RerollRefreshMode RerollRefreshModeValue => Value(RerollRefreshOnLevel);
    internal static int RerollRefreshEveryValue => Math.Max(1, Value(RerollRefreshEveryNLevels));
    internal static int ChoicesEveryValue => Math.Max(1, Value(ItemChoicesEveryNLevels));
    internal static float InteractableCreditMultiplierValue => Math.Max(0f, Value(InteractableCreditMultiplier));

    internal static void Initialize(ConfigFile config)
    {
        EnableQualityIntegration = config.Bind(ServerSection, "Enable Quality Integration", true,
            "Promote LevelUpChoices base items to Item Qualities variants after the original roll.");
        RemoveQualityInteractables = config.Bind(ServerSection, "Remove Quality Interactables", true,
            "Remove Item Qualities chests and printers when LevelUpChoices removes item sources.");
        QualityChance = config.Bind(ServerSection, "Quality Chance", 4f,
            "Percent chance for a level-up item to receive a quality. Host value is authoritative.");
        UncommonQualityWeight = config.Bind(ServerSection, "Uncommon Quality Weight", 70f,
            "Relative weight for Uncommon quality variants.");
        RareQualityWeight = config.Bind(ServerSection, "Rare Quality Weight", 20f,
            "Relative weight for Rare quality variants.");
        EpicQualityWeight = config.Bind(ServerSection, "Epic Quality Weight", 8f,
            "Relative weight for Epic quality variants.");
        LegendaryQualityWeight = config.Bind(ServerSection, "Legendary Quality Weight", 2f,
            "Relative weight for Legendary quality variants.");
        GuaranteedQualityEveryNLevels = config.Bind(ServerSection, "Guaranteed Quality Every N Levels", 5,
            new ConfigDescription(
                "Queue one guaranteed-quality choice set at this level interval. Set to 0 to disable.",
                new AcceptableValueRange<int>(0, 100)));
        GuaranteedQualityChoiceCount = config.Bind(ServerSection, "Guaranteed Quality Choice Count", 3,
            new ConfigDescription(
                "Number of quality choices in each guaranteed-quality set. Capped by the upstream choice count.",
                new AcceptableValueRange<int>(1, 10)));
        ItemBlacklist = config.Bind(ServerSection, "Item Blacklist", "DefensiveMicrobots",
            "Comma-separated item names that are removed from every LevelUpChoices player pool.");
        RerollRefreshOnLevel = config.Bind(ServerSection, "Reroll Refresh On Level", RerollRefreshMode.Off,
            "At the configured interval, do nothing, reset rerolls to the starting value, or add one reroll.");
        RerollRefreshEveryNLevels = config.Bind(ServerSection, "Reroll Refresh Every N Levels", 1,
            "Level interval used by Reroll Refresh On Level.");
        ItemChoicesEveryNLevels = config.Bind(ServerSection, "Item Choices Every N Levels", 1,
            "Grant a level-up choice token every N levels. Existing unspent choices are preserved.");
        InteractableCreditMultiplier = config.Bind(ServerSection, "Interactable Credit Multiplier", 1f,
            "Multiplier applied to the original interactable credit budget; 1 leaves it unchanged.");
        XpCurve = config.Bind(ServerSection, "XP Curve", XpCurveMode.Exponential,
            "Release 4 XP curve: Exponential or Linear.");
        StartingXp = config.Bind(ServerSection, "Starting XP", 20f,
            "XP required for the first custom level step.");
        ExponentialXpScaling = config.Bind(ServerSection, "XP Scaling", 1.55f,
            "Exponential multiplier, or linear additive rate when XP Curve is Linear.");
    }

    internal static void RegisterNetworkMessage()
    {
        NetworkingAPI.RegisterMessageType<SyncFixConfig>();
        Run.onRunStartGlobal += OnRunStart;
        Run.onRunDestroyGlobal += OnRunDestroy;
        SubscribeServerEntries();
    }

    internal static void UnregisterNetworkMessage()
    {
        Run.onRunStartGlobal -= OnRunStart;
        Run.onRunDestroyGlobal -= OnRunDestroy;
        UnsubscribeServerEntries();
    }

    private static void SubscribeServerEntries()
    {
        RemoveQualityInteractables.SettingChanged += OnServerSettingChanged;
        QualityChance.SettingChanged += OnServerSettingChanged;
        UncommonQualityWeight.SettingChanged += OnServerSettingChanged;
        RareQualityWeight.SettingChanged += OnServerSettingChanged;
        EpicQualityWeight.SettingChanged += OnServerSettingChanged;
        LegendaryQualityWeight.SettingChanged += OnServerSettingChanged;
        GuaranteedQualityEveryNLevels.SettingChanged += OnServerSettingChanged;
        GuaranteedQualityChoiceCount.SettingChanged += OnServerSettingChanged;
        ItemBlacklist.SettingChanged += OnServerSettingChanged;
        RerollRefreshOnLevel.SettingChanged += OnServerSettingChanged;
        RerollRefreshEveryNLevels.SettingChanged += OnServerSettingChanged;
        ItemChoicesEveryNLevels.SettingChanged += OnServerSettingChanged;
        InteractableCreditMultiplier.SettingChanged += OnServerSettingChanged;
        XpCurve.SettingChanged += OnServerSettingChanged;
        StartingXp.SettingChanged += OnServerSettingChanged;
        ExponentialXpScaling.SettingChanged += OnServerSettingChanged;
    }

    private static void UnsubscribeServerEntries()
    {
        RemoveQualityInteractables.SettingChanged -= OnServerSettingChanged;
        QualityChance.SettingChanged -= OnServerSettingChanged;
        UncommonQualityWeight.SettingChanged -= OnServerSettingChanged;
        RareQualityWeight.SettingChanged -= OnServerSettingChanged;
        EpicQualityWeight.SettingChanged -= OnServerSettingChanged;
        LegendaryQualityWeight.SettingChanged -= OnServerSettingChanged;
        GuaranteedQualityEveryNLevels.SettingChanged -= OnServerSettingChanged;
        GuaranteedQualityChoiceCount.SettingChanged -= OnServerSettingChanged;
        ItemBlacklist.SettingChanged -= OnServerSettingChanged;
        RerollRefreshOnLevel.SettingChanged -= OnServerSettingChanged;
        RerollRefreshEveryNLevels.SettingChanged -= OnServerSettingChanged;
        ItemChoicesEveryNLevels.SettingChanged -= OnServerSettingChanged;
        InteractableCreditMultiplier.SettingChanged -= OnServerSettingChanged;
        XpCurve.SettingChanged -= OnServerSettingChanged;
        StartingXp.SettingChanged -= OnServerSettingChanged;
        ExponentialXpScaling.SettingChanged -= OnServerSettingChanged;
    }

    private static void OnServerSettingChanged(object sender, EventArgs _)
    {
        if (NetworkServer.active && Run.instance != null)
            SendToClients();
    }

    private static void SendToClients()
    {
        new SyncFixConfig(true).Send(NetworkDestination.Clients);
    }

    private static void OnRunStart(Run _)
    {
        if (NetworkServer.active)
            SendToClients();
    }


    private static void OnRunDestroy(Run _)
    {
        _hasServerOverride = false;
        _serverBlacklist = string.Empty;
        SyncFixConfig.ClearOverride();
    }

    private static T Value<T>(ConfigEntry<T> entry)
    {
        return _hasServerOverride ? SyncFixConfig.GetOverride(entry) : entry.Value;
    }

    private static float ClampPercent(float value) => Math.Max(0f, Math.Min(100f, value));

    internal sealed class SyncFixConfig : INetMessage
    {
        private bool _enableQuality;
        private bool _removeQualityInteractables;
        private float _qualityChance;
        private float _uncommon;
        private float _rare;
        private float _epic;
        private float _legendary;
        private int _guaranteedQualityEvery;
        private int _guaranteedQualityChoices;
        private string _blacklist;
        private int _rerollMode;
        private int _rerollEvery;
        private int _choicesEvery;
        private float _creditMultiplier;
        private int _xpCurve;
        private float _startingXp;
        private float _xpScaling;

        public SyncFixConfig() { }

        public SyncFixConfig(bool _)
        {
            _enableQuality = EnableQualityIntegration.Value;
            _removeQualityInteractables = RemoveQualityInteractables.Value;
            _qualityChance = QualityChance.Value;
            _uncommon = UncommonQualityWeight.Value;
            _rare = RareQualityWeight.Value;
            _epic = EpicQualityWeight.Value;
            _legendary = LegendaryQualityWeight.Value;
            _guaranteedQualityEvery = GuaranteedQualityEveryNLevels.Value;
            _guaranteedQualityChoices = GuaranteedQualityChoiceCount.Value;
            _blacklist = ItemBlacklist.Value ?? string.Empty;
            _rerollMode = (int)RerollRefreshOnLevel.Value;
            _rerollEvery = RerollRefreshEveryNLevels.Value;
            _choicesEvery = ItemChoicesEveryNLevels.Value;
            _creditMultiplier = InteractableCreditMultiplier.Value;
            _xpCurve = (int)XpCurve.Value;
            _startingXp = StartingXp.Value;
            _xpScaling = ExponentialXpScaling.Value;
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(_enableQuality);
            writer.Write(_removeQualityInteractables);
            writer.Write(_qualityChance);
            writer.Write(_uncommon);
            writer.Write(_rare);
            writer.Write(_epic);
            writer.Write(_legendary);
            writer.Write(_guaranteedQualityEvery);
            writer.Write(_guaranteedQualityChoices);
            writer.Write(_blacklist ?? string.Empty);
            writer.Write(_rerollMode);
            writer.Write(_rerollEvery);
            writer.Write(_choicesEvery);
            writer.Write(_creditMultiplier);
            writer.Write(_xpCurve);
            writer.Write(_startingXp);
            writer.Write(_xpScaling);
        }

        public void Deserialize(NetworkReader reader)
        {
            _enableQuality = reader.ReadBoolean();
            _removeQualityInteractables = reader.ReadBoolean();
            _qualityChance = reader.ReadSingle();
            _uncommon = reader.ReadSingle();
            _rare = reader.ReadSingle();
            _epic = reader.ReadSingle();
            _legendary = reader.ReadSingle();
            _guaranteedQualityEvery = reader.ReadInt32();
            _guaranteedQualityChoices = reader.ReadInt32();
            _blacklist = reader.ReadString();
            _rerollMode = reader.ReadInt32();
            _rerollEvery = reader.ReadInt32();
            _choicesEvery = reader.ReadInt32();
            _creditMultiplier = reader.ReadSingle();
            _xpCurve = reader.ReadInt32();
            _startingXp = reader.ReadSingle();
            _xpScaling = reader.ReadSingle();
        }

        public void OnReceived()
        {
            if (NetworkServer.active)
                return;
            _hasServerOverride = true;
            _serverBlacklist = _blacklist ?? string.Empty;
            _overrides = this;
            Log.Info("Applied host LevelUpChoicesFixes configuration.");
        }

        internal static T GetOverride<T>(ConfigEntry<T> entry)
        {
            object value = entry.Definition.Key switch
            {
                "Enable Quality Integration" => _overrides._enableQuality,
                "Remove Quality Interactables" => _overrides._removeQualityInteractables,
                "Quality Chance" => _overrides._qualityChance,
                "Uncommon Quality Weight" => _overrides._uncommon,
                "Rare Quality Weight" => _overrides._rare,
                "Epic Quality Weight" => _overrides._epic,
                "Legendary Quality Weight" => _overrides._legendary,
                "Guaranteed Quality Every N Levels" => _overrides._guaranteedQualityEvery,
                "Guaranteed Quality Choice Count" => _overrides._guaranteedQualityChoices,
                "Reroll Refresh On Level" => (RerollRefreshMode)_overrides._rerollMode,
                "Reroll Refresh Every N Levels" => _overrides._rerollEvery,
                "Interactable Credit Multiplier" => _overrides._creditMultiplier,
                "XP Curve" => (XpCurveMode)_overrides._xpCurve,
                "Starting XP" => _overrides._startingXp,
                "XP Scaling" => _overrides._xpScaling,
                _ => entry.Value
            };
            return (T)value;
        }

        private static SyncFixConfig _overrides;

        internal static void ClearOverride() => _overrides = null;
    }
}

