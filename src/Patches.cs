using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace SilentWalker;

internal static class Patches
{
    private const uint PlayFootsteps = 3854155799;
    private const uint PlayFootstepsLimp = 2480385009;
    private static readonly uint SwPlayFootsteps = FootstepBank.Fnv(FootstepBank.EventFootsteps);
    private static readonly uint SwPlayFootstepsLimp = FootstepBank.Fnv(FootstepBank.EventLimp);
    private static readonly string[] GameWeights = { "InventoryWeightGeneral", "InventoryWeightMetal", "InventoryWeightWood", "InventoryWeightWater" };

    private static bool insidePlayerFootstep;
    private static int suppressedPosts;
    private static int redirectedPosts;
    private static int scaledHearings;

    [HarmonyPatch(typeof(FootStepSounds), nameof(FootStepSounds.PlayFootStepSound),
        typeof(Vector3), typeof(string), typeof(FootStepSounds.State))]
    private static class FootstepScopePatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            insidePlayerFootstep = true;
            if (Acoustic.Enabled && FootstepBankStore.Loaded)
            {
                SampleCarriedWeights();
            }
        }

        [HarmonyPostfix]
        private static void Postfix() => insidePlayerFootstep = false;

        [HarmonyFinalizer]
        private static void Finalizer() => insidePlayerFootstep = false;
    }

    [HarmonyPatch(typeof(GameAudioManager), nameof(GameAudioManager.PlaySound),
        typeof(uint), typeof(GameObject))]
    private static class FootstepPostPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(uint soundID, ref GameObject go, ref uint __result)
        {
            if (!insidePlayerFootstep || Acoustic.AudioIsVanilla)
            {
                return true;
            }

            if (soundID != PlayFootsteps && soundID != PlayFootstepsLimp)
            {
                return true;
            }

            if (Acoustic.AudioIsSilent)
            {
                __result = 0;
                if (Settings.DebugLogging && suppressedPosts++ % 50 == 0)
                {
                    Core.Logger.Msg($"footstep post {soundID} suppressed (count {suppressedPosts})");
                }

                return false;
            }

            var ours = FootstepEmitter.Ensure();
            if (ours == null)
            {
                return true;
            }

            FootstepBankStore.EnsureLoaded();
            FootstepEmitter.ApplyGain();
            if (FootstepBankStore.Loaded)
            {
                ApplyLayers();
                __result = AkSoundEngine.PostEvent(soundID == PlayFootsteps ? SwPlayFootsteps : SwPlayFootstepsLimp, ours);
                if (Settings.DebugLogging && redirectedPosts++ % 50 == 0)
                {
                    Core.Logger.Msg($"footstep post {soundID} replaced by the mod copy at {Acoustic.AudioFactor:F2} (count {redirectedPosts})");
                }

                return false;
            }

            go = ours;
            if (Settings.DebugLogging && redirectedPosts++ % 50 == 0)
            {
                Core.Logger.Msg($"footstep post {soundID} redirected to mod emitter at {Acoustic.AudioFactor:F2} (count {redirectedPosts})");
            }

            return true;
        }
    }

    private static readonly int[] GameToAcoustic = { Acoustic.General, Acoustic.Metal, Acoustic.Wood, Acoustic.Water };

    private static void SampleCarriedWeights()
    {
        for (int i = 0; i < GameWeights.Length; i++)
        {
            float kg = 0f;
            int type = 1;
            AkSoundEngine.GetRTPCValue(GameWeights[i], (ulong)0, 0u, out kg, ref type);
            Acoustic.SetCarried(GameToAcoustic[i], kg);
            AkSoundEngine.SetRTPCValue(FootstepBank.WeightParams[i], kg);
        }
    }

    private static void ApplyLayers()
    {
        AkSoundEngine.SetRTPCValue(FootstepBank.ParamGroundVolume, Acoustic.GroundAmplitude * 100f);
        AkSoundEngine.SetRTPCValue(FootstepBank.ParamClothingVolume, Acoustic.ClothingAmplitude * 100f);
        AkSoundEngine.SetRTPCValue(FootstepBank.ParamPackMuffle, Acoustic.PackMuffle);
        AkSoundEngine.SetRTPCValue(FootstepBank.ParamMetalWrap, Acoustic.MetalWrap);
        for (int m = 0; m < 4; m++)
        {
            AkSoundEngine.SetRTPCValue(FootstepBank.PackVolumeParams[m], Acoustic.PackAmplitude(m) * 100f);
        }
    }

    [HarmonyPatch(typeof(AkSoundEngine), nameof(AkSoundEngine.SetSwitch),
        typeof(uint), typeof(uint), typeof(GameObject))]
    private static class SwitchMirrorPatch
    {
        [HarmonyPostfix]
        private static void Postfix(uint in_switchGroup, uint in_switchState, GameObject in_gameObjectID)
        {
            var ours = FootstepEmitter.Current;
            if (ours == null || !FootstepEmitter.IsVanillaEmitter(in_gameObjectID))
            {
                return;
            }

            AkSoundEngine.SetSwitch(in_switchGroup, in_switchState, ours);
        }
    }

    [HarmonyPatch(typeof(AkSoundEngine), nameof(AkSoundEngine.SetSwitch),
        typeof(string), typeof(string), typeof(GameObject))]
    private static class SwitchMirrorByNamePatch
    {
        [HarmonyPostfix]
        private static void Postfix(string in_pszSwitchGroup, string in_pszSwitchState, GameObject in_gameObjectID)
        {
            var ours = FootstepEmitter.Current;
            if (ours == null || !FootstepEmitter.IsVanillaEmitter(in_gameObjectID))
            {
                return;
            }

            AkSoundEngine.SetSwitch(in_pszSwitchGroup, in_pszSwitchState, ours);
        }
    }

    [HarmonyPatch(typeof(GameAudioManager), nameof(GameAudioManager.SetMaterialSwitch),
        typeof(string), typeof(GameObject))]
    private static class MaterialMirrorPatch
    {
        [HarmonyPostfix]
        private static void Postfix(string tag, GameObject go)
        {
            var ours = FootstepEmitter.Current;
            if (ours != null && FootstepEmitter.IsVanillaEmitter(go))
            {
                GameAudioManager.SetMaterialSwitch(tag, ours);
            }
        }
    }

    [HarmonyPatch(typeof(GameAudioManager), nameof(GameAudioManager.SetCramponsSwitch),
        typeof(CramponsState), typeof(GameObject))]
    private static class CramponsMirrorPatch
    {
        [HarmonyPostfix]
        private static void Postfix(CramponsState cramponsState, GameObject go)
        {
            var ours = FootstepEmitter.Current;
            if (ours != null && FootstepEmitter.IsVanillaEmitter(go))
            {
                GameAudioManager.SetCramponsSwitch(cramponsState, ours);
            }
        }
    }

    [HarmonyPatch(typeof(GameAudioManager), nameof(GameAudioManager.SetIndoorEnvironmentSwitch), typeof(GameObject))]
    private static class IndoorMirrorPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameObject go)
        {
            var ours = FootstepEmitter.Current;
            if (ours != null && FootstepEmitter.IsVanillaEmitter(go))
            {
                GameAudioManager.SetIndoorEnvironmentSwitch(ours);
            }
        }
    }

    [HarmonyPatch(typeof(GameAudioManager), nameof(GameAudioManager.SetOutdoorEnvironmentSwitch), typeof(GameObject))]
    private static class OutdoorMirrorPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameObject go)
        {
            var ours = FootstepEmitter.Current;
            if (ours != null && FootstepEmitter.IsVanillaEmitter(go))
            {
                GameAudioManager.SetOutdoorEnvironmentSwitch(ours);
            }
        }
    }

    [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.ProcessFootstepAudioEvent),
        typeof(GameObject), typeof(Vector3))]
    private static class HearingScalePatch
    {
        private readonly struct Saved
        {
            public Saved(float normal, float feeding, float sleeping)
            {
                Normal = normal;
                Feeding = feeding;
                Sleeping = sleeping;
            }

            public readonly float Normal;
            public readonly float Feeding;
            public readonly float Sleeping;
        }

        [HarmonyPrefix]
        private static void Prefix(BaseAi __instance, GameObject sender, out Saved? __state)
        {
            __state = null;
            if (Acoustic.HearingIsVanilla || sender == null || sender != GameManager.GetPlayerObject())
            {
                return;
            }

            float factor = Acoustic.HearingFactor;
            __state = new Saved(
                __instance.m_HearFootstepsRange,
                __instance.m_HearFootstepsRangeWhileFeeding,
                __instance.m_HearFootstepsRangeWhileSleeping);
            __instance.m_HearFootstepsRange = __state.Value.Normal * factor;
            __instance.m_HearFootstepsRangeWhileFeeding = __state.Value.Feeding * factor;
            __instance.m_HearFootstepsRangeWhileSleeping = __state.Value.Sleeping * factor;

            if (Settings.DebugLogging && scaledHearings++ % 50 == 0)
            {
                Core.Logger.Msg($"hearing scaled x{factor:F2} on {__instance.name} (count {scaledHearings})");
            }
        }

        [HarmonyPostfix]
        private static void Postfix(BaseAi __instance, Saved? __state) => Restore(__instance, __state);

        [HarmonyFinalizer]
        private static void Finalizer(BaseAi __instance, Saved? __state) => Restore(__instance, __state);

        private static void Restore(BaseAi instance, Saved? saved)
        {
            if (saved is not { } s)
            {
                return;
            }

            instance.m_HearFootstepsRange = s.Normal;
            instance.m_HearFootstepsRangeWhileFeeding = s.Feeding;
            instance.m_HearFootstepsRangeWhileSleeping = s.Sleeping;
        }
    }
}
