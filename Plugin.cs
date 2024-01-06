using BepInEx;
using GameNetcodeStuff;
using HarmonyLib;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace LCCinematicFreecam
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "io.daxcess.lccinematicfreecam";
        public const string PLUGIN_NAME = "LCCinematicFreecam";
        public const string PLUGIN_VERSION = "1.0.0";

        private void Awake()
        {
            LCCinematicFreecam.Logger.SetSource(Logger);

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

            Logger.LogInfo($"Inserted patches for {PLUGIN_NAME}");
        }
    }

    [HarmonyPatch]
    internal static class MainPatches
    {
        private static GameObject helmetObject;
        private static SkinnedMeshRenderer playerRenderer;
        
        private static Transform playerAudioListener;
        private static Transform mainCamera;
        private static Transform freeCamera;

        private static Camera cinematicCamera;

        [HarmonyPatch(typeof(StartOfRound), "Start")]
        [HarmonyPostfix]
        private static void OnGameEntered(StartOfRound __instance)
        {
            __instance.StartCoroutine(SetupFreecam());
        }

        [HarmonyPatch(typeof(StartOfRound), "OnDestroy")]
        [HarmonyPostfix]
        private static void OnGameLeft()
        {
            IngamePlayerSettings.Instance.playerInput.actions.FindAction("SetFreeCamera").performed -= SetFreeCamera_performed;
        }

        [HarmonyPatch(typeof(PlayerControllerB), "KillPlayer")]
        [HarmonyPostfix]
        private static void OnPlayerDeath(PlayerControllerB __instance)
        {
            if (!__instance.IsOwner)
                return;

            if (__instance.isFreeCamera)
                DisableFreecam();
        }

        private static IEnumerator SetupFreecam()
        {
            Logger.LogInfo("Setting up free camera");
            yield return new WaitUntil(() => StartOfRound.Instance.localPlayerController != null);
            
            var player = StartOfRound.Instance.localPlayerController;

            helmetObject = GameObject.Find("Systems").transform.Find("Rendering/PlayerHUDHelmetModel").gameObject;
            playerRenderer = player.transform.Find("ScavengerModel/LOD1").GetComponent<SkinnedMeshRenderer>();

            playerAudioListener = player.transform.Find("ScavengerModel/metarig/CameraContainer/MainCamera/PlayerAudioListener");
            mainCamera = player.transform.Find("ScavengerModel/metarig/CameraContainer/MainCamera");
            freeCamera = GameObject.Find("FreeCameraCinematic").transform;

            cinematicCamera = StartOfRound.Instance.freeCinematicCamera;

            IngamePlayerSettings.Instance.playerInput.actions.FindAction("SetFreeCamera").performed += SetFreeCamera_performed;
        }

        private static void SetFreeCamera_performed(InputAction.CallbackContext obj)
        {
            Logger.LogDebug("Freecamera keybind pressed");

            var instance = StartOfRound.Instance.localPlayerController;

            // Forward event to game
            typeof(PlayerControllerB).GetMethod("SetFreeCamera_performed", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, [obj]);

            if (instance.isFreeCamera)
                DisableFreecam();
            else if (!instance.isPlayerDead)
                EnableFreecam();
        }

        private static void EnableFreecam()
        {
            var player = StartOfRound.Instance.localPlayerController;
            
            Logger.Log("Enabling freecam");

            player.isFreeCamera = true;
            StartOfRound.Instance.SwitchCamera(cinematicCamera);
            cinematicCamera.cullingMask = 557520895;

            helmetObject.SetActive(false);
            player.thisPlayerModelArms.enabled = false;
            playerRenderer.shadowCastingMode = ShadowCastingMode.On;
            playerAudioListener.SetParent(freeCamera, false);

            HUDManager.Instance.HideHUD(true);
        }

        private static void DisableFreecam()
        {
            var player = StartOfRound.Instance.localPlayerController;

            Logger.Log($"Disabling freecam (Dead: {player.isPlayerDead})");

            player.isFreeCamera = false;
            StartOfRound.Instance.freeCinematicCamera.enabled = false;
            StartOfRound.Instance.SwitchCamera(player.isPlayerDead ? StartOfRound.Instance.spectateCamera : player.gameplayCamera);

            helmetObject.SetActive(true);
            player.thisPlayerModelArms.enabled = true;
            playerRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            playerAudioListener.SetParent(mainCamera, false);

            HUDManager.Instance.HideHUD(player.isPlayerDead);
        }
    }
}
