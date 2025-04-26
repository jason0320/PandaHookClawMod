using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ReflexCLI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using UnityEngine;
using static ContentPopulation;

[BepInPlugin("panda.hookclaw.mod", "Panda's Hook Claw Mod", "1.0.0.0")]
[BepInProcess("Elin.exe")]
public class Mod_PandaHookClawMod : BaseUnityPlugin {
    public static new ManualLogSource Logger;

    void Awake() {
        Logger = base.Logger;
        var harmony = new Harmony("Panda's Hook Claw Mod");
        harmony.PatchAll();
        Mod_PandaHookClawMod.Logger.LogInfo("Awake");
    }

    public void OnStartCore() {
        Mod_PandaHookClawMod.Logger.LogInfo("OnStartCore");
        var dir = Path.GetDirectoryName(Info.Location);
        var excel = dir + "/Item/Thing.xlsx";
        var sources = Core.Instance.sources;
        ModUtil.ImportExcel(excel, "Thing", sources.things);
    }

    void Start(){
        Mod_PandaHookClawMod.Logger.LogInfo("Start");
        Mod_PandaHookClawMod.Logger.LogInfo("Try trans to CN");
        if(Lang.langCode == Lang.LangCode.CN.ToString()){
            var sources = Core.Instance.sources.things;
            var row = sources.GetRow("hp_hook_claw");
            row.name = "钩爪";
            row.unit = "把";
            row.detail = "模仿野兽、猛禽、毒虫爪子的装备。本用于爬树，不过因为其攻击可弹开防具，提升格斗威力，所以就用于战斗了。";
        }
    }
}

[HarmonyPatch(typeof(Thing))]
[HarmonyPatch(nameof(Thing.AddAttackEvaluation))]
class Thing_AddAttackEvaluation_Patch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var matcher = new CodeMatcher(instructions);

        // Find the BLT (Branch if Less Than) instruction to clone its label
        matcher.MatchForward(false, new CodeMatch(OpCodes.Blt));
        if (!matcher.IsValid)
        {
            return instructions; // Skip patching if pattern not found
        }

        // Clone the label from the BLT instruction
        var skipLabel = matcher.Clone().Operand;

        // Reset matcher and search for the BodySlot.thing sequence
        matcher.Start()
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(BodySlot), "thing")),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Thing), "source")),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(SourceThing.Row), "offense")),
                new CodeMatch(OpCodes.Ldlen),
                new CodeMatch(OpCodes.Conv_I4),
                new CodeMatch(OpCodes.Ldc_I4_0)
            );

        if (!matcher.IsValid)
        {
            return instructions;
        }

        // Insert null check for BodySlot.thing using the cloned label
        matcher.Advance(5) // Move to the BLT instruction
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldloc_3),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(BodySlot), "thing")),
                new CodeInstruction(OpCodes.Brfalse, skipLabel) // Use cloned label
            );

        return matcher.InstructionEnumeration();
    }
}

[HarmonyPatch(typeof(CharaBody))]
[HarmonyPatch(nameof(CharaBody.GetAttackIndex))]
[HarmonyPatch(new[] { typeof(Thing) })]
class CharaBody_GetAttackIndex_Patch
{
    private static bool Prefix(CharaBody __instance, Thing t, ref int __result)
    {
        int num = 0;
        foreach (BodySlot bodySlot in __instance.slots)
        {
            // Allow all weapons except shields
            if (bodySlot.thing != null && bodySlot.thing.source.category != "shield")
            {
                if (bodySlot.thing == t)
                {
                    __result = num;
                    return false; // Skip original method
                }
                num++;
            }
        }
        // If no slots match, run the original method (e.g., for barehanded)
        return true; // <-- Critical fix: Allow original logic
    }
}

