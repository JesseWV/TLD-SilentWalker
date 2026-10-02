using System.Reflection;
using MelonLoader;

[assembly: MelonInfo(typeof(SilentWalker.Core), SilentWalker.Core.DisplayName, "2.0.0", "Lycanthor")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace SilentWalker;

public class Core : MelonMod
{

    internal const string Name = "SilentWalker";

    internal const string DisplayName = "Silent Walker";

    internal static MelonLogger.Instance Logger { get; } = new MelonLogger.Instance(Name);

    public override void OnInitializeMelon()
    {

        Logger.Msg("build " + (Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown"));
        Settings.OnLoad();
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {

        if (Acoustic.Enabled)
        {
            FootstepBankStore.EnsureLoaded();
        }
    }
}
