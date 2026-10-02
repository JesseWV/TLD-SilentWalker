using Il2Cpp;
using UnityEngine;

namespace SilentWalker;

internal static class FootstepEmitter
{
    private const string ObjectName = "SilentWalker_FootstepEmitter";

    private static GameObject? anchor;
    private static GameObject? emitter;
    private static GameObject? vanillaEmitter;
    private static int vanillaEmitterId;
    private static int playerObjectId;
    private static float appliedFactor = -1f;
    private static int appliedListenerId;
    private static bool appliedWithBank;
    private static AkAudioListener? listener;

    internal static GameObject? VanillaEmitter
    {
        get
        {
            if (vanillaEmitter != null)
            {
                return vanillaEmitter;
            }

            var player = GameManager.GetPlayerObject();
            if (player == null)
            {
                return null;
            }

            vanillaEmitter = GameAudioManager.GetSoundEmitterFromGameObject(player);
            vanillaEmitterId = vanillaEmitter != null ? vanillaEmitter.GetInstanceID() : 0;
            playerObjectId = player.GetInstanceID();
            return vanillaEmitter;
        }
    }

    internal static bool IsVanillaEmitter(GameObject? go)
    {
        if (go == null)
        {
            return false;
        }

        var vanilla = VanillaEmitter;
        if (vanilla == null)
        {
            return false;
        }

        int id = go.GetInstanceID();
        return id == vanillaEmitterId || id == playerObjectId;
    }

    internal static GameObject? Current => emitter != null ? emitter : null;

    internal static GameObject? Ensure()
    {
        var vanilla = VanillaEmitter;
        if (vanilla == null)
        {
            return null;
        }

        if (emitter != null && anchor != null && anchor.transform.parent == vanilla.transform.parent)
        {
            return emitter;
        }

        if (anchor != null)
        {
            UnityEngine.Object.Destroy(anchor);
        }

        anchor = new GameObject(ObjectName);
        anchor.transform.SetParent(vanilla.transform.parent, false);
        anchor.transform.localPosition = vanilla.transform.localPosition;
        anchor.transform.localRotation = vanilla.transform.localRotation;

        emitter = GameAudioManager.GetSoundEmitterFromGameObject(anchor);
        if (emitter == null)
        {
            UnityEngine.Object.Destroy(anchor);
            anchor = null;
            return null;
        }

        appliedFactor = -1f;
        ReplaySwitches(vanilla);
        ApplyGain();

        if (Settings.DebugLogging)
        {
            Core.Logger.Msg($"footstep emitter created: {emitter.name} layer {emitter.layer} id {emitter.GetInstanceID()}, player emitter {vanilla.name} id {vanillaEmitterId}");
        }

        return emitter;
    }

    private static void ReplaySwitches(GameObject vanilla)
    {
        if (emitter == null)
        {
            return;
        }

        var pm = GameManager.GetPlayerManagerComponent();
        if (pm != null)
        {
            MirrorRegion(pm, ClothingRegion.Feet, "CLOTHINGREGION_FEET");
            MirrorRegion(pm, ClothingRegion.Chest, "CLOTHINGREGION_CHEST");
            MirrorRegion(pm, ClothingRegion.Legs, "CLOTHINGREGION_LEGS");
            GameAudioManager.SetCramponsSwitch(pm.GetCramponsState(), emitter);
        }

        var footsteps = UnityEngine.Object.FindObjectOfType<FootStepSounds>();
        string? tag = footsteps != null ? footsteps.m_MaterialTagLastFootstep : null;
        if (!string.IsNullOrEmpty(tag))
        {
            GameAudioManager.SetMaterialSwitch(tag, emitter);
        }

        var weather = GameManager.GetWeatherComponent();
        if (weather != null)
        {
            if (weather.IsIndoorScene())
            {
                GameAudioManager.SetIndoorEnvironmentSwitch(emitter);
            }
            else
            {
                GameAudioManager.SetOutdoorEnvironmentSwitch(emitter);
            }
        }
    }

    private static void MirrorRegion(PlayerManager pm, ClothingRegion region, string switchGroup)
    {
        uint state = pm.GetLastAudioSwitchForRegion(region);
        if (state == 0)
        {
            return;
        }

        AkSoundEngine.SetSwitch(AkSoundEngine.GetIDFromString(switchGroup), state, emitter);
    }

    internal static void ApplyGain()
    {
        if (emitter == null)
        {
            return;
        }

        if (listener == null)
        {

            listener = UnityEngine.Object.FindObjectOfType<AkAudioListener>();
            if (listener == null)
            {
                return;
            }
        }

        bool bank = FootstepBankStore.Loaded;
        float factor = Acoustic.AudioFactor;
        float output = bank ? 1f : factor;
        int listenerId = listener.gameObject.GetInstanceID();
        if (factor == appliedFactor && listenerId == appliedListenerId && bank == appliedWithBank)
        {
            return;
        }

        var result = AkSoundEngine.SetGameObjectOutputBusVolume(emitter, listener.gameObject, output);
        if (bank)
        {
            AkSoundEngine.SetRTPCValue(FootstepBank.ParamFootstepVolume, factor * 100f);
        }

        appliedFactor = factor;
        appliedListenerId = listenerId;
        appliedWithBank = bank;

        if (Settings.DebugLogging)
        {
            Core.Logger.Msg($"footstep emitter gain {factor:F2} ({(bank ? "bank voice volume" : "output bus volume")}) -> {result}");
        }
    }

    internal static void Remove()
    {
        if (anchor != null)
        {
            UnityEngine.Object.Destroy(anchor);
        }

        anchor = null;
        emitter = null;
        Invalidate();
    }

    internal static void Invalidate()
    {
        vanillaEmitter = null;
        listener = null;
        appliedFactor = -1f;
    }
}
