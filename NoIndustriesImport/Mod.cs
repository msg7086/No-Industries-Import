using Harmony;
using ICities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace NoIndustriesImport
{
    public class Mod : IUserMod
    {
        public string Name => "No Industries Import";
        public string Description => "Industries DLC buildings no longer import raw materials.";
    }
    public class NII_Loading : LoadingExtensionBase
    {
        public override void OnLevelLoaded(LoadMode mode)
        {
            if (!(mode == LoadMode.LoadGame || mode == LoadMode.LoadScenario || mode == LoadMode.NewGame || mode == LoadMode.NewGameFromScenario))
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
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count - 3; i++)
            {
                if (codes[i].opcode != OpCodes.Ldloc_1)
                    continue;
                if (codes[i + 1].opcode != OpCodes.Ldc_I4_S)
                    continue;
                if (codes[i + 2].opcode != OpCodes.Ldloc_S)
                    continue;
                if (codes[i + 3].opcode != OpCodes.Callvirt && codes[i + 3].operand != typeof(TransferManager).GetMethod("AddOutgoingOffer"))
                    continue;
                switch ((TransferManager.TransferReason)(sbyte)codes[i + 1].operand)
                {
                    case TransferManager.TransferReason.Ore:
                    case TransferManager.TransferReason.Oil:
                    case TransferManager.TransferReason.Logs:
                    case TransferManager.TransferReason.Grain:
                        codes[i].opcode = OpCodes.Nop;
                        codes[i + 1].opcode = OpCodes.Nop;
                        codes[i + 2].opcode = OpCodes.Nop;
                        codes[i + 3].opcode = OpCodes.Nop;
                        i += 4;
                        break;
                }
            }
            return codes.AsEnumerable();
        }
    }
}
