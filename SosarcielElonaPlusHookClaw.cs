using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

[BepInPlugin("sosarciel.elonaplushookclaw", "SosarcielElonaPlusClaw", "1.0.0.0")]
[BepInProcess("Elin.exe")]
public class SosarcielElonaPlusHookClaw : BaseUnityPlugin {
    public static new ManualLogSource Logger;

    void Awake() {
        Logger = base.Logger;
        var harmony = new Harmony("SosarcielElonaPlusClaw");
        harmony.PatchAll();
        SosarcielElonaPlusHookClaw.Logger.LogInfo("Awake");
    }

    public void OnStartCore() {
        SosarcielElonaPlusHookClaw.Logger.LogInfo("OnStartCore");
        var dir = Path.GetDirectoryName(Info.Location);
        var excel = dir + "/Item/SosarcielThing.xlsx";
        var sources = Core.Instance.sources;
        ModUtil.ImportExcel(excel, "Thing", sources.things);
    }

    void Start(){
        SosarcielElonaPlusHookClaw.Logger.LogInfo("Start");
        SosarcielElonaPlusHookClaw.Logger.LogInfo("Try trans to CN");
        if(Lang.langCode == Lang.LangCode.CN.ToString()){
            var sources = Core.Instance.sources.things;
            var row = sources.GetRow("sosarciel_hook_claw");
            row.name = "钩爪";
            row.unit = "把";
            row.detail = "模仿野兽、猛禽、毒虫爪子的装备。本用于爬树，不过因为其攻击可弹开防具，提升格斗威力，所以就用于战斗了。";
        }
    }
}

[HarmonyPatch(typeof(Thing))]
[HarmonyPatch(nameof(Thing.AddAttackEvaluation))]
[HarmonyPatch(new [] { typeof(UINote), typeof(Chara), typeof(Thing) })]
class Thing_AddAttackEvaluation_Patch {
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions){
        //var codes = new List<CodeInstruction>(instructions);
        //foreach (var code in codes)
        //    Debug.Log(code.ToString());
        SosarcielElonaPlusHookClaw.Logger.LogInfo("SosarcielElonaPlusHookClaw Thing_AddAttackEvaluation_Patch");

        var matcher = new CodeMatcher(instructions,null);
        matcher.MatchForward(false, new CodeMatch[]{
            //new (OpCodes.Ldloc, 3),
            new (OpCodes.Ldfld, AccessTools.Field(typeof(BodySlot), "thing")),
            new (OpCodes.Ldfld, AccessTools.Field(typeof(Thing), "source")),
            new (OpCodes.Ldfld, AccessTools.Field(typeof(SourceThing.Row), "offense")),
            new (OpCodes.Ldlen),
            new (OpCodes.Conv_I4),
            new (OpCodes.Ldc_I4_2),
        });

        matcher.MatchForward(false, new CodeMatch(OpCodes.Blt, null));

        var label = matcher.Operand;

        matcher.Advance(1).InsertAndAdvance(new CodeInstruction[]{
            new (OpCodes.Ldloc_3),
            new (OpCodes.Ldfld, AccessTools.Field(typeof(BodySlot), "thing")),
            new (OpCodes.Ldfld, AccessTools.Field(typeof(Thing), "source")),
            new (OpCodes.Ldfld, AccessTools.Field(typeof(SourceThing.Row), "category")),
            new (OpCodes.Ldstr, "shield"),
            new (OpCodes.Call, AccessTools.Method(typeof(string), "op_Equality")),
            new (OpCodes.Brtrue, label)
        });

        return matcher.InstructionEnumeration();
    }
}

[HarmonyPatch(typeof(CharaBody))]
[HarmonyPatch(nameof(CharaBody.GetAttackIndex))]
[HarmonyPatch(new [] { typeof(Thing) })]
class CharaBody_GetAttackIndex_Patch {
    public static bool Prefix(CharaBody __instance, Thing t, ref int __result) {
        int num = 0;
        foreach (BodySlot bodySlot in __instance.slots) {
            if (bodySlot.thing != null && bodySlot.elementId == 35 && bodySlot.thing.source.offense.Length >= 2 && bodySlot.thing.source.category != "shield") {
                if (bodySlot.thing == t) {
                    __result = num;
                    return false; // Skip the original method
                }
                num++;
            }
        }
        __result = -1;
        return false; // Skip the original method
    }
}

