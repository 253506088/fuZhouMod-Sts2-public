using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>遗物格初始化或计数变化时刷新封印标记。</summary>
[HarmonyPatch]
internal static class GrandMageSealDisplayPatch
{
    /// <summary>只监听原生遗物格的初始化与计数刷新，不另建逐帧轮询。</summary>
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NRelicInventoryHolder), "_Ready");
        yield return AccessTools.Method(typeof(NRelicInventoryHolder), "RefreshAmount");
    }

    /// <summary>显示层只读取已经结算的封印记录。</summary>
    private static void Postfix(NRelicInventoryHolder __instance) => GrandMageSealRelicDisplay.Refresh(__instance);
}

/// <summary>在被封印遗物的悬停说明首位显示封印期限和老爹得到的对应增益。</summary>
[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.HoverTips), MethodType.Getter)]
internal static class GrandMageSealHoverPatch
{
    /// <summary>未封印时保留原说明，封印时追加独立提示而不覆盖遗物说明。</summary>
    private static void Postfix(RelicModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (GrandMageSealInfoService.Remaining(__instance) <= 0) return;
        __result = new IHoverTip[] { new HoverTip(__instance.Title,
            GrandMageSealInfoService.Text("sealHoverTitle") + "\n" + GrandMageSealInfoService.RelicHint(__instance)) }.Concat(__result);
    }
}
