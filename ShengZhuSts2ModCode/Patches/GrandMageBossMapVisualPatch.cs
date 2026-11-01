using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>地图显示补丁只建立按钮和连接，不修改联机游戏状态。</summary>
public static class GrandMageBossMapVisualPatch
{
    /// <summary>初始化老爹专用地图画像。</summary>
    [HarmonyPatch(typeof(NBossMapPoint), nameof(NBossMapPoint._Ready))]
    public static class BossReadyPatch
    {
        /// <summary>其他 Boss 完整使用原版初始化。</summary>
        public static bool Prefix(NBossMapPoint __instance) => !GrandMageBossMapVisuals.PrepareBossIcon(__instance);
    }

    /// <summary>基础地图完成后补第三个原生 Boss 按钮。</summary>
    [HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.SetMap))]
    public static class SetMapPatch
    {
        /// <summary>只有三 Boss 模式增加按钮。</summary>
        public static void Postfix(NMapScreen __instance, ActMap map) => GrandMageBossMapVisuals.CompleteMap(__instance, map);
    }

    /// <summary>绘制第一条连线前补齐第三按钮，使读档投票和可达性查询不会访问缺失节点。</summary>
    [HarmonyPatch(typeof(NMapScreen), "DrawPaths")]
    public static class DrawPathsPatch
    {
        /// <summary>只创建一次，普通地图不受影响。</summary>
        public static void Prefix(NMapScreen __instance) => GrandMageBossMapVisuals.PrepareMap(__instance);
    }

    /// <summary>第三按钮创建前暂缓坐标字典查询。</summary>
    [HarmonyPatch(typeof(NMapScreen), "RecalculateTravelability")]
    public static class TravelPatch
    {
        /// <summary>按钮齐全后执行原生可达性规则。</summary>
        public static bool Prefix(NMapScreen __instance) => GrandMageBossMapVisuals.CanRefreshTravel(__instance);
    }
}
