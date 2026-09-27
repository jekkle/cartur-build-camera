// Cartur's Build Camera - a maintained fork of Azumatt's Build Camera Custom Hammers Edition,
// which was itself a fork of gittywithexcitement's / CookieMilk's Valheim Build Camera.
// Licensed GPL-3.0, same as both upstreams. See LICENSE.txt.
//
// Why this fork exists: Azumatt deprecated the Thunderstore package on version 1.3.1, and that
// build is broken on Valheim 1.0.x. C# bakes optional argument counts into the call site, and
// 1.0 dropped the trailing bool from ZInput.GetJoyLeftStickX/Y and GetJoyRightStickX/Y, so
// 1.3.1 references four signatures that no longer exist and throws every frame on controller.
// Resolving 1.3.1 against Valheim 1.0.16 with Mono.Cecil turns up exactly those four and
// nothing else. He fixed it in an unreleased 1.3.2 that only ever reached Nexus.
using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Bootstrap;
using HarmonyLib;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using ServerSync;
using UnityEngine;

namespace Valheim_Build_Camera;

[BepInPlugin(ModGUID, ModName, ModVersion)]
public class Valheim_Build_CameraPlugin : BaseUnityPlugin
{
	internal const string ModName = "Cartur's Build Camera";
	internal const string ModVersion = "1.0.0";

	/// Matches the other Cartur mods' GUID shape, which is also the config file name.
	private const string ModGUID = "com.jekkle.valheim.carturbuildcamera";

	/// Separate from ModName because this ends up inside RPC names, which should not carry
	/// spaces or an apostrophe.
	internal const string RpcPrefix = "CarturBuildCamera";

	/// Azumatt's original. If it is loaded we patch nothing - both mods take over
	/// GameCamera.UpdateCamera and would fight over one transform.
	private const string UpstreamGUID = "Azumatt.BuildCameraCHE";

	private readonly Harmony _harmony = new(ModGUID);
	private FileSystemWatcher? _configWatcher;
	private static readonly string ConfigFileName = ModGUID + ".cfg";
	private static readonly string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
	private static readonly string UpstreamConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + UpstreamGUID + ".cfg";
	internal static string ConnectionError = "";
	public static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(ModName);

	/// Set when the upstream mod is present, so Update() also stands down.
	private bool _standDown;

	// MinimumRequiredVersion is a deliberate floor, not ModVersion. Upstream pinned it to
	// CurrentVersion, which makes every patch release hard-disconnect clients that have not
	// updated yet, for changes that are not protocol changes.
	internal const string MinimumCompatibleVersion = "1.0.0";

