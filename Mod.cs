using Harmony;
using ICities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using static TransferManager;

namespace NoIndustriesImport
{
    public class Mod : IUserMod
    {
        public static bool enabled = false;
        public static Dictionary<TransferReason, bool> ImportConfig = new Dictionary<TransferReason, bool>(4);
        public static Dictionary<TransferReason, bool> ExportConfig = new Dictionary<TransferReason, bool>(4);
        public string Name => "No Industries Import";
        public string Description => "Industries DLC buildings no longer import or export raw materials with outside of the map.";
        readonly string SettingsFile = "NoIndustriesImportConfig.txt";
        readonly string ImportPrefix = "Import";
        readonly string ExportPrefix = "Export";
        public static readonly TransferReason[] resources = {
            TransferReason.Ore,
            TransferReason.Oil,
            TransferReason.Logs,
            TransferReason.Grain
        };
        public void OnEnabled()
        {
            LoadSettings();
            enabled = true;
        }

        public void OnDisabled()
        {
            enabled = false;
        }

        public void OnSettingsUI(UIHelperBase helper)
        {
            var group1 = helper.AddGroup("Disable importing of raw materials");
            foreach (var reason in resources)
                group1.AddCheckbox(string.Format("Disable importing of {0}", reason.ToString()), ImportConfig[reason], isChecked => { ImportConfig[reason] = isChecked; SaveSettings(); });
            var group2 = helper.AddGroup("Disable exporting of raw materials");
            foreach (var reason in resources)
                group2.AddCheckbox(string.Format("Disable exporting of {0}", reason.ToString()), ExportConfig[reason], isChecked => { ExportConfig[reason] = isChecked; SaveSettings(); });
            helper.AddGroup("Turning off any option after a map load requires a restart of the game!");
        }

        public void LoadSettings()
        {
            if (File.Exists(SettingsFile))
            {
                foreach (var config in File.ReadAllLines(SettingsFile)
                    .Where(l => l.Length > 0 && l[0] != '#')
                    .Select(l => l.Split('=')))
                {
                    var io_config = ImportConfig;
                    if (config.Length != 2)
                        continue;
                    if(config[0].Contains("::"))
                    {
                        var parts = config[0].Split(new string[] { "::" }, StringSplitOptions.None);
                        if (parts[0] == ExportPrefix)
                            io_config = ExportConfig;
                        config[0] = parts[1];
                    }
                    if (!Enum.IsDefined(typeof(TransferReason), config[0].Trim()))
                        continue;
                    var key = (TransferReason)Enum.Parse(typeof(TransferReason), config[0].Trim());
                    if (bool.TryParse(config[1].Trim(), out bool value))
                        io_config[key] = value;
                }
            }
            foreach (var reason in resources)
            {
                if (!ImportConfig.ContainsKey(reason))
                    ImportConfig[reason] = false;
                if (!ExportConfig.ContainsKey(reason))
                    ExportConfig[reason] = false;
            }
            SaveSettings();
        }

        public void SaveSettings()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# False = allow importing / exporting");
            sb.AppendLine("# True = disallow importing / exporting");
            foreach (var reason in resources)
            {
                sb.AppendLine(string.Format("{0}::{1} = {2}", ImportPrefix, reason, ImportConfig[reason].ToString()));
                sb.AppendLine(string.Format("{0}::{1} = {2}", ExportPrefix, reason, ExportConfig[reason].ToString()));
            }
            File.WriteAllText(SettingsFile, sb.ToString());
        }
    }
    public class NII_Loading : LoadingExtensionBase
    {
        public override void OnLevelLoaded(LoadMode mode)
        {
            if (!(mode == LoadMode.LoadGame || mode == LoadMode.LoadScenario || mode == LoadMode.NewGame || mode == LoadMode.NewGameFromScenario))
                return;
            if (!Mod.enabled)
                return;

            var harmony = HarmonyInstance.Create("me.msg7086.nii");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
    [HarmonyPatch(typeof(OutsideConnectionAI), "AddConnectionOffers")]
    public class NII_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo import_method = typeof(TransferManager).GetMethod("AddOutgoingOffer");
            MethodInfo export_method = typeof(TransferManager).GetMethod("AddIncomingOffer");
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count - 3; i++)
            {
                if (codes[i].opcode != OpCodes.Ldloc_1)
                    continue;
                if (codes[i + 1].opcode != OpCodes.Ldc_I4_S)
                    continue;
                if (codes[i + 2].opcode != OpCodes.Ldloc_S)
                    continue;
                if (codes[i + 3].opcode != OpCodes.Callvirt)
                    continue;
                var operand = ((TransferReason)(sbyte)codes[i + 1].operand);
                if (!Mod.resources.Contains(operand))
                    continue;
                if ((codes[i + 3].operand == import_method && Mod.ImportConfig[operand])
                    || (codes[i + 3].operand == export_method && Mod.ExportConfig[operand]))
                    OverwriteCode(codes, i);
            }
            return codes.AsEnumerable();
        }

        static void OverwriteCode(List<CodeInstruction> Codes, int Index)
        {
            Codes[Index].opcode = OpCodes.Nop;
            Codes[Index + 1].opcode = OpCodes.Nop;
            Codes[Index + 2].opcode = OpCodes.Nop;
            Codes[Index + 3].opcode = OpCodes.Nop;
        }
    }
}
