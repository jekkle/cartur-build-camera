using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Valheim_Build_Camera.Compatibility.WardIsLove;

namespace Valheim_Build_Camera
{
	[HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
	static class Localization_SetupLanguage_Patch
	{
		static void Postfix(Localization __instance)
		{
			Utils.AddLocalizations(__instance);
			PickupBlockedHud.LanguageChanged();
		}
	}

	[HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.HaveBuildStationInRange))]
	static class CraftingStation_HaveBuildStationInRange_Patch
	{
		static void Postfix(string name, Vector3 point, ref CraftingStation __result)
		{
			if (!__result && Utils.InBuildMode() && Utils.FindCameraStation(name, point) is { } station)
				__result = station;
		}
	}

	[HarmonyPatch]
	static class Player_BuildCameraPlacementDistance_Patch
	{
		static IEnumerable<MethodBase> TargetMethods()
		{
			yield return AccessTools.DeclaredMethod(typeof(Player), "PieceRayTest");
			yield return AccessTools.DeclaredMethod(typeof(Player), "UpdateWearNTearHover");
			yield return AccessTools.DeclaredMethod(typeof(Player), "CopyPiece");
			yield return AccessTools.DeclaredMethod(typeof(Player), "RemovePiece");
		}

		static void Prefix(ref float ___m_maxPlaceDistance, out float __state)
		{
			__state = ___m_maxPlaceDistance;
			if (Utils.InBuildMode())
				___m_maxPlaceDistance = Mathf.Max(___m_maxPlaceDistance, Valheim_Build_CameraPlugin.distanceCanBuildFromAvatar.Value);
		}

		static Exception Finalizer(Exception __exception, ref float ___m_maxPlaceDistance, float __state)
		{
			___m_maxPlaceDistance = __state;
			return __exception;
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.SetLocalPlayer))]
	static class Player_SetLocalPlayer_Patch
	{
		static void Postfix()
		{
			PickupBlockedHud.Cleanup();
			Utils.DisableBuildMode();
		}
	}

	/// Dying in build mode used to leave the camera detached with Player.Update skipped and
	/// PlayerController.TakeInput forced false, so you watched your own corpse and could not
	/// take control back. SetLocalPlayer only fires on logout or character switch, not on death.
	[HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
	static class Player_OnDeath_Patch
	{
		static void Postfix()
		{
			Utils.DisableBuildMode();
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.Update))]
	static class Player_Update_Patch
	{
		private static readonly string[] HotbarButtons =
		{
			"Hotbar1", "Hotbar2", "Hotbar3", "Hotbar4", "Hotbar5", "Hotbar6", "Hotbar7", "Hotbar8",
		};

		// Vanilla registers these alongside the plain ones, from a "Hotbar{0}Alt" format string in
		// ZInput.ResetKBMButtons. Upstream's loop left them out, so alt hotbar binds silently
		// stopped working while the build camera was up.
		private static readonly string[] HotbarAltButtons =
		{
			"Hotbar1Alt", "Hotbar2Alt", "Hotbar3Alt", "Hotbar4Alt", "Hotbar5Alt", "Hotbar6Alt", "Hotbar7Alt", "Hotbar8Alt",
		};

		// None of the early returns below assign __runOriginal = true. Harmony passes the running
		// value in, so writing true would resurrect a method an earlier prefix deliberately
		// skipped - leaving it alone is both correct and what "do nothing" means here.
		static void Prefix(Player __instance, ref bool __runOriginal)
		{
			if (!Utils.IsLocalPlayer(__instance) || !Utils.InBuildMode()) return;

			if (!Utils.CanUseCamera())
			{
				Utils.ShowComfortMessage();
				Utils.DisableBuildMode();
				return;
			}

			if (Utils.ShouldDeactivateBuildMode(__instance))
			{
				Utils.DisableBuildMode();
				return;
			}

			// A portal hop moves the player but leaves the camera clamped to a station in the
			// world just left, stranding it outside its own range check.
			if (__instance.IsTeleporting())
			{
				Utils.DisableBuildMode();
				return;
			}

			__runOriginal = false;
			if (!__instance.TakeInput()) return;
			// Vanilla's own hotbar loop in Player.Update is exactly:
			//     if (ZInput.GetButtonDown($"Hotbar{i}") || ZInput.GetButtonDown($"Hotbar{i}Alt"))
			//         UseHotbarItem(i);
			// Upstream instead read Input.GetKeyDown(KeyCode.Alpha1 + i) - the raw number row,
			// straight past ZInput - so in build camera the number keys fired hotbar slots even
			// for a player who had rebound them, and the Alt binds were ignored. Match vanilla.
			for (int index = 0; index < HotbarButtons.Length; ++index)
			{
				if (ZInput.GetButtonDown(HotbarButtons[index]) || ZInput.GetButtonDown(HotbarAltButtons[index]))
					__instance.UseHotbarItem(index + 1);
			}

			if (ZInput.GetButtonDown("Hide") || ZInput.GetButtonDown("JoyHide"))
			{
				if ((__instance.GetRightItem() != null || __instance.GetLeftItem() != null) && !__instance.InAttack())
					__instance.HideHandItems();
			}

			__instance.UpdatePlacement(true, Time.deltaTime);
		}

		static void Postfix(Player __instance)
		{
			if (!Utils.IsLocalPlayer(__instance) || !Valheim_Build_CameraPlugin.toggleBuildMode.Value.IsDown()) return;
			if (Utils.InBuildMode())
			{
				if (__instance.TakeInput()) Utils.DisableBuildMode();
				return;
			}

			if (!__instance.TakeInput())
			{
				Utils.LogWhenVerbose("Build Mode not enabled because chat, console, menu, inventory, map, or similar is open.");
				return;
			}

			if (!Utils.ToolIsEquipped(__instance))
			{
				Utils.LogWhenVerbose("Build Mode not enabled because hammer is not equipped.");
				return;
			}

			if (!Utils.BuildStationInRange(__instance))
			{
				Utils.LogWhenVerbose("Build Mode not enabled because no build station (e.g. workbench) is in range.");
				return;
			}

			if (!Utils.CanUseCamera())
			{
				Utils.ShowComfortMessage();
				Utils.LogWhenVerbose("Build Mode not enabled because the cozy and comfort requirement is not met.");
				return;
			}

			// Vanilla owns a detached camera of its own (GameCamera.UpdateFreeFly, reached through
			// the freefly console command). Our GameCamera.UpdateCamera prefix skips the original
			// outright, so entering build mode during free-fly puts two systems on one transform,
			// ours winning while free-fly's state keeps advancing underneath. InFreeFly is static
			// and returns false when there is no GameCamera, so it needs no guard of its own.
			if (GameCamera.InFreeFly())
			{
				Utils.LogWhenVerbose("Build Mode not enabled because the game's own free-fly camera is active.");
				return;
			}

			Utils.EnableBuildMode();
		}
	}

	[HarmonyPatch(typeof(PlayerController), nameof(PlayerController.TakeInput))]
	static class PlayerController_TakeInput_Patch
	{
		static void Prefix(ref bool __result, ref bool __runOriginal)
		{
			if (!Utils.InBuildMode()) return;

			__result = false;
			__runOriginal = false;
		}
	}

	[HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateCamera))]
	[HarmonyBefore("Azumatt.FirstPersonMode")]
	[HarmonyPriority(Priority.VeryHigh)]
	static class GameCamera_UpdateCamera_Patch
	{
		private const float PickupScanInterval = 0.1f;
		private static float _nextPickupScan;

		static void Prefix(float dt, GameCamera __instance, ref bool __runOriginal)
		{
			if (!Utils.InBuildMode())
			{
				_nextPickupScan = 0f;
				return;
			}

			Utils.UpdateBuildCamera(dt, __instance);
			if (Time.time >= _nextPickupScan)
			{
				_nextPickupScan = Time.time + PickupScanInterval;
				if (Utils.TryGetAutoPickupPlayer(out Player player))
				{
					if (WardAccess.Check(player.GetPlayerID(), __instance.transform.position)) Utils.AutoPickup(__instance, player);
				}
			}

			__runOriginal = false;
		}

		static void Postfix(GameCamera __instance)
		{
			if (Utils.InBuildMode()) DvergrCircletCameraLight.Update(__instance);
		}
	}
}
