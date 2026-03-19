using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

[BepInPlugin("panda.hookclaw.mod", "Panda's Hook Claw Mod", "1.0.0.0")]
[BepInProcess("Elin.exe")]
public class Mod_PandaHookClawMod : BaseUnityPlugin
{
    public static new ManualLogSource Logger;

    private void Awake()
    {
        Logger = base.Logger;
        var harmony = new Harmony("panda.hookclaw.mod");
        harmony.PatchAll();
        Logger.LogInfo("Panda's Hook Claw Mod loaded");
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.AddAttackEvaluation))]
    private static class Thing_AddAttackEvaluation_Patch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            HookClawILHelper.TryLowerOffenseRequirement(codes, "Thing.AddAttackEvaluation");
            return codes;
        }
    }

    [HarmonyPatch(typeof(ActMelee), nameof(ActMelee.Attack))]
    private static class ActMelee_Attack_Patch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            HookClawILHelper.TryLowerOffenseRequirement(codes, "ActMelee.Attack");
            return codes;
        }
    }

    private static class HookClawILHelper
    {
        public static bool TryLowerOffenseRequirement(List<CodeInstruction> codes, string methodName)
        {
            var fThing = AccessTools.Field(typeof(BodySlot), nameof(BodySlot.thing));
            var fSource = AccessTools.Field(typeof(Thing), nameof(Thing.source));
            var fOffense = AccessTools.Field(typeof(SourceThing.Row), nameof(SourceThing.Row.offense));

            for (int i = 0; i <= codes.Count - 6; i++)
            {
                if (!IsLdfld(codes[i], fThing)) continue;
                if (!IsLdfld(codes[i + 1], fSource)) continue;
                if (!IsLdfld(codes[i + 2], fOffense)) continue;
                if (codes[i + 3].opcode != OpCodes.Ldlen) continue;
                if (codes[i + 4].opcode != OpCodes.Conv_I4) continue;
                if (!IsConstTwo(codes[i + 5])) continue;

                codes[i + 5] = new CodeInstruction(OpCodes.Ldc_I4_1);
                return true;
            }

            return false;
        }

        private static bool IsLdfld(CodeInstruction ci, System.Reflection.FieldInfo field)
        {
            return ci.opcode == OpCodes.Ldfld && Equals(ci.operand, field);
        }

        private static bool IsConstTwo(CodeInstruction ci)
        {
            if (ci.opcode == OpCodes.Ldc_I4_2)
                return true;

            if (ci.opcode == OpCodes.Ldc_I4_S && ci.operand is sbyte sb && sb == 2)
                return true;

            if (ci.opcode == OpCodes.Ldc_I4 && ci.operand is int i && i == 2)
                return true;

            return false;
        }
    }
}