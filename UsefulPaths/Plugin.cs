using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using ServerSync;
using UnityEngine;
using UsefulPaths.Managers;

namespace UsefulPaths
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class UsefulPathsPlugin : BaseUnityPlugin
    {
        internal const string ModName = "UsefulPaths";
        internal const string ModVersion = "1.0.5";
        internal const string Author = "RustyMods";
        private const string ModGUID = Author + "." + ModName;
        private static readonly string ConfigFileName = ModGUID + ".cfg";
        private static readonly string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource UsefulPathsLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        private static readonly ConfigSync ConfigSync = new(ModGUID)
            { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };
        private static UsefulPathsPlugin _Plugin = null!;
        public enum Toggle { On = 1, Off = 0 }

        public static readonly Dictionary<GroundTypes, ConfigEntry<float>> m_speed = new();
        public static readonly Dictionary<GroundTypes, ConfigEntry<float>> m_staminaRegen = new();
        public static readonly Dictionary<GroundTypes, ConfigEntry<float>> m_runStaminaDrain = new();
        public static readonly Dictionary<GroundTypes, ConfigEntry<float>> m_carryWeight = new();
        public static readonly Dictionary<GroundTypes, ConfigEntry<float>> m_jump = new();
        public static readonly Dictionary<GroundTypes, ConfigEntry<float>> m_vagonMass = new();

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        public static ConfigEntry<float> m_update = null!;
        public static ConfigEntry<Toggle> m_enabled = null!;

        public static ConfigEntry<Toggle> m_showIcon = null!;
        public static ConfigEntry<Toggle> m_applyToCreatures = null!;

        private void InitConfigs()
        {
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);
            m_update = config("2 - Settings", "Update Rate", 1f, new ConfigDescription("Set the rate to check terrain", new AcceptableValueRange<float>(1f, 10f)));
            m_enabled = config("2 - Settings", "Enabled", Toggle.On, "If on, plugin is active and enabled");
            m_showIcon = config("2 - Settings", "Display Icon", Toggle.On, "If on, status effect will be displayed on HUD");
            m_applyToCreatures = config("2 - Settings", "Tames", Toggle.Off, "If on, path effects are applied to tamed creatures");

            foreach (GroundTypes type in Enum.GetValues(typeof(GroundTypes)))
            {
                if (type is GroundTypes.None) continue;
                ConfigEntry<float> speed = _Plugin.config(type.ToString(), "Speed Modifier", 1f,
                    new ConfigDescription($"Set the speed modifier for {type.ToString()}",
                        new AcceptableValueRange<float>(0f, 10f)));
                m_speed[type] = speed;
                ConfigEntry<float> staminaRegen = _Plugin.config(type.ToString(), "Stamina Regeneration", 1f,
                    new ConfigDescription($"Set the stamina regeneration for {type.ToString()}",
                        new AcceptableValueRange<float>(0f, 10f)));
                m_staminaRegen[type] = staminaRegen;
                ConfigEntry<float> runStaminaDrain = _Plugin.config(type.ToString(), "Run Stamina Drain", 1f,
                    new ConfigDescription($"Set the run stamina drain for {type.ToString()}",
                        new AcceptableValueRange<float>(0f, 10f)));
                m_runStaminaDrain[type] = runStaminaDrain;
                ConfigEntry<float> carryWeight = _Plugin.config(type.ToString(), "Max Carry Weight", 0f,
                    new ConfigDescription($"Set the max carry weight of {type.ToString()}",
                        new AcceptableValueRange<float>(-100f, 100f)));
                m_carryWeight[type] = carryWeight;
                ConfigEntry<float> jump = _Plugin.config(type.ToString(), "Jump Modifier", 1f,
                    new ConfigDescription($"Set the jump modifier for {type.ToString()}",
                        new AcceptableValueRange<float>(0f, 10f)));
                m_jump[type] = jump;
                ConfigEntry<float> vagonMass = config(type.ToString(), "Cart Modifier", 1f,
                    new ConfigDescription($"Set the cart mass modifier for {type.ToString()}",
                        new AcceptableValueRange<float>(0f, 10f)));
                m_vagonMass[type] = vagonMass;
            }
        }

        public void Awake()
        {
            _Plugin = this;
            InitConfigs();
            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }
        
        private void Update()
        {
            float dt = Time.deltaTime;
            Managers.UsefulPaths.UpdateStatusEffect(dt);
        }

        private void OnDestroy() => Config.Save();
        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                UsefulPathsLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                UsefulPathsLogger.LogError($"There was an issue loading your {ConfigFileName}");
                UsefulPathsLogger.LogError("Please check your config entries for spelling and format!");
            }
        }
        
        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order;
            [UsedImplicitly] public bool? Browsable;
            [UsedImplicitly] public string? Category;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
        }
    }

    public static class KeyboardExtensions
    {
        public static bool IsKeyDown(this KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None && Input.GetKeyDown(shortcut.MainKey) &&
                   shortcut.Modifiers.All(Input.GetKey);
        }

        public static bool IsKeyHeld(this KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None && Input.GetKey(shortcut.MainKey) &&
                   shortcut.Modifiers.All(Input.GetKey);
        }
    }
}