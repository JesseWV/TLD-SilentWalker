using ModSettings;
using System.Reflection;

namespace SilentWalker
{
    public class Settings : JsonModSettings
    {
        internal static readonly bool DebugLogging = false;

        internal const int ModeLinked = 0;
        internal const int ModeSeparate = 1;

        internal static Settings Instance { get; } = new();

        internal static void OnLoad()
        {
            MigrateFromV1();
            Instance.AddToModSettings(Core.DisplayName);
            Instance.ApplyVisibility();
        }

        private static void MigrateFromV1()
        {
            try
            {
                string path = Path.Combine(MelonLoader.Utils.MelonEnvironment.ModsDirectory, Core.Name + ".json");
                if (!File.Exists(path))
                {
                    return;
                }

                string json = File.ReadAllText(path);
                string? summary = ApplyV1(json);
                if (summary != null)
                {
                    Instance.Save();
                    Core.Logger.Msg($"settings from v1.0.0 translated: {summary}");
                }
            }
            catch (Exception e)
            {
                Core.Logger.Warning($"could not translate v1.0.0 settings, using defaults: {e.Message}");
            }
        }

        internal static string? ApplyV1(string json)
        {
            var silence = System.Text.RegularExpressions.Regex.Match(json, "\"silenceFootSteps\"\\s*:\\s*(true|false)");
            int? V1(string material)
            {
                var m = System.Text.RegularExpressions.Regex.Match(json, $"\"inventoryWeight{material}Volume\"\\s*:\\s*(\\d+)");
                return m.Success ? int.Parse(m.Groups[1].Value) : null;
            }

            int? general = V1("General"), wood = V1("Wood"), metal = V1("Metal"), water = V1("Water");
            if (!silence.Success && general == null && wood == null && metal == null && water == null)
            {
                return null;
            }

            if (silence.Success && silence.Groups[1].Value == "true")
            {

                Instance.mode = ModeLinked;
                Instance.movementNoisePercent = 0;
                return "Silence Foot Steps on -> Linked, Movement noise 0%";
            }

            Instance.packGeneralNoisePercent = NoiseFor(general);
            Instance.packWoodNoisePercent = NoiseFor(wood);
            Instance.packMetalNoisePercent = NoiseFor(metal);
            Instance.packWaterNoisePercent = NoiseFor(water);
            bool any = Instance.packGeneralNoisePercent < 100 || Instance.packWoodNoisePercent < 100 || Instance.packMetalNoisePercent < 100 || Instance.packWaterNoisePercent < 100;
            if (any)
            {
                Instance.mode = ModeSeparate;
            }

            return any
                ? $"Separate mode, pack noise Small supplies {Instance.packGeneralNoisePercent}%, Wood {Instance.packWoodNoisePercent}%, Metal {Instance.packMetalNoisePercent}%, Water {Instance.packWaterNoisePercent}%"
                : "all v1 settings were vanilla, nothing to change";
        }

        internal static int NoiseFor(int? v1Percent)
        {
            if (v1Percent is not int p || p >= 100)
            {
                return 100;
            }

            if (p <= 0)
            {
                return 0;
            }

            double db = -20.0 * Math.Log10(p / 100.0);
            return (int)Math.Max(0, 100 - Math.Round(db / 0.3));
        }

        protected override void OnChange(FieldInfo field, object? oldValue, object? newValue)
        {
            base.OnChange(field, oldValue, newValue);
            ApplyVisibility();
            ApplyEnabled();
        }

        private void ApplyEnabled()
        {
            if (enabled)
            {
                FootstepEmitter.ApplyGain();
            }
            else
            {
                FootstepEmitter.Remove();
            }
        }

        protected override void OnConfirm()
        {
            base.OnConfirm();
            ApplyEnabled();
        }

        private void ApplyVisibility()
        {
            bool on = enabled;
            bool separate = mode == ModeSeparate;
            SetFieldVisible(nameof(mode), on);
            SetFieldVisible(nameof(movementNoisePercent), on && !separate);
            SetFieldVisible(nameof(hearingPercent), on && separate);
            SetFieldVisible(nameof(footstepVolumePercent), on && separate);
            SetFieldVisible(nameof(packGeneralNoisePercent), on);
            SetFieldVisible(nameof(packWoodNoisePercent), on);
            SetFieldVisible(nameof(packMetalNoisePercent), on);
            SetFieldVisible(nameof(packWaterNoisePercent), on);
            SetFieldVisible(nameof(showDetailed), on);
            SetFieldVisible(nameof(groundVolumePercent), on && showDetailed);
            SetFieldVisible(nameof(clothingVolumePercent), on && showDetailed);
            SetFieldVisible(nameof(packTreblePercent), on && showDetailed);
            SetFieldVisible(nameof(metalRingPercent), on && showDetailed);
            RefreshGUI();
        }