[HarmonyPatch(typeof(AttackProcess))]
[HarmonyPatch(nameof(AttackProcess.Prepare))]
[HarmonyPatch(new [] { typeof(Chara),typeof(Thing),typeof(Card),typeof(Point),typeof(int),typeof(bool) })]
public static class AttackProcess_Prepare_Patch {
    public static bool Prefix(AttackProcess __instance, Chara _CC, Thing _weapon, Card _TC, Point _TP, int _attackIndex, bool _isThrow) {
        if(_weapon!=null && _weapon.source.category=="shield"){
            Msg.Say("ElonaPlusHookClaw.AttackProcess_Prepare_Patch.Warning: Try shield attack");
            __instance.weapon = _weapon;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(AttackProcess))]
[HarmonyPatch(nameof(AttackProcess.Perform))]
[HarmonyPatch(new [] { typeof(int),typeof(bool),typeof(float), typeof(bool), typeof(bool) })]
public static class AttackProcess_Perform_Patch {
    public static bool Prefix(AttackProcess __instance, int count, bool hasHit, float dmgMulti, bool maxRoll, bool subAttack, ref bool __result) {
        if(__instance.weapon!=null && __instance.weapon.source.category=="shield"){
            Msg.Say("ElonaPlusHookClaw.AttackProcess_Perform_Patch.Warning: Try shield attack");
            __result = false;
            return false;
        }
        return true;
    }
}


[HarmonyPatch(typeof(ActMelee))]
[HarmonyPatch(nameof(ActMelee.Attack))]
[HarmonyPatch(new [] { typeof(float),typeof(bool) })]
public static class ActMelee_Attack_Patch{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions){
        var codes = new List<CodeInstruction>(instructions);
        //foreach (var code in codes)
        //    SosarcielElonaPlusHookClaw.Logger.LogInfo(code.ToString());

        SosarcielElonaPlusHookClaw.Logger.LogInfo("SosarcielElonaPlusHookClaw ActMelee_Attack_Patch");
        //Debug.Log("ElonaPlusHookClaw.ActMelee_Attack_Patch Transpiler 1");
        try{
        var matcher = new CodeMatcher(instructions,null);
        matcher.MatchForward(false, new CodeMatch[]{
            //new (OpCodes.Ldloc_S, 4),
            new (OpCodes.Ldfld, AccessTools.Field(typeof(BodySlot), "thing")),
            new (OpCodes.Ldfld, AccessTools.Field(typeof(Thing), "source")),
            new (OpCodes.Ldfld, AccessTools.Field(typeof(SourceThing.Row), "offense")),
            new (OpCodes.Ldlen),
            new (OpCodes.Conv_I4),
            new (OpCodes.Ldc_I4_2),
        });

        //Debug.Log("ElonaPlusHookClaw.ActMelee_Attack_Patch Transpiler 2");

        matcher.MatchForward(false, new CodeMatch(OpCodes.Blt, null));

        //Debug.Log("Operand");
        var label = matcher.Operand;

        int operand = 0;
        for(var i=0;i<20;i++){
            //SosarcielElonaPlusHookClaw.Logger.LogInfo(codes[matcher.Pos-i].ToString());
            var ldlocText = codes[matcher.Pos-7].ToString();
            var match = Regex.Match(ldlocText, @"ldloc.+? (\d+)");
            if(match.Success){
                operand = int.Parse(match.Groups[1].Value);
                break;
            }
            if(i==19) throw new Exception("cant match operand");
        }

        SosarcielElonaPlusHookClaw.Logger.LogInfo("Match label: "+label.ToString());
        SosarcielElonaPlusHookClaw.Logger.LogInfo("Match locv: "+operand.ToString());
        //Debug.Log(label.ToString());

        //Debug.Log("ElonaPlusHookClaw.ActMelee_Attack_Patch Transpiler 3");

        matcher.Advance(1).InsertAndAdvance(new CodeInstruction[]{
            new (OpCodes.Ldloc_S, operand),
            new (OpCodes.Ldfld, AccessTools.Field(typeof(BodySlot), "thing")),
            new (OpCodes.Ldfld, AccessTools.Field(typeof(Thing), "source")),
            new (OpCodes.Ldfld, AccessTools.Field(typeof(SourceThing.Row), "category")),
            new (OpCodes.Ldstr, "shield"),
            new (OpCodes.Call, AccessTools.Method(typeof(string), "op_Equality")),
            new (OpCodes.Brtrue, label)
        });
        //Debug.Log("ElonaPlusHookClaw.ActMelee_Attack_Patch Transpiler 4");
        return matcher.InstructionEnumeration();
        }catch(Exception e){
            SosarcielElonaPlusHookClaw.Logger.LogError(e);
            return instructions;
        }
    }
}


