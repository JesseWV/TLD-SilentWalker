namespace SilentWalker;

internal static class Acoustic
{

    internal static bool Enabled => Settings.Instance.enabled;

    internal static bool Linked => Settings.Instance.mode != Settings.ModeSeparate;

    private static int HearingPercent => Linked ? Settings.Instance.movementNoisePercent : Settings.Instance.hearingPercent;

    private static int AudioPercent => Linked ? Settings.Instance.movementNoisePercent : Settings.Instance.footstepVolumePercent;

    internal static float HearingFactor => !Enabled ? 1f : Mathf.Clamp01(HearingPercent / 100f) * (Linked ? LayerHearingFactor : 1f);

    internal static bool HearingIsVanilla => HearingFactor >= 1f;

    internal static float AudioFactor => Mathf.Clamp01(AudioPercent / 100f);

    internal static bool AudioIsVanilla => !Enabled || (AudioPercent >= 100 && !LayersActive);

    internal static bool AudioIsSilent => Enabled && AudioPercent <= 0;

    internal const int General = 0, Wood = 1, Metal = 2, Water = 3;

    private static readonly float[] CarriedKg = new float[4];

    internal static void SetCarried(int material, float kg) => CarriedKg[material] = kg;

    internal static bool LayersActive
    {
        get
        {
            if (!FootstepBankStore.Loaded)
            {
                return false;
            }

            for (int m = 0; m < 4; m++)
            {
                if (PackNoisePercent(m) < 100)
                {
                    return true;
                }
            }

            return GroundAmplitude < 1f || ClothingAmplitude < 1f || PackMuffle > 0f || MetalWrap > 0f;
        }
    }

    internal static float PackMuffle => Settings.Instance.showDetailed ? 100 - Mathf.Clamp(Settings.Instance.packTreblePercent, 0, 100) : 0f;

    internal static float MetalWrap => Settings.Instance.showDetailed ? 100 - Mathf.Clamp(Settings.Instance.metalRingPercent, 0, 100) : 0f;

    internal static int PackNoisePercent(int material) => material switch
    {
        General => Settings.Instance.packGeneralNoisePercent,
        Wood => Settings.Instance.packWoodNoisePercent,
        Metal => Settings.Instance.packMetalNoisePercent,
        _ => Settings.Instance.packWaterNoisePercent,
    };

    internal static float PackAmplitude(int material)
    {
        int n = PackNoisePercent(material);
        if (n >= 100)
        {
            return 1f;
        }

        return n <= 0 ? 0f : Mathf.Pow(10f, -0.3f * (100 - n) / 20f);
    }

    internal static float GroundAmplitude => Settings.Instance.showDetailed ? Mathf.Clamp01(Settings.Instance.groundVolumePercent / 100f) : 1f;

    internal static float ClothingAmplitude => Settings.Instance.showDetailed ? Mathf.Clamp01(Settings.Instance.clothingVolumePercent / 100f) : 1f;

    private const float GroundDb = -41.5f, ClothingDb = -39.9f;
    private static readonly float[] TableKg = { 1f, 3f, 10f, 30f };
    private static readonly float[,] MaterialDb =
    {
        { -45.9f, -41.7f, -45.5f, -32.1f },
        { -48.0f, -47.5f, -35.2f, -33.8f },
        { -48.0f, -46.9f, -47.1f, -34.2f },
        { -48.0f, -44.8f, -38.5f, -38.9f },
    };

    internal static float MaterialEnergy(int material, float kg)
    {
        if (kg <= 0f)
        {
            return 0f;
        }

        if (kg < TableKg[0])
        {
            return kg / TableKg[0] * Energy(MaterialDb[material, 0]);
        }

        for (int i = 1; i < TableKg.Length; i++)
        {
            if (kg <= TableKg[i])
            {
                float t = (kg - TableKg[i - 1]) / (TableKg[i] - TableKg[i - 1]);
                return Energy(Mathf.Lerp(MaterialDb[material, i - 1], MaterialDb[material, i], t));
            }
        }

        return Energy(MaterialDb[material, TableKg.Length - 1]);
    }

    private static float Energy(float db) => Mathf.Pow(10f, db / 10f);

    internal static float LayerHearingFactor
    {
        get
        {
            if (!LayersActive)
            {
                return 1f;
            }

            float g = Energy(GroundDb), c = Energy(ClothingDb);
            float ga = GroundAmplitude, ca = ClothingAmplitude;
            float before = g + c, after = g * ga * ga + c * ca * ca;
            for (int m = 0; m < 4; m++)
            {
                float e = MaterialEnergy(m, CarriedKg[m]);
                float a = PackAmplitude(m);
                before += e;
                after += e * a * a;
            }

            return before > 0f ? Mathf.Sqrt(after / before) : 1f;
        }
    }
}