        [Section("SilentWalker")]
        [Name("Enabled")]
        [Description("Off: the mod does nothing at all. Footsteps, wildlife hearing and everything else are vanilla until you turn it back on.")]
        public bool enabled = true;

        [Section("Movement noise")]
        [Name("Mode")]
        [Description("Linked: one slider sets both how far wildlife hears you and how loud you hear your own footsteps.\n" +
            "Separate: set each on its own slider.")]
        [Choice("Linked", "Separate")]
        public int mode = ModeLinked;

        [Name("Movement noise")]
        [Description("Changes gameplay AND sound. Wildlife hears your footsteps at this % of its normal distance, and you hear them at this % volume.\n" +
            "100% is vanilla. Crouching and walking still reduce hearing further.")]
        [Slider(0, 100, NumberFormat = "{0:0}%")]
        public int movementNoisePercent = 100;

        [Name("Wildlife hearing (gameplay)")]
        [Description("How far animals can hear your footsteps, as a % of the normal distance. Does not change what you hear.\n" +
            "100% is vanilla. Crouching and walking still reduce it further.")]
        [Slider(0, 100, NumberFormat = "{0:0}%")]
        public int hearingPercent = 100;

        [Name("Footstep volume (audio only)")]
        [Description("How loud you hear your own footsteps. Never changes what animals hear.\n" +
            "100% is vanilla, relative to the game's sound effects volume.")]
        [Slider(0, 100, NumberFormat = "{0:0}%")]
        public int footstepVolumePercent = 100;

        [Section("Pack noise")]
        [Name("Small supplies")]
        [Description("How much noise your small supplies make as you move (matches, medicine, bandages, sewing kit, paper). Lower values model packing them carefully. 100% is vanilla, each 10% lower is 3 dB quieter, and 0% is silent.\n" +
            "Linked: wildlife hearing drops by as much as this gear contributes to your noise. Separate: sound only.")]
        [Slider(0, 100, NumberFormat = "{0:0}%")]
        public int packGeneralNoisePercent = 100;

        [Name("Wood")]
        [Description("How much noise your firewood and sticks make as you move. Lower values model tying them down. 100% is vanilla, each 10% lower is 3 dB quieter, and 0% is silent.\n" +
            "Linked: wildlife hearing drops by as much as the wood contributes to your noise. Separate: sound only.")]
        [Slider(0, 100, NumberFormat = "{0:0}%")]
        public int packWoodNoisePercent = 100;

        [Name("Metal")]
        [Description("How much noise your metal makes as you move (tools, scrap, cans and canned drinks). Lower values model wrapping and separating it. 100% is vanilla, each 10% lower is 3 dB quieter, and 0% is silent.\n" +
            "Linked: wildlife hearing drops by as much as the metal contributes to your noise. Separate: sound only.")]
        [Slider(0, 100, NumberFormat = "{0:0}%")]
        public int packMetalNoisePercent = 100;

        [Name("Water")]
        [Description("How much noise your bottled liquids make as you move (water, lamp fuel, antiseptic). Lower values model wedging them upright. 100% is vanilla, each 10% lower is 3 dB quieter, and 0% is silent.\n" +
            "Linked: wildlife hearing drops by as much as the liquids contribute to your noise. Separate: sound only.")]
        [Slider(0, 100, NumberFormat = "{0:0}%")]
        public int packWaterNoisePercent = 100;

        [Section("Detail")]
        [Name("Show detailed settings")]
        [Description("Shows the ground and clothing sliders. While hidden, they stay at vanilla.")]
        public bool showDetailed = false;

        [Name("Ground impact")]
        [Description("Volume of your boots on the ground, as a % of vanilla.\n" +
            "Linked: wildlife hearing drops by as much as the ground contributes to your noise. Separate: sound only.")]
        [Slider(0, 100, NumberFormat = "{0:0}%")]
        public int groundVolumePercent = 100;

        [Name("Clothing rustle")]
        [Description("Volume of your clothing as you move, as a % of vanilla.\n" +
            "Linked: wildlife hearing drops by as much as your clothing contributes to your noise. Separate: sound only.")]
        [Slider(0, 100, NumberFormat = "{0:0}%")]
        public int clothingVolumePercent = 100;

        [Name("Pack treble (experimental)")]
        [Description("The high frequencies of everything in your pack. 100% is vanilla; lower values muffle the pack, as if it were wrapped in fabric.\n" +
            "Changes tone only, never wildlife hearing. Not yet tuned by ear; feedback welcome.")]
        [Slider(0, 100, NumberFormat = "{0:0}%")]
        public int packTreblePercent = 100;

        [Name("Metal ring (experimental)")]
        [Description("The ring of metal in your pack. 100% is vanilla; lower values take the ring out, as if each piece were wrapped.\n" +
            "Changes tone only, never wildlife hearing. Not yet tuned by ear; feedback welcome.")]
        [Slider(0, 100, NumberFormat = "{0:0}%")]
        public int metalRingPercent = 100;
    }
}