	private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = MinimumCompatibleVersion };

	internal static bool BuildCameraActive;

	internal static float CameraYaw;
	internal static float CameraPitch;

	public enum Toggle
	{
		On = 1,
		Off = 0,
	}

	public enum RestrictionMode
	{
		Off,
		CameraNeedsCoziness,
		CameraPickUpNeedsCoziness,
	}

	public enum CameraLightShadows
	{
		Original,
		Off,
		Hard,
		Soft,
	}

	void Awake()
	{
		if (Chainloader.PluginInfos.ContainsKey(UpstreamGUID))
		{
			_standDown = true;
			Log.LogWarning($"{UpstreamGUID} is installed - standing down and patching nothing. Remove one of the two.");
			return;
		}

		AdoptUpstreamConfig();

		_serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
		_ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

		distanceCanBuildFromAvatar = config("General", "Distance Can Build From Avatar", 100f, "Distance from your avatar that you can build or repair. (Valheim default is 8)");

		distanceCanBuildFromWorkbench = config("General", "Distance Can Build From Workbench", 100f, "Maximum distance between the build camera and its workbench/stonecutter/etc. This does not change the station's actual build or player-base radius.");

		resourcePickupRange = config("General", "Resource Pickup Range", 10f, "Distance from which you can pick up resources on the ground while in build mode. (Valheim default is 2)");

		cameraRangeMultiplier = config("General", "Camera Range Multiplier", 1f, "Changes maximum range camera can move away from the build station. 1 means the build station's range, 2 means twice the build station range, etc.");

		cameraMoveSpeedMultiplier = config("General", "Camera Move Speed Multiplier", 3f, "Multiplies the speed at which the build camera pans (i.e. moves around).");

		cameraTerrainClearance = config("General", "Camera Terrain Clearance", 1f, new ConfigDescription("Radius kept clear around the detached camera so it cannot cross terrain or a HearthBelow cave wall.", new AcceptableValueRange<float>(0.1f, 2f)));

		restrictionMode = config("General", "Restriction Mode", RestrictionMode.CameraPickUpNeedsCoziness, "Off: no cozy restriction. CameraNeedsCoziness: cozy is required to enter and stay in camera mode. CameraPickUpNeedsCoziness: camera entry is allowed, but camera pickup requires cozy.");

		minimumComfortLevel = config("General", "Minimum Comfort Level", 1, new ConfigDescription("Minimum comfort required by the selected restriction mode.", new AcceptableValueRange<int>(1, 30)));

		moveWithRespectToWorld = config("General", "Move With Respect To World", Toggle.Off,
			"When true, camera panning input (e.g. pressing WASD) moves the camera with respect to the " +
			"world coordinates. This means that turning the camera has no effect on the direction of " +
			"movement. For example, pressing W will always move the camera toward the world's 'North', " +
			"as opposed to the direction the camera is currently facing.");

		// Shift+B, not the bare B upstream used. Plain B is crowded: Cartur's UI HUD binds quick
		// slot 3 to it, EquipmentAndQuickSlots binds its slot 3 to it, and OdinHorse binds
		// saddlebags to it - so one press toggled the camera and ate whatever was in that slot.
		// BepInEx's KeyboardShortcut.ModifierKeyTest blocks a shortcut whose modifiers are not all
		// held, and its block list covers every key code except the mouse buttons, so a bare "B"
		// shortcut does not fire while Shift is down. That is what keeps the two apart.
		toggleBuildMode = config("Hotkeys", "Toggle build mode", new KeyboardShortcut(KeyCode.B, KeyCode.LeftShift), "See https://docs.unity3d.com/ScriptReference/KeyCode.html for the names of all key codes. To add one or more modifier keys, separate them with +, like so: Toggle build mode = B + LeftControl", false);

		verboseLogging = config("General", "Verbose Logging", Toggle.Off, "When true, increases verbosity of logging. Enable this if you're wondering why you're unable to enable the Build Camera.", false);

		demisterFollowCamera = config("Mist", "Demister Follow Camera", Toggle.On, "When enabled and you have the Demister status effect (Wisplight), the demister ball will follow the build camera instead of your character while in build mode.");

		demisterRangeMultiplier = config("Mist", "Demister Range Multiplier", 2.5f, "Multiplier for the demister's mist-clearing range while in build mode. Higher values clear more mist. (Default: 2.5x, Vanilla: 1.0x)");

		followDvergrCircletLight = config("Helmet Light", "Follow Dvergr Circlet Light", Toggle.On, "Moves the equipped Dvergr circlet light to the detached camera. Also supports CircletExtended and RaziCirclet custom slots when installed.");

		helmetLightOffsetForward = config("Helmet Light", "Camera Light Forward/Back Offset", 0.65f, new ConfigDescription("Circlet light offset on the camera's forward axis.", new AcceptableValueRange<float>(-5f, 5f)), false);

		helmetLightOffsetUp = config("Helmet Light", "Camera Light Up/Down Offset", -0.08f, new ConfigDescription("Circlet light offset on the camera's up axis.", new AcceptableValueRange<float>(-5f, 5f)), false);

		helmetLightOffsetRight = config("Helmet Light", "Camera Light Left/Right Offset", 0f, new ConfigDescription("Circlet light offset on the camera's right axis. Negative values move it left.", new AcceptableValueRange<float>(-5f, 5f)), false);

		helmetLightIntensityMultiplier = config("Helmet Light", "Camera Light Intensity Multiplier", 1f, new ConfigDescription("Multiplies the equipped circlet light's current intensity.", new AcceptableValueRange<float>(0f, 5f)));

		helmetLightRangeMultiplier = config("Helmet Light", "Camera Light Range Multiplier", 1f, new ConfigDescription("Multiplies the equipped circlet light's current range.", new AcceptableValueRange<float>(0f, 5f)));

		helmetLightColor = config("Helmet Light", "Camera Light Color", "Original", "Original uses the equipped circlet light color. Otherwise use a hex color such as FFD27F or FFD27FFF.");

		helmetLightShadows = config("Helmet Light", "Camera Light Shadows", CameraLightShadows.Original, "Original uses the equipped circlet light's shadow mode. Off, Hard, and Soft override it.");

		pickupHudX = config("HUD", "Pickup Blocked HUD X", 0.3f, new ConfigDescription("Horizontal pickup-blocked message position as a screen ratio.", new AcceptableValueRange<float>(0f, 1f)), false);

		pickupHudY = config("HUD", "Pickup Blocked HUD Y", 0.3f, new ConfigDescription("Vertical pickup-blocked message position (1 = top, 0 = bottom).", new AcceptableValueRange<float>(0f, 1f)), false);

		pickupHudFontSize = config("HUD", "Pickup Blocked HUD Font Size", 19, new ConfigDescription("Pickup-blocked message body size. The font and panel styling come from Valheim's HUD.", new AcceptableValueRange<int>(12, 32)), false);

		invertControllerLookHorizontal = config("Controls", "Invert Controller Look Horizontal", Toggle.Off, "Inverts the horizontal (left/right) look axis for the controller right stick while in build camera mode.", false);
		invertControllerLookVertical = config("Controls", "Invert Controller Look Vertical", Toggle.Off, "Inverts the vertical (up/down) look axis for the controller right stick while in build camera mode.", false);
		invertMouseLookHorizontal = config("Controls", "Invert Mouse Look Horizontal", Toggle.Off, "Inverts the horizontal (left/right) look axis for the mouse while in build camera mode.", false);
		invertMouseLookVertical = config("Controls", "Invert Mouse Look Vertical", Toggle.Off, "Inverts the vertical (up/down) look axis for the mouse while in build camera mode. This is separate from the game's built-in mouse invert setting.", false);

		// A mod that throws in Awake takes the whole chainloader down with it, so the game would
		// not start at all. Log and let everything else load.
		try
		{
			_harmony.PatchAll(Assembly.GetExecutingAssembly());
		}
		catch (Exception ex)
		{
			_standDown = true;
			Log.LogWarning($"Patching failed, build camera disabled: {ex.Message}");
			return;
		}

		Utils.AddLocalizations(Localization.instance);
		SetupWatcher();
		Log.LogInfo("Patched Player.Update, GameCamera.UpdateCamera, PlayerController.TakeInput, Player build-distance raycasts, CraftingStation.HaveBuildStationInRange and SE_Demister");
	}

	/// One-time convenience: if Azumatt's config is on disk and ours is not, start from his
	/// values so an existing setup carries over. Every section and key is identical because
	/// this fork keeps them for parity, so a plain copy is the whole migration - BepInEx
	/// ignores the header comment naming his plugin, and rewrites it on first save.
	private void AdoptUpstreamConfig()
	{
		if (File.Exists(ConfigFileFullPath) || !File.Exists(UpstreamConfigFileFullPath)) return;
		try
		{
			File.Copy(UpstreamConfigFileFullPath, ConfigFileFullPath);
			Log.LogInfo($"No {ConfigFileName} yet - seeded it from {UpstreamGUID}.cfg");
		}
		catch (Exception ex)
		{
			Log.LogWarning($"Could not seed config from {UpstreamGUID}.cfg, using defaults: {ex.Message}");
		}
	}

	private void Update()
	{
		if (_standDown) return;
		PickupBlockedHud.Update();
	}

	private void OnDestroy()
	{
		if (_configWatcher != null)
		{
			_configWatcher.EnableRaisingEvents = false;
			_configWatcher.Changed -= ReadConfigValues;
			_configWatcher.Created -= ReadConfigValues;
			_configWatcher.Renamed -= ReadConfigValues;
			_configWatcher.Dispose();
			_configWatcher = null;
		}
		PickupBlockedHud.Cleanup();
		Utils.DisableBuildMode();
		_harmony.UnpatchSelf();
		Config.Save();
	}

	private void SetupWatcher()
	{
		_configWatcher = new FileSystemWatcher(Paths.ConfigPath, ConfigFileName);
		_configWatcher.Changed += ReadConfigValues;
		_configWatcher.Created += ReadConfigValues;
		_configWatcher.Renamed += ReadConfigValues;
		_configWatcher.IncludeSubdirectories = false;
		_configWatcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
		_configWatcher.EnableRaisingEvents = true;
	}

	private void ReadConfigValues(object sender, FileSystemEventArgs e)
	{
		if (!File.Exists(ConfigFileFullPath)) return;
		try
		{
			Log.LogDebug("ReadConfigValues called");
			Config.Reload();
		}
		catch
		{
			Log.LogError($"There was an issue loading your {ConfigFileName}");
			Log.LogError("Please check your config entries for spelling and format!");
		}
	}

	#region ConfigOptions

	private static ConfigEntry<Toggle> _serverConfigLocked = null!;
	internal static ConfigEntry<float> distanceCanBuildFromAvatar = null!;
	internal static ConfigEntry<float> distanceCanBuildFromWorkbench = null!;
	internal static ConfigEntry<float> resourcePickupRange = null!;
	internal static ConfigEntry<float> cameraRangeMultiplier = null!;
	internal static ConfigEntry<float> cameraMoveSpeedMultiplier = null!;
	internal static ConfigEntry<float> cameraTerrainClearance = null!;
	internal static ConfigEntry<RestrictionMode> restrictionMode = null!;
	internal static ConfigEntry<int> minimumComfortLevel = null!;
	internal static ConfigEntry<Toggle> moveWithRespectToWorld = null!;
	internal static ConfigEntry<KeyboardShortcut> toggleBuildMode = null!;
	internal static ConfigEntry<Toggle> verboseLogging = null!;
	internal static ConfigEntry<Toggle> demisterFollowCamera = null!;
	internal static ConfigEntry<float> demisterRangeMultiplier = null!;
	internal static ConfigEntry<Toggle> followDvergrCircletLight = null!;
	internal static ConfigEntry<float> helmetLightOffsetForward = null!;
	internal static ConfigEntry<float> helmetLightOffsetUp = null!;
	internal static ConfigEntry<float> helmetLightOffsetRight = null!;
	internal static ConfigEntry<float> helmetLightIntensityMultiplier = null!;
	internal static ConfigEntry<float> helmetLightRangeMultiplier = null!;
	internal static ConfigEntry<string> helmetLightColor = null!;
	internal static ConfigEntry<CameraLightShadows> helmetLightShadows = null!;
	internal static ConfigEntry<float> pickupHudX = null!;
	internal static ConfigEntry<float> pickupHudY = null!;
	internal static ConfigEntry<int> pickupHudFontSize = null!;
	internal static ConfigEntry<Toggle> invertControllerLookHorizontal = null!;
	internal static ConfigEntry<Toggle> invertControllerLookVertical = null!;
	internal static ConfigEntry<Toggle> invertMouseLookHorizontal = null!;
	internal static ConfigEntry<Toggle> invertMouseLookVertical = null!;

	private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
	{
		ConfigDescription extendedDescription = new(description.Description + (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"), description.AcceptableValues, description.Tags);
		ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);

		SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
		syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

		return configEntry;
	}

	private ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true)
	{
		return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
	}

	#endregion
}
