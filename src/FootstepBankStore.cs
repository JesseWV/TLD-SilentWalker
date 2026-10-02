using System.Runtime.InteropServices;
using System.Security.Cryptography;
using MelonLoader.Utils;

namespace SilentWalker;

internal static class FootstepBankStore
{
    private const string FileName = FootstepBank.BankName + ".bnk";
    private const string StampName = FootstepBank.BankName + ".stamp";

    private static bool attempted;

    internal static bool Loaded { get; private set; }

    internal static void EnsureLoaded()
    {
        if (attempted || !AkSoundEngine.IsInitialized())
        {
            return;
        }

        attempted = true;
        try
        {
            byte[] bank = GetBank();
            Load(bank);
        }
        catch (Exception e)
        {
            Core.Logger.Warning($"footstep bank unavailable, using the emitter-volume path this session: {e.Message}");
        }
    }

    private static byte[] GetBank()
    {
        string source = FindGameBank();
        byte[] game = File.ReadAllBytes(source);

        string events = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(source)!)!, "_BaseEvents.bnk");
        FootstepBank.VerifyEvents(File.ReadAllBytes(events));

        string builtFrom = $"builder {FootstepBank.BuilderVersion}\nsha256 {Convert.ToHexString(SHA256.HashData(game))}\n";

        string dir = Path.Combine(MelonEnvironment.UserDataDirectory, Core.Name);
        string bankPath = Path.Combine(dir, FileName);
        string stampPath = Path.Combine(dir, StampName);
        if (File.Exists(bankPath) && File.Exists(stampPath))
        {
            byte[] stored = File.ReadAllBytes(bankPath);
            if (File.ReadAllText(stampPath) == builtFrom + $"bank {Convert.ToHexString(SHA256.HashData(stored))}\n")
            {
                Core.Logger.Msg($"footstep bank: using stored {bankPath}");
                return stored;
            }
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        byte[] bank = FootstepBank.Build(game);
        Directory.CreateDirectory(dir);

        File.Delete(stampPath);
        File.WriteAllBytes(bankPath, bank);
        File.WriteAllText(stampPath, builtFrom + $"bank {Convert.ToHexString(SHA256.HashData(bank))}\n");
        Core.Logger.Msg($"footstep bank: built {bank.Length} bytes from {source} in {watch.ElapsedMilliseconds} ms, stored at {bankPath}");
        return bank;
    }

    private static string FindGameBank()
    {
        string root = Path.Combine(Application.streamingAssetsPath, "Audio", "GeneratedSoundBanks", "Windows");
        string english = Path.Combine(root, "English(US)", "_BaseStructures.bnk");
        if (File.Exists(english))
        {
            return english;
        }

        foreach (string dir in Directory.GetDirectories(root))
        {
            string candidate = Path.Combine(dir, "_BaseStructures.bnk");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"no _BaseStructures.bnk in any language folder under {root}");
    }

    private static void Load(byte[] bank)
    {

        IntPtr raw = Marshal.AllocHGlobal(bank.Length + 16);
        try
        {
            var aligned = new IntPtr((raw.ToInt64() + 15) & ~15L);
            Marshal.Copy(bank, 0, aligned, bank.Length);
            var result = AkSoundEngine.LoadBankMemoryCopy(aligned, (uint)bank.Length, out uint bankId);
            if (result != AKRESULT.AK_Success)
            {
                throw new InvalidOperationException($"LoadBankMemoryCopy returned {result}");
            }

            Core.Logger.Msg($"footstep bank: loaded, id {bankId}");
        }
        finally
        {
            Marshal.FreeHGlobal(raw);
        }

        AkSoundEngine.SetRTPCValue(FootstepBank.ParamFootstepVolume, 100f);
        AkSoundEngine.SetRTPCValue(FootstepBank.ParamGroundVolume, 100f);
        AkSoundEngine.SetRTPCValue(FootstepBank.ParamClothingVolume, 100f);
        foreach (string param in FootstepBank.PackVolumeParams)
        {
            AkSoundEngine.SetRTPCValue(param, 100f);
        }

        AkSoundEngine.SetRTPCValue(FootstepBank.ParamPackMuffle, 0f);
        AkSoundEngine.SetRTPCValue(FootstepBank.ParamMetalWrap, 0f);
        Loaded = true;
    }
}
