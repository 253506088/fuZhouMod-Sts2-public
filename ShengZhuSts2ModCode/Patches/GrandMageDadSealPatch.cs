using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>仅过滤当前老爹战斗中已封存遗物的战斗钩子，保留原枚举顺序。</summary>
[HarmonyPatch(typeof(CombatState), nameof(CombatState.IterateHookListeners))]
public static class GrandMageDadSealPatch
{
    /// <summary>非老爹战斗保持原枚举，避免影响其他模块。</summary>
    public static void Postfix(CombatState __instance, ref IEnumerable<AbstractModel> __result)
    {
        if (__instance.Encounter is GrandMageDadEncounter encounter && encounter.State.SealedTalismans.Count > 0)
            __result = __result.Where(model => model is not RelicModel relic || !GrandMageDadSealService.IsSealed(relic));
    }
}

/// <summary>阻止自动挡直接激发被封存的符咒，仍允许取消激发。</summary>
[HarmonyPatch]
public static class GrandMageDadSealedActivationPatch
{
    /// <summary>只选择本 Mod 已有的动物符咒激发入口，不拦截其他 Mod。</summary>
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (Type type in TalismanHelper.AnimalTalismanTypes)
        {
            MethodInfo? method = AccessTools.Method(type, "SetActivated", [typeof(bool)]);
            if (method != null) yield return method;
        }
    }

    /// <summary>取消激发可继续执行，激发必须通过封存状态检查。</summary>
    public static bool Prefix(RelicModel __instance, bool __0) => !__0 || !GrandMageDadSealService.IsSealed(__instance);
}

/// <summary>封存期间只把遗物图标显示为禁用，解封后自动恢复原来的激发或冷却显示。</summary>
[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.Status), MethodType.Getter)]
public static class GrandMageDadSealedStatusPatch
{
    /// <summary>不改写原始状态，避免解封时丢失已有的冷却与激发信息。</summary>
    public static void Postfix(RelicModel __instance, ref RelicStatus __result)
    {
        if (GrandMageDadSealService.IsSealed(__instance)) __result = RelicStatus.Disabled;
    }
}