[HarmonyPatch(typeof(AttackProcess))]
[HarmonyPatch(nameof(AttackProcess.Prepare))]
[HarmonyPatch(new[] { typeof(Chara), typeof(Thing), typeof(Card), typeof(Point), typeof(int), typeof(bool) })]
public static class AttackProcess_Prepare_Patch
{
    public static bool Prefix(AttackProcess __instance, Chara _CC, Thing _weapon, Card _TC, Point _TP, int _attackIndex, bool _isThrow)
    {
        __instance.CC = _CC;
        __instance.TC = _TC;
        __instance.TP = _TP;
        __instance.isThrow = _isThrow;
        __instance.weapon = _weapon;
        __instance.ammo = _weapon?.ammoData;
        __instance.hit = (__instance.crit = (__instance.critFury = (__instance.evadePlus = false)));
        __instance.toolRange = __instance.weapon?.trait as TraitToolRange;
        __instance.attackType = AttackType.Slash;
        __instance.attackStyle = AttackStyle.Default;
        __instance.evasion = 0;
        __instance.penetration = 0;
        __instance.distMod = 100;
        __instance.attackIndex = _attackIndex;
        __instance.posRangedAnime = __instance.TP;
        __instance.ignoreAnime = (__instance.ignoreAttackSound = false);

        if (_weapon != null && _weapon.source.category == "shield")
        {
            __instance.weapon = _weapon;
            __instance.weaponSkill = __instance.CC.elements.GetOrCreateElement(100);
            __instance.toHit = __instance.toHit * 100 / (115 + __instance.attackIndex * 15 + __instance.attackIndex * Mathf.Clamp(2000 / (20 + __instance.CC.EvalueMax(131, -10)), 0, 100));
            bool flag2 = __instance.weapon != null && __instance.weapon.Evalue(482) > 0;
            if (flag2)
            {
                __instance.weaponSkill = __instance.CC.elements.GetOrCreateElement(305);
            }
            __instance.attackType = ((!__instance.CC.race.meleeStyle.IsEmpty()) ? __instance.CC.race.meleeStyle.ToEnum<AttackType>() : ((EClass.rnd(2) == 0) ? AttackType.Kick : AttackType.Punch));
            __instance.dBonus = __instance.CC.DMG + __instance.CC.encLV + (int)Mathf.Sqrt(Mathf.Max(0, __instance.weaponSkill.GetParent(__instance.CC).Value / 5 + __instance.weaponSkill.Value / 4));
            __instance.dNum = 2 + Mathf.Min(__instance.weaponSkill.Value / 10, 4);
            __instance.dDim = 5 + (int)Mathf.Sqrt(Mathf.Max(0, __instance.weaponSkill.Value / 3));
            __instance.dMulti = 0.6f + (float)(__instance.weaponSkill.GetParent(__instance.CC).Value / 2 + __instance.weaponSkill.Value / 2 + __instance.CC.Evalue(flag2 ? 304 : 132) / 2) / 50f;
            __instance.dMulti += 0.05f * (float)__instance.CC.Evalue(1400);
            __instance.toHitBase = EClass.curve(__instance.CC.DEX / 3 + __instance.weaponSkill.GetParent(__instance.CC).Value / 3 + __instance.weaponSkill.Value, 50, 25) + 50;
            __instance.toHitFix = __instance.CC.HIT;
            if (__instance.attackStyle == AttackStyle.Shield)
            {
                __instance.toHitBase = __instance.toHitBase * 75 / 100;
            }
            __instance.penetration = Mathf.Clamp(__instance.weaponSkill.Value / 10 + 5, 5, 20) + __instance.CC.Evalue(92);
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(ActMelee))]
[HarmonyPatch(nameof(ActMelee.Attack))]
public static class ActMelee_Attack_Patch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
    {
        var codes = new List<CodeInstruction>(instructions);
        var matcher = new CodeMatcher(codes, il);

        // Look for the start of the BodySlot loop (should assign to local var like stloc.s)
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(List<BodySlot>), "GetEnumerator")));

        if (!matcher.IsValid) return codes;

        matcher.MatchForward(false, new CodeMatch(ci => ci.opcode.Name.StartsWith("stloc")));
        if (!matcher.IsValid) return codes;

        var slotLocal = (LocalBuilder)matcher.Operand;

        matcher.MatchForward(false, new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(BodySlot), "thing")));
        if (!matcher.IsValid) return codes;

        matcher.MatchBack(false, new CodeMatch(ci => ci.opcode.Name.StartsWith("ldloc")));
        var labelSkip = il.DefineLabel();

        matcher.Insert(
            new CodeInstruction(OpCodes.Ldloc, slotLocal),
            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(BodySlot), "thing")),
            new CodeInstruction(OpCodes.Brfalse_S, labelSkip)
        );

        matcher.Advance(3);
        matcher.Labels.Add(labelSkip);

        return matcher.InstructionEnumeration();
    }
}