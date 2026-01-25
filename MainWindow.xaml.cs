using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace AiAdminUi;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<RuleItem> _rules = new();
    private readonly string _configPath;
    private AiConfig? _originalConfig;
    private bool _isLoading;
    private const int MaxManualSpawnCount = 4;
    private const int MinAutoSpawnIntervalMs = 10000;
    private bool _autoLoopRunning;
    private bool _suppressUiEvents;
    private static readonly SolidColorBrush StopAutoBrush = new(Color.FromRgb(0xD1, 0x34, 0x38));
    private readonly DispatcherTimer _saveStatusTimer = new();

    private static readonly Dictionary<string, int> DefaultRuleMax = new(StringComparer.OrdinalIgnoreCase)
    {
        ["carno"] = 4,
        ["cerato"] = 3,
        ["compy"] = 6,
        ["deino"] = 2,
        ["diablo"] = 3,
        ["dilo"] = 4,
        ["dryo"] = 5,
        ["galli"] = 4,
        ["hypso"] = 5,
        ["omni"] = 4,
        ["psitta_coastal"] = 4,
        ["psitta"] = 4,
        ["ptera"] = 4,
        ["tenonto"] = 3,
        ["rex"] = 1,
    };

    private static readonly string[] SpeciesOrder =
    {
        "carno",
        "cerato",
        "compy",
        "deino",
        "diablo",
        "dilo",
        "dryo",
        "galli",
        "hypso",
        "omni",
        "psitta_coastal",
        "psitta",
        "ptera",
        "tenonto",
        "rex",
    };

    [GeneratedRegex(@"\b(?<key>enabled|interval_ms|min_player_distance|manual_min_player_distance|max_spawn_attempts|safe_manual_spawn|auto_reload_enabled|auto_reload_interval_ms|player_sync_interval_ms|players_write_interval_ms|debug_logs_enabled|debug_log_parsing|debug_chat_commands|debug_spawning|debug_config|debug_command_queue|spawn_apply_growth|spawn_growth|spawn_log_enabled|auto_spawn_corpse_only|auto_spawn_corpse_scale|chat_commands_enabled|require_admin_for_commands|admin_log_parse_interval_ms|admin_log_parse_max_lines|fish_respawn_enabled|fish_respawn_interval_ms|fish_respawn_only_far|fish_respawn_min_player_distance|fish_respawn_fish_amount|hunger_corpse_enabled|hunger_corpse_threshold|hunger_corpse_cooldown_ms|hunger_corpse_check_interval_ms|hunger_corpse_carnivore_only|hunger_corpse_spawn_radius_min|hunger_corpse_spawn_radius_max|hunger_corpse_max_players_per_check|hunger_corpse_match_size)\s*=\s*(?<val>true|false|-?[0-9]+(?:\.[0-9]+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex ConfigValueRegex();

    [GeneratedRegex(@"rules\s*=\s*\{(?<body>[\s\S]*?)\}\s*,", RegexOptions.Multiline)]
    private static partial Regex RulesBlockRegex();

    [GeneratedRegex(@"(?<key>[a-zA-Z0-9_]+)\s*=\s*(?<val>\d+)", RegexOptions.Multiline)]
    private static partial Regex RuleEntryRegex();

    public MainWindow()
    {
        try
        {
            InitializeComponent();

            _configPath = ResolveConfigPath();
            _commandPath = Path.Combine(Path.GetDirectoryName(_configPath) ?? ".", "IsleServerMod_commands.txt");
            _statusPath = Path.Combine(Path.GetDirectoryName(_configPath) ?? ".", "IsleServerMod_status.txt");
            _playersPath = Path.Combine(Path.GetDirectoryName(_configPath) ?? ".", "IsleServerMod_players.txt");
            _rconPath = Path.Combine(Path.GetDirectoryName(_configPath) ?? ".", "IsleServerMod_rcon.txt");
            rulesGrid.ItemsSource = _rules;
            EnsureDefaultRules();
            cmbSpecies.ItemsSource = SpeciesOrder.ToList();
            cmbSpecies.SelectedIndex = 0;
            cmbHungerCorpseSpecies.ItemsSource = SpeciesOrder.ToList();
            cmbHungerCorpseSpecies.SelectedIndex = 0;
            chkAutoSpawnEnabled.Checked += OnAutoSpawnEnabledChanged;
            chkAutoSpawnEnabled.Unchecked += OnAutoSpawnEnabledChanged;

            // Hook up change tracking for all settings
            txtIntervalMs.TextChanged += (s, e) => UpdateDirtyState();
            txtMinPlayerDistance.TextChanged += (s, e) => UpdateDirtyState();
            txtManualMinPlayerDistance.TextChanged += (s, e) => UpdateDirtyState();
            txtMaxSpawnAttempts.TextChanged += (s, e) => UpdateDirtyState();
            chkSafeManualSpawn.Checked += (s, e) => UpdateDirtyState();
            chkSafeManualSpawn.Unchecked += (s, e) => UpdateDirtyState();
            chkSafeManualSpawn.Unchecked += OnSafeManualSpawnUnchecked;
            chkAutoReloadEnabled.Checked += OnAutoReloadEnabledChanged;
            chkAutoReloadEnabled.Unchecked += OnAutoReloadEnabledChanged;
            txtAutoReloadIntervalMs.TextChanged += (s, e) => UpdateDirtyState();
            txtPlayerSyncIntervalMs.TextChanged += (s, e) => UpdateDirtyState();
            txtPlayersWriteIntervalMs.TextChanged += (s, e) => UpdateDirtyState();
            chkDebugLogsEnabled.Checked += (s, e) => UpdateDirtyState();
            chkDebugLogsEnabled.Unchecked += (s, e) => UpdateDirtyState();
            chkDebugLogParsing.Checked += (s, e) => UpdateDirtyState();
            chkDebugLogParsing.Unchecked += (s, e) => UpdateDirtyState();
            chkDebugChatCommands.Checked += (s, e) => UpdateDirtyState();
            chkDebugChatCommands.Unchecked += (s, e) => UpdateDirtyState();
            chkDebugSpawning.Checked += (s, e) => UpdateDirtyState();
            chkDebugSpawning.Unchecked += (s, e) => UpdateDirtyState();
            chkDebugConfig.Checked += (s, e) => UpdateDirtyState();
            chkDebugConfig.Unchecked += (s, e) => UpdateDirtyState();
            chkDebugCommandQueue.Checked += (s, e) => UpdateDirtyState();
            chkDebugCommandQueue.Unchecked += (s, e) => UpdateDirtyState();
            chkSpawnApplyGrowth.Checked += (s, e) => UpdateDirtyState();
            chkSpawnApplyGrowth.Unchecked += (s, e) => UpdateDirtyState();
            txtSpawnGrowth.TextChanged += (s, e) => UpdateDirtyState();
            chkSpawnLogEnabled.Checked += (s, e) => UpdateDirtyState();
            chkSpawnLogEnabled.Unchecked += (s, e) => UpdateDirtyState();
            chkAutoSpawnCorpseOnly.Checked += (s, e) => UpdateDirtyState();
            chkAutoSpawnCorpseOnly.Unchecked += (s, e) => UpdateDirtyState();
            txtAutoSpawnCorpseClass.TextChanged += (s, e) => UpdateDirtyState();
            txtAutoSpawnCorpseScale.TextChanged += (s, e) => UpdateDirtyState();
            chkChatCommandsEnabled.Checked += (s, e) => UpdateDirtyState();
            chkChatCommandsEnabled.Unchecked += (s, e) => UpdateDirtyState();
            chkRequireAdminForCommands.Checked += (s, e) => UpdateDirtyState();
            chkRequireAdminForCommands.Unchecked += (s, e) => UpdateDirtyState();
            rulesGrid.CellEditEnding += (s, e) => UpdateDirtyState();
            txtRconHost.TextChanged += (s, e) => UpdateDirtyState();
            txtRconPort.TextChanged += (s, e) => UpdateDirtyState();
            txtRconPass.TextChanged += (s, e) => UpdateDirtyState();
            chkFishRespawnEnabled.Checked += (s, e) => UpdateDirtyState();
            chkFishRespawnEnabled.Unchecked += (s, e) => UpdateDirtyState();
            txtFishRespawnInterval.TextChanged += (s, e) => UpdateDirtyState();
            chkFishRespawnOnlyFar.Checked += (s, e) => UpdateDirtyState();
            chkFishRespawnOnlyFar.Unchecked += (s, e) => UpdateDirtyState();
            txtFishRespawnMinDistance.TextChanged += (s, e) => UpdateDirtyState();
            txtFishRespawnAmount.TextChanged += (s, e) => UpdateDirtyState();
            txtFishRespawnFishToSpawn.TextChanged += (s, e) => UpdateDirtyState();
            chkHungerCorpseEnabled.Checked += (s, e) => UpdateDirtyState();
            chkHungerCorpseEnabled.Unchecked += (s, e) => UpdateDirtyState();
            txtHungerCorpseThreshold.TextChanged += (s, e) => UpdateDirtyState();
            txtHungerCorpseCooldown.TextChanged += (s, e) => UpdateDirtyState();
            txtHungerCorpseCheckInterval.TextChanged += (s, e) => UpdateDirtyState();
            chkHungerCorpseCarnivoreOnly.Checked += (s, e) => UpdateDirtyState();
            chkHungerCorpseCarnivoreOnly.Unchecked += (s, e) => UpdateDirtyState();
            txtHungerCorpseRadiusMin.TextChanged += (s, e) => UpdateDirtyState();
            txtHungerCorpseRadiusMax.TextChanged += (s, e) => UpdateDirtyState();
            txtHungerCorpseMaxPlayersPerCheck.TextChanged += (s, e) => UpdateDirtyState();
            cmbHungerCorpseSpecies.SelectionChanged += (s, e) => UpdateDirtyState();
            chkHungerCorpseMatchSize.Checked += (s, e) => UpdateDirtyState();
            chkHungerCorpseMatchSize.Unchecked += (s, e) => UpdateDirtyState();
            cmbCommandMode.SelectionChanged += (s, e) => UpdateExecuteButtonState();

            _saveStatusTimer.Interval = TimeSpan.FromSeconds(3);
            _saveStatusTimer.Tick += (_, _) =>
            {
                _saveStatusTimer.Stop();
                txtSaveStatus.Text = "";
            };

            _isLoading = true;
            LoadRconSettings();
            _isLoading = false;

            _ = LoadConfigAsync();
            RefreshStatus();
            RefreshPlayers();
            UpdateExecuteButtonState();
        }
        catch (Exception ex)
        {
            Log("Startup error: " + ex);
            MessageBox.Show($"Startup error:\n{ex.Message}", "AI Admin UI", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
    }

    private static string ResolveConfigPath()
    {
        string? current = AppContext.BaseDirectory;
        while (current != null && !current.EndsWith("TheIsleServerFiles", StringComparison.OrdinalIgnoreCase))
        {
            current = Path.GetDirectoryName(current);
        }

        if (string.IsNullOrEmpty(current))
        {
            return Path.Combine(AppContext.BaseDirectory, "config", "IsleServerMod_config.lua");
        }

        return Path.Combine(
            current,
            "TheIsle",
            "Binaries",
            "Win64",
            "config",
            "IsleServerMod_config.lua");
    }

    private void EnsureDefaultRules()
    {
        if (_rules.Count > 0) return;
        foreach (var s in SpeciesOrder)
        {
            DefaultRuleMax.TryGetValue(s, out int max);
            _rules.Add(new RuleItem { Species = s, Max = max });
        }
    }

    private async Task LoadConfigAsync()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                MessageBox.Show(
                    "Config file not found:\n" + _configPath,
                    "AI Admin UI",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string text = await File.ReadAllTextAsync(_configPath);
            var config = ParseConfig(text);
            ApplyConfigToUi(config, true);
        }
        catch (Exception ex)
        {
            Log("LoadConfig error: " + ex);
            MessageBox.Show($"LoadConfig failed:\n{ex.Message}", "AI Admin UI", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyConfigToUi(AiConfig config, bool updateOriginal)
    {
        _isLoading = true;
        try
        {
            chkAutoSpawnEnabled.IsChecked = config.Enabled;
            txtIntervalMs.Text = MsToMinutes(config.IntervalMs).ToString("0.###", CultureInfo.InvariantCulture);
            txtMinPlayerDistance.Text = config.MinPlayerDistance.ToString(CultureInfo.InvariantCulture);
            txtManualMinPlayerDistance.Text = config.ManualMinPlayerDistance.ToString(CultureInfo.InvariantCulture);
            txtMaxSpawnAttempts.Text = config.MaxSpawnAttempts.ToString(CultureInfo.InvariantCulture);
            chkSafeManualSpawn.IsChecked = config.SafeManualSpawn;
            chkAutoReloadEnabled.IsChecked = config.AutoReloadEnabled;
            txtAutoReloadIntervalMs.Text = MsToMinutes(config.AutoReloadIntervalMs).ToString("0.###", CultureInfo.InvariantCulture);
            txtPlayerSyncIntervalMs.Text = MsToMinutes(config.PlayerSyncIntervalMs).ToString("0.###", CultureInfo.InvariantCulture);
            txtPlayersWriteIntervalMs.Text = MsToMinutes(config.PlayersWriteIntervalMs).ToString("0.###", CultureInfo.InvariantCulture);
            chkDebugLogsEnabled.IsChecked = config.DebugLogsEnabled;
            chkDebugLogParsing.IsChecked = config.DebugLogParsing;
            chkDebugChatCommands.IsChecked = config.DebugChatCommands;
            chkDebugSpawning.IsChecked = config.DebugSpawning;
            chkDebugConfig.IsChecked = config.DebugConfig;
            chkDebugCommandQueue.IsChecked = config.DebugCommandQueue;
            chkSpawnApplyGrowth.IsChecked = config.SpawnApplyGrowth;
            txtSpawnGrowth.Text = config.SpawnGrowth.ToString("0.###", CultureInfo.InvariantCulture);
            chkSpawnLogEnabled.IsChecked = config.SpawnLogEnabled;
            chkAutoSpawnCorpseOnly.IsChecked = config.AutoSpawnCorpseOnly;
            txtAutoSpawnCorpseClass.Text = config.AutoSpawnCorpseClass ?? "";
            txtAutoSpawnCorpseScale.Text = config.AutoSpawnCorpseScale.ToString("0.###", CultureInfo.InvariantCulture);
            chkChatCommandsEnabled.IsChecked = config.ChatCommandsEnabled;
            chkRequireAdminForCommands.IsChecked = config.RequireAdminForCommands;
            txtAdminLogParseInterval.Text = MsToMinutes(config.AdminLogParseIntervalMs).ToString("0.###", CultureInfo.InvariantCulture);
            txtAdminLogParseMaxLines.Text = config.AdminLogParseMaxLines.ToString(CultureInfo.InvariantCulture);
            chkFishRespawnEnabled.IsChecked = config.FishRespawnEnabled;
            txtFishRespawnInterval.Text = MsToSeconds(config.FishRespawnIntervalMs).ToString("0.###", CultureInfo.InvariantCulture);
            chkFishRespawnOnlyFar.IsChecked = config.FishRespawnOnlyFar;
            txtFishRespawnMinDistance.Text = config.FishRespawnMinPlayerDistance.ToString("0.###", CultureInfo.InvariantCulture);
            txtFishRespawnAmount.Text = config.FishRespawnFishAmount < 0
                ? ""
                : config.FishRespawnFishAmount.ToString(CultureInfo.InvariantCulture);
            txtFishRespawnFishToSpawn.Text = config.FishRespawnFishToSpawn ?? "";
            chkHungerCorpseEnabled.IsChecked = config.HungerCorpseEnabled;
            txtHungerCorpseThreshold.Text = config.HungerCorpseThreshold.ToString("0.#", CultureInfo.InvariantCulture);
            txtHungerCorpseCooldown.Text = MsToMinutes(config.HungerCorpseCooldownMs).ToString("0.###", CultureInfo.InvariantCulture);
            txtHungerCorpseCheckInterval.Text = MsToSeconds(config.HungerCorpseCheckIntervalMs).ToString("0.###", CultureInfo.InvariantCulture);
            chkHungerCorpseCarnivoreOnly.IsChecked = config.HungerCorpseCarnivoreOnly;
            txtHungerCorpseRadiusMin.Text = config.HungerCorpseSpawnRadiusMin.ToString("0.#", CultureInfo.InvariantCulture);
            txtHungerCorpseRadiusMax.Text = config.HungerCorpseSpawnRadiusMax.ToString("0.#", CultureInfo.InvariantCulture);
            txtHungerCorpseMaxPlayersPerCheck.Text = config.HungerCorpseMaxPlayersPerCheck.ToString(CultureInfo.InvariantCulture);

            // Set hunger corpse species ComboBox
            string hungerSpecies = config.HungerCorpseSpecies ?? "";
            if (!string.IsNullOrEmpty(hungerSpecies))
            {
                // Find the item in the ComboBox that matches the config value
                int matchIndex = -1;
                for (int i = 0; i < cmbHungerCorpseSpecies.Items.Count; i++)
                {
                    if (cmbHungerCorpseSpecies.Items[i]?.ToString()?.Equals(hungerSpecies, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        matchIndex = i;
                        break;
                    }
                }
                cmbHungerCorpseSpecies.SelectedIndex = matchIndex >= 0 ? matchIndex : 0;
            }
            else
            {
                cmbHungerCorpseSpecies.SelectedIndex = 0;  // Default to first species
            }

            chkHungerCorpseMatchSize.IsChecked = config.HungerCorpseMatchSize;
            _autoLoopRunning = false;
            UpdateAutoToggleState();
            UpdateAdminLogParseState();

            var map = config.Rules;
            foreach (var item in _rules)
            {
                if (map.TryGetValue(item.Species, out int max))
                {
                    item.Max = max;
                }
            }
            rulesGrid.Items.Refresh();

        }
        finally
        {
            _isLoading = false;
            if (updateOriginal)
            {
                _originalConfig = CollectConfigFromUi();
            }
            UpdateDirtyState();
            UpdateReloadButtonState();
        }
    }

    private AiConfig CollectConfigFromUi()
    {
        return new AiConfig
        {
            Enabled = chkAutoSpawnEnabled.IsChecked == true,
            IntervalMs = ReadMinutesToMs(txtIntervalMs.Text, 180000),
            MinPlayerDistance = ReadDouble(txtMinPlayerDistance.Text, 900.0),
            ManualMinPlayerDistance = ReadDouble(txtManualMinPlayerDistance.Text, 200.0),
            MaxSpawnAttempts = ReadInt(txtMaxSpawnAttempts.Text, 6),
            SafeManualSpawn = chkSafeManualSpawn.IsChecked == true,
            AutoReloadEnabled = chkAutoReloadEnabled.IsChecked == true,
            AutoReloadIntervalMs = ReadMinutesToMs(txtAutoReloadIntervalMs.Text, 60000),
            PlayerSyncIntervalMs = ReadMinutesToMs(txtPlayerSyncIntervalMs.Text, 60000),
            PlayersWriteIntervalMs = ReadMinutesToMs(txtPlayersWriteIntervalMs.Text, 15000),
            DebugLogsEnabled = chkDebugLogsEnabled.IsChecked == true,
            DebugLogParsing = chkDebugLogParsing.IsChecked == true,
            DebugChatCommands = chkDebugChatCommands.IsChecked == true,
            DebugSpawning = chkDebugSpawning.IsChecked == true,
            DebugConfig = chkDebugConfig.IsChecked == true,
            DebugCommandQueue = chkDebugCommandQueue.IsChecked == true,
            SpawnApplyGrowth = chkSpawnApplyGrowth.IsChecked == true,
            SpawnGrowth = ClampGrowth(ReadDouble(txtSpawnGrowth.Text, 1.0)),
            SpawnLogEnabled = chkSpawnLogEnabled.IsChecked == true,
            AutoSpawnCorpseOnly = chkAutoSpawnCorpseOnly.IsChecked == true,
            AutoSpawnCorpseClass = txtAutoSpawnCorpseClass.Text.Trim(),
            AutoSpawnCorpseScale = Math.Max(0.05, ReadDouble(txtAutoSpawnCorpseScale.Text, 0.1)),
            ChatCommandsEnabled = chkChatCommandsEnabled.IsChecked == true,
            RequireAdminForCommands = chkRequireAdminForCommands.IsChecked == true,
            AdminLogParseIntervalMs = Math.Max(1000, ReadMinutesToMs(txtAdminLogParseInterval.Text, 60000)),
            AdminLogParseMaxLines = Math.Max(100, ReadInt(txtAdminLogParseMaxLines.Text, 10000)),
            FishRespawnEnabled = chkFishRespawnEnabled.IsChecked == true,
            FishRespawnIntervalMs = Math.Max(10000, ReadSecondsToMs(txtFishRespawnInterval.Text, 300000)),
            FishRespawnOnlyFar = chkFishRespawnOnlyFar.IsChecked == true,
            FishRespawnMinPlayerDistance = Math.Max(0, ReadDouble(txtFishRespawnMinDistance.Text, 3000.0)),
            FishRespawnFishAmount = ReadInt(txtFishRespawnAmount.Text, -1),
            FishRespawnFishToSpawn = txtFishRespawnFishToSpawn.Text.Trim(),
            HungerCorpseEnabled = chkHungerCorpseEnabled.IsChecked == true,
            HungerCorpseThreshold = Math.Clamp((float)ReadDouble(txtHungerCorpseThreshold.Text, 20.0), 0f, 100f),
            HungerCorpseCooldownMs = Math.Max(10000, ReadMinutesToMs(txtHungerCorpseCooldown.Text, 300000)),
            HungerCorpseCheckIntervalMs = Math.Max(1000, ReadSecondsToMs(txtHungerCorpseCheckInterval.Text, 5000)),
            HungerCorpseCarnivoreOnly = chkHungerCorpseCarnivoreOnly.IsChecked == true,
            HungerCorpseSpawnRadiusMin = Math.Max(0f, (float)ReadDouble(txtHungerCorpseRadiusMin.Text, 200.0)),
            HungerCorpseSpawnRadiusMax = Math.Max(0f, (float)ReadDouble(txtHungerCorpseRadiusMax.Text, 600.0)),
            HungerCorpseSpecies = cmbHungerCorpseSpecies.SelectedItem?.ToString() ?? "",
            HungerCorpseMatchSize = chkHungerCorpseMatchSize.IsChecked == true,
            HungerCorpseMaxPlayersPerCheck = Math.Max(0, ReadInt(txtHungerCorpseMaxPlayersPerCheck.Text, 100)),
            Rules = _rules.ToDictionary(r => r.Species, r => Math.Max(0, r.Max)),
            RconHost = txtRconHost.Text.Trim(),
            RconPort = txtRconPort.Text.Trim(),
            RconPass = txtRconPass.Text
        };
    }

    private static AiConfig CreateDefaultConfig()
    {
        var rules = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in SpeciesOrder)
        {
            DefaultRuleMax.TryGetValue(s, out int max);
            rules[s] = max;
        }

        return new AiConfig
        {
            Enabled = false,
            IntervalMs = 180000,
            MinPlayerDistance = 900.0,
            ManualMinPlayerDistance = 200.0,
            MaxSpawnAttempts = 6,
            SafeManualSpawn = true,
            AutoReloadEnabled = true,
            AutoReloadIntervalMs = 60000,
            PlayerSyncIntervalMs = 60000,
            PlayersWriteIntervalMs = 15000,
            DebugLogsEnabled = false,
            DebugLogParsing = false,
            DebugChatCommands = false,
            DebugSpawning = false,
            DebugConfig = false,
            DebugCommandQueue = false,
            SpawnApplyGrowth = true,
            SpawnGrowth = 1.0,
            SpawnLogEnabled = false,
            AutoSpawnCorpseOnly = false,
            AutoSpawnCorpseClass = "dead_dino",
            AutoSpawnCorpseScale = 0.1,
            ChatCommandsEnabled = false,
            RequireAdminForCommands = true,
            AdminLogParseIntervalMs = 60000,
            AdminLogParseMaxLines = 10000,
            FishRespawnEnabled = false,
            FishRespawnIntervalMs = 300000,  // 5 minutes
            FishRespawnOnlyFar = true,
            FishRespawnMinPlayerDistance = 3000.0,  // 30 meters
            FishRespawnFishAmount = -1,
            FishRespawnFishToSpawn = "",
            HungerCorpseEnabled = false,
            HungerCorpseThreshold = 20.0f,
            HungerCorpseCooldownMs = 300000,  // 5 minutes
            HungerCorpseCheckIntervalMs = 5000,
            HungerCorpseCarnivoreOnly = true,
            HungerCorpseSpawnRadiusMin = 200.0f,
            HungerCorpseSpawnRadiusMax = 600.0f,
            HungerCorpseSpecies = "",
            HungerCorpseMatchSize = true,
            HungerCorpseMaxPlayersPerCheck = 100,
            Rules = rules,
        };
    }

    private static int ReadInt(string? text, int fallback)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            return value;
        }
        return fallback;
    }

    private static double ReadDouble(string? text, double fallback)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            return value;
        }
        return fallback;
    }

    private static double ClampGrowth(double value)
    {
        if (value < 0) return 0;
        if (value > 1) return 1;
        return value;
    }

    private bool ClampAutoSpawnInterval(AiConfig config)
    {
        if (config.IntervalMs < MinAutoSpawnIntervalMs)
        {
            config.IntervalMs = MinAutoSpawnIntervalMs;
            _suppressUiEvents = true;
            txtIntervalMs.Text = MsToMinutes(MinAutoSpawnIntervalMs).ToString("0.###", CultureInfo.InvariantCulture);
            _suppressUiEvents = false;
            return true;
        }
        return false;
    }

    private static int ReadMinutesToMs(string? text, int fallbackMs)
    {
        double fallbackMinutes = fallbackMs / 60000.0;
        double minutes = ReadDouble(text, fallbackMinutes);
        if (minutes < 0)
        {
            minutes = 0;
        }
        return (int)Math.Round(minutes * 60000.0);
    }

    private static double MsToMinutes(int ms)
    {
        if (ms <= 0) return 0;
        return ms / 60000.0;
    }

    private static double MsToSeconds(int ms)
    {
        if (ms <= 0) return 0;
        return ms / 1000.0;
    }

    private static int ReadSecondsToMs(string? text, int fallbackMs)
    {
        double fallbackSeconds = fallbackMs / 1000.0;
        double seconds = ReadDouble(text, fallbackSeconds);
        if (seconds < 0)
        {
            seconds = 0;
        }
        return (int)Math.Round(seconds * 1000.0);
    }

    private static AiConfig ParseConfig(string text)
    {
        var config = new AiConfig();
        var matches = ConfigValueRegex().Matches(text);

        foreach (Match m in matches)
        {
            string key = m.Groups["key"].Value.ToLowerInvariant();
            string val = m.Groups["val"].Value;

            switch (key)
            {
                case "enabled": config.Enabled = bool.Parse(val); break;
                case "interval_ms": config.IntervalMs = int.Parse(val); break;
                case "min_player_distance": config.MinPlayerDistance = double.Parse(val, CultureInfo.InvariantCulture); break;
                case "manual_min_player_distance": config.ManualMinPlayerDistance = double.Parse(val, CultureInfo.InvariantCulture); break;
                case "max_spawn_attempts": config.MaxSpawnAttempts = int.Parse(val); break;
                case "safe_manual_spawn": config.SafeManualSpawn = bool.Parse(val); break;
                case "auto_reload_enabled": config.AutoReloadEnabled = bool.Parse(val); break;
                case "auto_reload_interval_ms": config.AutoReloadIntervalMs = int.Parse(val); break;
                case "player_sync_interval_ms": config.PlayerSyncIntervalMs = int.Parse(val); break;
                case "players_write_interval_ms": config.PlayersWriteIntervalMs = int.Parse(val); break;
                case "debug_logs_enabled": config.DebugLogsEnabled = bool.Parse(val); break;
                case "debug_log_parsing": config.DebugLogParsing = bool.Parse(val); break;
                case "debug_chat_commands": config.DebugChatCommands = bool.Parse(val); break;
                case "debug_spawning": config.DebugSpawning = bool.Parse(val); break;
                case "debug_config": config.DebugConfig = bool.Parse(val); break;
                case "debug_command_queue": config.DebugCommandQueue = bool.Parse(val); break;
                case "spawn_apply_growth": config.SpawnApplyGrowth = bool.Parse(val); break;
                case "spawn_growth": config.SpawnGrowth = ClampGrowth(double.Parse(val, CultureInfo.InvariantCulture)); break;
                case "spawn_log_enabled": config.SpawnLogEnabled = bool.Parse(val); break;
                case "auto_spawn_corpse_only": config.AutoSpawnCorpseOnly = bool.Parse(val); break;
                case "auto_spawn_corpse_scale": config.AutoSpawnCorpseScale = double.Parse(val, CultureInfo.InvariantCulture); break;
                case "chat_commands_enabled": config.ChatCommandsEnabled = bool.Parse(val); break;
                case "require_admin_for_commands": config.RequireAdminForCommands = bool.Parse(val); break;
                case "admin_log_parse_interval_ms": config.AdminLogParseIntervalMs = int.Parse(val); break;
                case "admin_log_parse_max_lines": config.AdminLogParseMaxLines = int.Parse(val); break;
                case "fish_respawn_enabled": config.FishRespawnEnabled = bool.Parse(val); break;
                case "fish_respawn_interval_ms": config.FishRespawnIntervalMs = int.Parse(val); break;
                case "fish_respawn_only_far": config.FishRespawnOnlyFar = bool.Parse(val); break;
                case "fish_respawn_min_player_distance": config.FishRespawnMinPlayerDistance = double.Parse(val); break;
                case "fish_respawn_fish_amount": config.FishRespawnFishAmount = int.Parse(val); break;
                case "hunger_corpse_enabled": config.HungerCorpseEnabled = bool.Parse(val); break;
                case "hunger_corpse_threshold": config.HungerCorpseThreshold = float.Parse(val, CultureInfo.InvariantCulture); break;
                case "hunger_corpse_cooldown_ms": config.HungerCorpseCooldownMs = int.Parse(val); break;
                case "hunger_corpse_check_interval_ms": config.HungerCorpseCheckIntervalMs = int.Parse(val); break;
                case "hunger_corpse_carnivore_only": config.HungerCorpseCarnivoreOnly = bool.Parse(val); break;
                case "hunger_corpse_spawn_radius_min": config.HungerCorpseSpawnRadiusMin = float.Parse(val, CultureInfo.InvariantCulture); break;
                case "hunger_corpse_spawn_radius_max": config.HungerCorpseSpawnRadiusMax = float.Parse(val, CultureInfo.InvariantCulture); break;
                case "hunger_corpse_max_players_per_check": config.HungerCorpseMaxPlayersPerCheck = int.Parse(val); break;
                case "hunger_corpse_match_size": config.HungerCorpseMatchSize = bool.Parse(val); break;
            }
        }

        var rules = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var rulesBlock = RulesBlockRegex().Match(text);
        if (rulesBlock.Success)
        {
            var body = rulesBlock.Groups["body"].Value;
            foreach (Match m in RuleEntryRegex().Matches(body))
            {
                string key = m.Groups["key"].Value;
                int val = int.Parse(m.Groups["val"].Value, CultureInfo.InvariantCulture);
                rules[key] = val;
            }
        }

        config.Rules = rules;
        config.FishRespawnFishToSpawn = GetString(text, "fish_respawn_fish_to_spawn", config.FishRespawnFishToSpawn);
        config.AutoSpawnCorpseClass = GetString(text, "auto_spawn_corpse_class", config.AutoSpawnCorpseClass);
        config.HungerCorpseSpecies = GetString(text, "hunger_corpse_species", config.HungerCorpseSpecies);
        return config;
    }

    private static bool GetBool(string text, string key, bool fallback)
    {
        var m = Regex.Match(text, $@"\b{Regex.Escape(key)}\s*=\s*(true|false)", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return string.Equals(m.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
        }
        return fallback;
    }

    private static int GetInt(string text, string key, int fallback)
    {
        var m = Regex.Match(text, $@"\b{Regex.Escape(key)}\s*=\s*(\d+)", RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
        {
            return v;
        }
        return fallback;
    }

    private static double GetDouble(string text, string key, double fallback)
    {
        var m = Regex.Match(text, $@"\b{Regex.Escape(key)}\s*=\s*(-?[0-9]+(?:\.[0-9]+)?)", RegexOptions.IgnoreCase);
        if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
        {
            return v;
        }
        return fallback;
    }

    private static string GetString(string text, string key, string fallback)
    {
        var m = Regex.Match(text, $@"\b{Regex.Escape(key)}\s*=\s*""(?<val>[^""]*)""", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            return m.Groups["val"].Value;
        }
        return fallback;
    }

    private static string EscapeLuaString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string SerializeConfig(AiConfig config)
    {
        var sb = new StringBuilder();
        sb.AppendLine("return {");
        sb.AppendLine($"    enabled = {config.Enabled.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    interval_ms = {config.IntervalMs},");
        sb.AppendLine($"    min_player_distance = {config.MinPlayerDistance.ToString(CultureInfo.InvariantCulture)},");
        sb.AppendLine($"    manual_min_player_distance = {config.ManualMinPlayerDistance.ToString(CultureInfo.InvariantCulture)},");
        sb.AppendLine($"    max_spawn_attempts = {config.MaxSpawnAttempts},");
        sb.AppendLine($"    safe_manual_spawn = {config.SafeManualSpawn.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    auto_reload_enabled = {config.AutoReloadEnabled.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    auto_reload_interval_ms = {config.AutoReloadIntervalMs},");
        sb.AppendLine($"    player_sync_interval_ms = {config.PlayerSyncIntervalMs},");
        sb.AppendLine($"    players_write_interval_ms = {config.PlayersWriteIntervalMs},");
        sb.AppendLine($"    debug_logs_enabled = {config.DebugLogsEnabled.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    debug_log_parsing = {config.DebugLogParsing.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    debug_chat_commands = {config.DebugChatCommands.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    debug_spawning = {config.DebugSpawning.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    debug_config = {config.DebugConfig.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    debug_command_queue = {config.DebugCommandQueue.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    spawn_apply_growth = {config.SpawnApplyGrowth.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    spawn_growth = {config.SpawnGrowth.ToString(CultureInfo.InvariantCulture)},");
        sb.AppendLine($"    spawn_log_enabled = {config.SpawnLogEnabled.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    auto_spawn_corpse_only = {config.AutoSpawnCorpseOnly.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    auto_spawn_corpse_class = \"{EscapeLuaString(config.AutoSpawnCorpseClass)}\",");
        sb.AppendLine($"    auto_spawn_corpse_scale = {config.AutoSpawnCorpseScale.ToString(CultureInfo.InvariantCulture)},");
        sb.AppendLine($"    chat_commands_enabled = {config.ChatCommandsEnabled.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    require_admin_for_commands = {config.RequireAdminForCommands.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    admin_log_parse_interval_ms = {config.AdminLogParseIntervalMs},");
        sb.AppendLine($"    admin_log_parse_max_lines = {config.AdminLogParseMaxLines},");
        sb.AppendLine($"    fish_respawn_enabled = {config.FishRespawnEnabled.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    fish_respawn_interval_ms = {config.FishRespawnIntervalMs},");
        sb.AppendLine($"    fish_respawn_only_far = {config.FishRespawnOnlyFar.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    fish_respawn_min_player_distance = {config.FishRespawnMinPlayerDistance.ToString(CultureInfo.InvariantCulture)},");
        sb.AppendLine($"    fish_respawn_fish_amount = {config.FishRespawnFishAmount},");
        sb.AppendLine($"    fish_respawn_fish_to_spawn = \"{EscapeLuaString(config.FishRespawnFishToSpawn)}\",");
        sb.AppendLine($"    hunger_corpse_enabled = {config.HungerCorpseEnabled.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    hunger_corpse_threshold = {config.HungerCorpseThreshold.ToString(CultureInfo.InvariantCulture)},");
        sb.AppendLine($"    hunger_corpse_cooldown_ms = {config.HungerCorpseCooldownMs},");
        sb.AppendLine($"    hunger_corpse_check_interval_ms = {config.HungerCorpseCheckIntervalMs},");
        sb.AppendLine($"    hunger_corpse_carnivore_only = {config.HungerCorpseCarnivoreOnly.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    hunger_corpse_spawn_radius_min = {config.HungerCorpseSpawnRadiusMin.ToString(CultureInfo.InvariantCulture)},");
        sb.AppendLine($"    hunger_corpse_spawn_radius_max = {config.HungerCorpseSpawnRadiusMax.ToString(CultureInfo.InvariantCulture)},");
        sb.AppendLine($"    hunger_corpse_max_players_per_check = {config.HungerCorpseMaxPlayersPerCheck},");
        sb.AppendLine($"    hunger_corpse_species = \"{EscapeLuaString(config.HungerCorpseSpecies)}\",");
        sb.AppendLine($"    hunger_corpse_match_size = {config.HungerCorpseMatchSize.ToString().ToLowerInvariant()},");
        sb.AppendLine("    rules = {");

        foreach (var key in SpeciesOrder)
        {
            if (config.Rules.TryGetValue(key, out int val))
            {
                sb.AppendLine($"        {key} = {val},");
            }
        }

        foreach (var kvp in config.Rules.Where(k => !SpeciesOrder.Contains(k.Key, StringComparer.OrdinalIgnoreCase)))
        {
            sb.AppendLine($"        {kvp.Key} = {kvp.Value},");
        }

        sb.AppendLine("    },");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private async void btnReload_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            btnReload.IsEnabled = false;
            await LoadConfigAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Reload failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            btnReload.IsEnabled = true;
        }
    }

    private void btnReset_Click(object sender, RoutedEventArgs e)
    {
        var config = CreateDefaultConfig();
        ApplyConfigToUi(config, false);
    }

    private async void btnSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            btnSave.IsEnabled = false;
            
            var config = CollectConfigFromUi();
            if (ClampAutoSpawnInterval(config))
            {
                MessageBox.Show(
                    $"Auto-spawn interval has a minimum of {MsToMinutes(MinAutoSpawnIntervalMs):0.###} minutes (10 seconds). " +
                    "Your value was clamped to the minimum.",
                    "AI Admin UI",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            string text = SerializeConfig(config);
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath) ?? ".");
            await File.WriteAllTextAsync(_configPath, text);
            SaveRconSettings();
            _originalConfig = CollectConfigFromUi();
            UpdateDirtyState();
            ShowSaveStatus("Settings saved successfully.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Save failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            btnSave.IsEnabled = true;
        }
    }

    private void btnToggleAuto_Click(object sender, RoutedEventArgs e)
    {
        if (chkAutoSpawnEnabled.IsChecked != true)
        {
            UpdateAutoToggleState();
            return;
        }

        if (_autoLoopRunning)
        {
            _ = WriteCommandAsync("stop");
            _autoLoopRunning = false;
        }
        else
        {
            _ = WriteCommandAsync("start");
            _autoLoopRunning = true;
        }

        UpdateAutoToggleState();
    }

    private void OnAutoSpawnEnabledChanged(object sender, RoutedEventArgs e)
    {
        if (!_isLoading && chkAutoSpawnEnabled.IsChecked == true)
        {
            var result = MessageBox.Show(
                "Warning: Enabling Auto Spawn may increase server load and carries a risk of crashing the server if settings are too aggressive.\n\nDo you want to proceed?",
                "Server Stability Warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                chkAutoSpawnEnabled.IsChecked = false;
                return;
            }
        }
        UpdateAutoToggleState();
        UpdateDirtyState();
    }

    private void OnAutoReloadEnabledChanged(object sender, RoutedEventArgs e)
    {
        UpdateReloadButtonState();
        UpdateDirtyState();
    }

    private void OnSafeManualSpawnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _suppressUiEvents)
        {
            return;
        }

        var result = MessageBox.Show(
            "Warning: Disabling Safe Manual Spawn can place dinos inside collisions or steep terrain.\n" +
            "This increases the risk of physics issues or crashes.\n\nDisable it anyway?",
            "AI Admin UI",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            _suppressUiEvents = true;
            chkSafeManualSpawn.IsChecked = true;
            _suppressUiEvents = false;
            UpdateDirtyState();
        }
    }

    private void UpdateAutoToggleState()
    {
        bool enabled = chkAutoSpawnEnabled.IsChecked == true;
        btnToggleAuto.IsEnabled = enabled;
        if (!enabled)
        {
            _autoLoopRunning = false;
        }
        if (_autoLoopRunning)
        {
            iconToggleAuto.Text = "\uE71A";
            iconToggleAuto.Foreground = StopAutoBrush;
            txtToggleAuto.Foreground = StopAutoBrush;
            txtToggleAuto.Text = "Stop Auto";
        }
        else
        {
            var defaultBrush = (Brush)FindResource("TextBrush");
            iconToggleAuto.Text = "\uE768";
            iconToggleAuto.Foreground = defaultBrush;
            txtToggleAuto.Foreground = defaultBrush;
            txtToggleAuto.Text = "Start Auto";
        }
    }

    private void UpdateReloadButtonState()
    {
        bool autoReloadOn = chkAutoReloadEnabled.IsChecked == true;
        btnReload.IsEnabled = !autoReloadOn;
        btnReload.ToolTip = autoReloadOn
            ? "Auto Reload is enabled. Turn it off to refresh manually."
            : "Reload config from disk and refresh the UI fields";
    }

    private void ShowSaveStatus(string message)
    {
        txtSaveStatus.Text = message;
        _saveStatusTimer.Stop();
        _saveStatusTimer.Start();
    }

    private void UpdateExecuteButtonState()
    {
        string mode = (cmbCommandMode.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "spawn_near";
        if (mode == "despawn_all")
        {
            txtExecuteLabel.Text = "Despawn All";
            btnExecute.ToolTip = "Despawn all AI spawned by this mod";
            return;
        }
        if (mode == "kill_all")
        {
            txtExecuteLabel.Text = "Kill All";
            btnExecute.ToolTip = "Kill all AI spawned by this mod (attempts corpse)";
            return;
        }
        txtExecuteLabel.Text = "Execute";
        btnExecute.ToolTip = "Execute the selected command";
    }

    private void UpdateAdminLogParseState()
    {
        // Log parsing is only needed when chat commands are enabled AND admin requirement is enabled
        bool chatEnabled = chkChatCommandsEnabled.IsChecked == true;
        bool adminRequired = chkRequireAdminForCommands.IsChecked == true;
        bool logParseNeeded = chatEnabled && adminRequired;

        lblAdminLogParseInterval.IsEnabled = logParseNeeded;
        txtAdminLogParseInterval.IsEnabled = logParseNeeded;
        lblAdminLogParseMaxLines.IsEnabled = logParseNeeded;
        txtAdminLogParseMaxLines.IsEnabled = logParseNeeded;

        if (!logParseNeeded)
        {
            txtAdminLogParseInterval.ToolTip = "Log parsing is disabled (only needed when in-game commands AND admin requirement are both enabled)";
            txtAdminLogParseMaxLines.ToolTip = "Log parsing is disabled (only needed when in-game commands AND admin requirement are both enabled)";
        }
        else
        {
            txtAdminLogParseInterval.ToolTip = "How often to parse join log for Steam IDs (minutes, min 0.017 = 1 second). Higher values for bigger servers to reduce CPU usage.";
            txtAdminLogParseMaxLines.ToolTip = "Maximum lines to parse per interval (min 100). Prevents lag spikes with large logs. 10000 is recommended for 400 player servers.";
        }
    }

    private void AdminCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateAdminLogParseState();
    }

    private async void btnExecute_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            btnExecute.IsEnabled = false;

            string mode = (cmbCommandMode.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "spawn_near";
            string species = cmbSpecies.SelectedItem?.ToString() ?? "carno";
            int countVal = ReadInt(txtCount.Text.Trim(), 1);
            if (countVal < 1) countVal = 1;
            if (countVal > MaxManualSpawnCount) countVal = MaxManualSpawnCount;

            txtCount.Text = countVal.ToString(CultureInfo.InvariantCulture);
            string count = countVal.ToString(CultureInfo.InvariantCulture);

            if (mode == "despawn_all")
            {
                await WriteCommandAsync("despawn_all");
                return;
            }

            if (mode == "kill_all")
            {
                await WriteCommandAsync("kill_all");
                return;
            }

            if (mode == "despawn")
            {
                await WriteCommandAsync($"{mode} {species}");
                return;
            }

            if (mode == "kill")
            {
                await WriteCommandAsync($"{mode} {species}");
                return;
            }

            if (mode == "spawn_at")
            {
                string coords = txtCoords.Text.Trim();
                if (string.IsNullOrWhiteSpace(coords))
                {
                    MessageBox.Show("Enter coordinates as: x y z", "AI Admin UI", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                await WriteCommandAsync($"{mode} {species} {coords} {count}");
            }
            else if (mode == "spawn_for" || mode == "spawn_corpse_for")
            {
                string playerId = txtPlayerId.Text.Trim();
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    MessageBox.Show("Enter player id or name.", "AI Admin UI", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                // Format: spawn_for <player> <species> [count]
                await WriteCommandAsync($"{mode} {playerId} {species} {count}");
            }
            else
            {
                await WriteCommandAsync($"{mode} {species} {count}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Execution failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            btnExecute.IsEnabled = true;
        }
    }

    private void btnRefreshStatus_Click(object sender, RoutedEventArgs e)
    {
        RefreshStatus();
    }

    private void btnRefreshPlayers_Click(object sender, RoutedEventArgs e)
    {
        RefreshPlayers();
    }

    private async void btnManualFishRespawn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            btnManualFishRespawn.IsEnabled = false;
            await WriteCommandAsync("fish respawn");

            MessageBox.Show(
                "Fish respawn command sent successfully!\n\n" +
                "The server will process this command shortly.\n" +
                "Check the server console for confirmation.",
                "Fish Respawn",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to send fish respawn command:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            btnManualFishRespawn.IsEnabled = true;
        }
    }

    private void lstPlayers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lstPlayers.SelectedItem is PlayerItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.Ue4ssId))
            {
                txtPlayerId.Text = item.Ue4ssId;
                cmbCommandMode.SelectedIndex = 3;
            }
            else
            {
                MessageBox.Show(
                    "No PlayerId available for this entry.\n" +
                    "Use a player entry or refresh the list.",
                    "AI Admin UI",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }

    private async void btnFetchPlayersRcon_Click(object sender, RoutedEventArgs e)
    {
        btnFetchPlayersRcon.IsEnabled = false;
        try
        {
            string host = txtRconHost.Text.Trim();
            string portText = txtRconPort.Text.Trim();
            string password = txtRconPass.Text;

            if (!int.TryParse(portText, out int port))
            {
                MessageBox.Show("Invalid RCON port.", "AI Admin UI", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var client = new RconClient(host, port, password);
            var players = await client.GetPlayerListAsync();
            _lastRconPlayers = players;

            var filePlayers = LoadPlayersFile();
            if (filePlayers.Count == 0)
            {
                MessageBox.Show(
                    "Player list is empty. Click Refresh to load IsleServerMod_players.txt.",
                    "AI Admin UI",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            var combined = new List<PlayerItem>();

            if (filePlayers.Count == players.Count && filePlayers.Count > 0)
            {
                for (int i = 0; i < filePlayers.Count; i++)
                {
                    combined.Add(new PlayerItem
                    {
                        PlayerName = players[i].PlayerName,
                        RconId = players[i].PlayerId,
                        Ue4ssId = filePlayers[i].Ue4ssId
                    });
                }
            }
            else
            {
                combined.AddRange(filePlayers);
                combined.AddRange(players.Select(p => new PlayerItem
                {
                    PlayerName = p.PlayerName,
                    RconId = p.PlayerId
                }));
            }

            lstPlayers.ItemsSource = combined;
        }
        catch (Exception ex)
        {
            Log("RCON fetch error: " + ex);
            MessageBox.Show($"RCON fetch failed:\n{ex.Message}", "AI Admin UI", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            UpdateRconFetchState();
        }
    }

    private sealed class AiConfig
    {
        public bool Enabled { get; set; }
        public int IntervalMs { get; set; }
        public double MinPlayerDistance { get; set; }
        public double ManualMinPlayerDistance { get; set; }
        public int MaxSpawnAttempts { get; set; }
        public bool SafeManualSpawn { get; set; }
        public bool AutoReloadEnabled { get; set; }
        public int AutoReloadIntervalMs { get; set; }
        public int PlayerSyncIntervalMs { get; set; } = 60000;
        public int PlayersWriteIntervalMs { get; set; } = 15000;
        public bool DebugLogsEnabled { get; set; } = false;
        public bool DebugLogParsing { get; set; } = false;
        public bool DebugChatCommands { get; set; } = false;
        public bool DebugSpawning { get; set; } = false;
        public bool DebugConfig { get; set; } = false;
        public bool DebugCommandQueue { get; set; } = false;
        public bool SpawnApplyGrowth { get; set; } = true;
        public double SpawnGrowth { get; set; } = 1.0;
        public bool SpawnLogEnabled { get; set; } = false;
        public bool AutoSpawnCorpseOnly { get; set; } = false;
        public string AutoSpawnCorpseClass { get; set; } = "dead_dino";
        public double AutoSpawnCorpseScale { get; set; } = 0.1;
        public bool ChatCommandsEnabled { get; set; } = false;
        public bool RequireAdminForCommands { get; set; } = true;
        public int AdminLogParseIntervalMs { get; set; } = 60000;
        public int AdminLogParseMaxLines { get; set; } = 10000;
        public Dictionary<string, int> Rules { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string RconHost { get; set; } = "";
        public string RconPort { get; set; } = "";
        public string RconPass { get; set; } = "";

        // Fish respawn settings
        public bool FishRespawnEnabled { get; set; } = false;
        public int FishRespawnIntervalMs { get; set; } = 300000;  // 5 minutes default
        public bool FishRespawnOnlyFar { get; set; } = true;
        public double FishRespawnMinPlayerDistance { get; set; } = 3000.0;  // 30 meters default
        public int FishRespawnFishAmount { get; set; } = -1;
        public string FishRespawnFishToSpawn { get; set; } = "";

        // Hunger-based corpse spawn settings
        public bool HungerCorpseEnabled { get; set; } = false;
        public float HungerCorpseThreshold { get; set; } = 20.0f;
        public int HungerCorpseCooldownMs { get; set; } = 300000;
        public int HungerCorpseCheckIntervalMs { get; set; } = 5000;
        public bool HungerCorpseCarnivoreOnly { get; set; } = true;
        public float HungerCorpseSpawnRadiusMin { get; set; } = 200.0f;
        public float HungerCorpseSpawnRadiusMax { get; set; } = 600.0f;
        public string HungerCorpseSpecies { get; set; } = "";
        public bool HungerCorpseMatchSize { get; set; } = true;
        public int HungerCorpseMaxPlayersPerCheck { get; set; } = 100;
    }

    private sealed class RuleItem
    {
        public string Species { get; set; } = "";
        public int Max { get; set; }
    }

    private sealed class PlayerItem
    {
        public string PlayerName { get; set; } = "";
        public string Ue4ssId { get; set; } = "";
        public string RconId { get; set; } = "";

        public override string ToString()
        {
            string name = string.IsNullOrWhiteSpace(PlayerName) ? "Player" : PlayerName;
            if (!string.IsNullOrWhiteSpace(RconId) && !string.IsNullOrWhiteSpace(Ue4ssId))
            {
                return $"{name} ({RconId}) ({Ue4ssId})";
            }
            if (!string.IsNullOrWhiteSpace(Ue4ssId))
            {
                return $"{name} ({Ue4ssId})";
            }
            return $"{name} ({RconId})";
        }
    }

    private readonly string _commandPath;
    private readonly string _statusPath;
    private readonly string _playersPath;
    private readonly string _rconPath;
    private List<RconPlayer> _lastRconPlayers = new();

    private async Task WriteCommandAsync(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(_commandPath) ?? ".");

        // UI commands are written without Steam ID (server owner has direct file access)
        await File.AppendAllTextAsync(_commandPath, command.Trim() + Environment.NewLine);
    }

    private void RefreshStatus()
    {
        try
        {
            if (!File.Exists(_statusPath))
            {
                txtLastCommand.Text = "";
                txtLastUpdated.Text = "";
                txtLastSpawn.Text = "";
                txtStatusCounts.Text = "Status file not found: " + _statusPath;
                return;
            }

            var lines = ReadAllLinesShared(_statusPath);
            string lastCommand = "";
            string lastUpdated = "";
            string lastSpawn = "";
            var counts = new List<string>();
            bool inCounts = false;

            foreach (var raw in lines)
            {
                var line = raw ?? "";
                if (line.StartsWith("last_command=", StringComparison.OrdinalIgnoreCase))
                {
                    lastCommand = line.Substring("last_command=".Length);
                    continue;
                }
                if (line.StartsWith("last_updated=", StringComparison.OrdinalIgnoreCase))
                {
                    lastUpdated = line.Substring("last_updated=".Length);
                    continue;
                }
                if (line.StartsWith("last_spawn=", StringComparison.OrdinalIgnoreCase))
                {
                    lastSpawn = line.Substring("last_spawn=".Length);
                    continue;
                }
                if (line.StartsWith("counts:", StringComparison.OrdinalIgnoreCase))
                {
                    inCounts = true;
                    continue;
                }
                if (inCounts && !string.IsNullOrWhiteSpace(line))
                {
                    counts.Add(line.Trim());
                }
            }

            txtLastCommand.Text = lastCommand;
            txtLastUpdated.Text = lastUpdated;
            txtLastSpawn.Text = lastSpawn;
            txtStatusCounts.Text = counts.Count > 0 ? string.Join(Environment.NewLine, counts) : "";
        }
        catch (Exception ex)
        {
            Log("RefreshStatus error: " + ex);
            MessageBox.Show($"RefreshStatus failed:\n{ex.Message}", "AI Admin UI", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshPlayers()
    {
        try
        {
            if (!File.Exists(_playersPath))
            {
                txtPlayerId.Text = "Players list missing: " + _playersPath;
                if (_lastRconPlayers.Count > 0)
                {
                    lstPlayers.ItemsSource = _lastRconPlayers
                        .Select(p => new PlayerItem { PlayerName = p.PlayerName, RconId = p.PlayerId })
                        .ToList();
                }
                else
                {
                    lstPlayers.ItemsSource = new[] { "Players file not found" };
                }
                return;
            }

            var filePlayers = LoadPlayersFile();
            if (filePlayers.Count == 0 && _lastRconPlayers.Count > 0)
            {
                lstPlayers.ItemsSource = _lastRconPlayers
                    .Select(p => new PlayerItem { PlayerName = p.PlayerName, RconId = p.PlayerId })
                    .ToList();
                return;
            }

            if (_lastRconPlayers.Count > 0)
            {
                var combined = new List<PlayerItem>();
                if (filePlayers.Count == _lastRconPlayers.Count && filePlayers.Count > 0)
                {
                    for (int i = 0; i < filePlayers.Count; i++)
                    {
                        combined.Add(new PlayerItem
                        {
                            PlayerName = _lastRconPlayers[i].PlayerName,
                            RconId = _lastRconPlayers[i].PlayerId,
                            Ue4ssId = filePlayers[i].Ue4ssId
                        });
                    }
                    lstPlayers.ItemsSource = combined;
                }
                else
                {
                    combined.AddRange(filePlayers);
                    combined.AddRange(_lastRconPlayers.Select(p => new PlayerItem
                    {
                        PlayerName = p.PlayerName,
                        RconId = p.PlayerId
                    }));
                    lstPlayers.ItemsSource = combined;
                }
                return;
            }

            lstPlayers.ItemsSource = filePlayers;
        }
        catch (Exception ex)
        {
            Log("RefreshPlayers error: " + ex);
            MessageBox.Show($"RefreshPlayers failed:\n{ex.Message}", "AI Admin UI", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            UpdateRconFetchState();
        }
    }

    private List<PlayerItem> LoadPlayersFile()
    {
        var items = new List<PlayerItem>();
        if (!File.Exists(_playersPath))
        {
            return items;
        }

        foreach (var line in ReadAllLinesShared(_playersPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split('|');
            if (parts.Length >= 2)
            {
                items.Add(new PlayerItem
                {
                    PlayerName = parts[0],
                    Ue4ssId = parts[1]
                });
            }
        }

        return items;
    }

    private void UpdateRconFetchState()
    {
        bool hasPlayers = false;
        foreach (var item in lstPlayers.Items)
        {
            if (item is PlayerItem)
            {
                hasPlayers = true;
                break;
            }
        }
        btnFetchPlayersRcon.IsEnabled = hasPlayers;
    }

    private void LoadRconSettings()
    {
        try
        {
            if (!File.Exists(_rconPath))
            {
                return;
            }

            foreach (var line in File.ReadAllLines(_rconPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2) continue;
                switch (parts[0].ToLowerInvariant())
                {
                    case "host":
                        txtRconHost.Text = parts[1];
                        break;
                    case "port":
                        txtRconPort.Text = parts[1];
                        break;
                    case "password":
                        txtRconPass.Text = parts[1];
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Log("LoadRconSettings error: " + ex);
        }
    }

    private void SaveRconSettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_rconPath) ?? ".");
        var lines = new[]
        {
            "host=" + txtRconHost.Text.Trim(),
            "port=" + txtRconPort.Text.Trim(),
            "password=" + txtRconPass.Text
        };
        File.WriteAllLines(_rconPath, lines);
    }

    private static List<string> ReadAllLinesShared(string path)
    {
        var lines = new List<string>();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lines.Add(line);
        }
        return lines;
    }

    private static void Log(string message)
    {
        try
        {
            string logPath = Path.Combine(AppContext.BaseDirectory, "AiAdminUi.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Swallow logging failures
        }
    }

    private void UpdateDirtyState()
    {
        if (_isLoading || _originalConfig == null) return;
        btnSave.Tag = IsConfigDirty() ? "Dirty" : null;
    }

    private bool IsConfigDirty()
    {
        if (_originalConfig == null) return false;
        var current = CollectConfigFromUi();

        if (current.Enabled != _originalConfig.Enabled) return true;
        if (current.IntervalMs != _originalConfig.IntervalMs) return true;
        if (Math.Abs(current.MinPlayerDistance - _originalConfig.MinPlayerDistance) > 0.001) return true;
        if (Math.Abs(current.ManualMinPlayerDistance - _originalConfig.ManualMinPlayerDistance) > 0.001) return true;
        if (current.MaxSpawnAttempts != _originalConfig.MaxSpawnAttempts) return true;
        if (current.SafeManualSpawn != _originalConfig.SafeManualSpawn) return true;
        if (current.AutoReloadEnabled != _originalConfig.AutoReloadEnabled) return true;
        if (current.AutoReloadIntervalMs != _originalConfig.AutoReloadIntervalMs) return true;
        if (current.PlayerSyncIntervalMs != _originalConfig.PlayerSyncIntervalMs) return true;
        if (current.PlayersWriteIntervalMs != _originalConfig.PlayersWriteIntervalMs) return true;
        if (current.DebugLogsEnabled != _originalConfig.DebugLogsEnabled) return true;
        if (current.DebugLogParsing != _originalConfig.DebugLogParsing) return true;
        if (current.DebugChatCommands != _originalConfig.DebugChatCommands) return true;
        if (current.DebugSpawning != _originalConfig.DebugSpawning) return true;
        if (current.DebugConfig != _originalConfig.DebugConfig) return true;
        if (current.DebugCommandQueue != _originalConfig.DebugCommandQueue) return true;
        if (current.SpawnApplyGrowth != _originalConfig.SpawnApplyGrowth) return true;
        if (Math.Abs(current.SpawnGrowth - _originalConfig.SpawnGrowth) > 0.001) return true;
        if (current.SpawnLogEnabled != _originalConfig.SpawnLogEnabled) return true;
        if (current.AutoSpawnCorpseOnly != _originalConfig.AutoSpawnCorpseOnly) return true;
        if (!string.Equals(current.AutoSpawnCorpseClass, _originalConfig.AutoSpawnCorpseClass, StringComparison.Ordinal)) return true;
        if (Math.Abs(current.AutoSpawnCorpseScale - _originalConfig.AutoSpawnCorpseScale) > 0.001) return true;
        if (current.ChatCommandsEnabled != _originalConfig.ChatCommandsEnabled) return true;
        if (current.RequireAdminForCommands != _originalConfig.RequireAdminForCommands) return true;
        if (current.AdminLogParseIntervalMs != _originalConfig.AdminLogParseIntervalMs) return true;
        if (current.AdminLogParseMaxLines != _originalConfig.AdminLogParseMaxLines) return true;
        if (current.FishRespawnEnabled != _originalConfig.FishRespawnEnabled) return true;
        if (current.FishRespawnIntervalMs != _originalConfig.FishRespawnIntervalMs) return true;
        if (current.FishRespawnOnlyFar != _originalConfig.FishRespawnOnlyFar) return true;
        if (Math.Abs(current.FishRespawnMinPlayerDistance - _originalConfig.FishRespawnMinPlayerDistance) > 0.01) return true;
        if (current.FishRespawnFishAmount != _originalConfig.FishRespawnFishAmount) return true;
        if (!string.Equals(current.FishRespawnFishToSpawn, _originalConfig.FishRespawnFishToSpawn, StringComparison.Ordinal)) return true;
        if (current.HungerCorpseEnabled != _originalConfig.HungerCorpseEnabled) return true;
        if (Math.Abs(current.HungerCorpseThreshold - _originalConfig.HungerCorpseThreshold) > 0.01f) return true;
        if (current.HungerCorpseCooldownMs != _originalConfig.HungerCorpseCooldownMs) return true;
        if (current.HungerCorpseCheckIntervalMs != _originalConfig.HungerCorpseCheckIntervalMs) return true;
        if (current.HungerCorpseCarnivoreOnly != _originalConfig.HungerCorpseCarnivoreOnly) return true;
        if (Math.Abs(current.HungerCorpseSpawnRadiusMin - _originalConfig.HungerCorpseSpawnRadiusMin) > 0.01f) return true;
        if (Math.Abs(current.HungerCorpseSpawnRadiusMax - _originalConfig.HungerCorpseSpawnRadiusMax) > 0.01f) return true;
        if (!string.Equals(current.HungerCorpseSpecies, _originalConfig.HungerCorpseSpecies, StringComparison.Ordinal)) return true;
        if (current.HungerCorpseMatchSize != _originalConfig.HungerCorpseMatchSize) return true;
        if (current.HungerCorpseMaxPlayersPerCheck != _originalConfig.HungerCorpseMaxPlayersPerCheck) return true;
        if (current.RconHost != _originalConfig.RconHost) return true;
        if (current.RconPort != _originalConfig.RconPort) return true;
        if (current.RconPass != _originalConfig.RconPass) return true;

        if (current.Rules.Count != _originalConfig.Rules.Count) return true;
        foreach (var kvp in current.Rules)
        {
            if (!_originalConfig.Rules.TryGetValue(kvp.Key, out int originalVal) || kvp.Value != originalVal)
            {
                return true;
            }
        }

        return false;
    }
}
