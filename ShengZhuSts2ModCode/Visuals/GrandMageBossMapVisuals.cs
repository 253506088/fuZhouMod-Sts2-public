using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

/// <summary>复用原生 Boss 按钮展示最后的老爹，点击、悬停、联机投票仍由游戏处理。</summary>
public static class GrandMageBossMapVisuals
{
    private const string Icon = "res://ShengZhuSts2Mod/images/map/grand_mage_dad";

    /// <summary>避免第三个 Boss 误用第一位 Boss 的地图画像。</summary>
    public static bool PrepareBossIcon(NBossMapPoint node)
    {
        if (!GodotObject.IsInstanceValid(node) || !GrandMageBossMapService.IsFinalPoint(RunManager.Instance.DebugOnlyGetState(), node.Point?.coord)) return false;
        AccessTools.Method(typeof(NMapPoint), "ConnectSignals").Invoke(node, null);
        AccessTools.Method(typeof(NBossMapPoint), "Disable").Invoke(node, null);
        Node2D container = node.GetNode<Node2D>("%SpriteContainer");
        Node2D spine = node.GetNode<Node2D>("%SpineSprite");
        TextureRect image = node.GetNode<TextureRect>("%PlaceholderImage");
        TextureRect outline = node.GetNode<TextureRect>("%PlaceholderOutline");
        spine.Visible = false;
        image.Visible = true;
        image.Texture = ResourceLoader.Load<Texture2D>(Icon + ".png");
        outline.Texture = ResourceLoader.Load<Texture2D>(Icon + "_outline.png");
        var act = RunManager.Instance.DebugOnlyGetState()!.Act;
        image.SelfModulate = act.MapTraveledColor;
        outline.SelfModulate = act.MapBgColor;
        AccessTools.Field(typeof(NBossMapPoint), "_spriteContainer").SetValue(node, container);
        AccessTools.Field(typeof(NBossMapPoint), "_spineSprite").SetValue(node, spine);
        AccessTools.Field(typeof(NBossMapPoint), "_usesSpine").SetValue(node, false);
        AccessTools.Field(typeof(NBossMapPoint), "_placeholderImage").SetValue(node, image);
        AccessTools.Field(typeof(NBossMapPoint), "_placeholderOutline").SetValue(node, outline);
        AccessTools.Method(typeof(NBossMapPoint), "RefreshColorInstantly").Invoke(node, null);
        return true;
    }

    /// <summary>原版 SetMap 建立第三按钮之前暂缓依赖完整字典的刷新。</summary>
    public static bool CanRefreshTravel(NMapScreen screen)
    {
        var map = AccessTools.Field(typeof(NMapScreen), "_map").GetValue(screen) as GrandMageFinalActMap;
        return map?.ThirdBossMapPoint == null || Points(screen).ContainsKey(map.ThirdBossMapPoint.coord);
    }

    /// <summary>在原版绘制连线和恢复投票前，把三位 Boss 的按钮及位置准备好。</summary>
    public static void PrepareMap(NMapScreen screen)
    {
        if (!GodotObject.IsInstanceValid(screen)
            || AccessTools.Field(typeof(NMapScreen), "_map").GetValue(screen) is not GrandMageFinalActMap { ThirdBossMapPoint: { } third } map) return;
        RunState? run = RunManager.Instance.DebugOnlyGetState();
        if (run == null || !ReferenceEquals(run.Map, map)) return;
        Dictionary<MapCoord, NMapPoint> points = Points(screen);
        if (points.ContainsKey(third.coord)) return;
        var parent = (Node)AccessTools.Field(typeof(NMapScreen), "_points").GetValue(screen)!;
        NBossMapPoint last = NBossMapPoint.Create(third, screen, run);
        parent.AddChild(last);
        points.Add(third.coord, last);
        NMapPoint[] bosses = [points[map.BossMapPoint.coord], points[map.SecondBossMapPoint!.coord], last];
        // 原生按钮带居中锚点；入树后必须以现有位置为基准，否则直接写-200会跑到纸面左边。
        Vector2 firstPosition = bosses[0].Position + new Vector2(0, 112);
        for (int index = 0; index < bosses.Length; index++)
        {
            bosses[index].Position = firstPosition + new Vector2(0, -index * 235);
            bosses[index].Scale = Vector2.One * .68f;
        }
    }

    /// <summary>原版只绘制第一位 Boss 的出边；补齐第二至第三位的连线与手柄导航。</summary>
    public static void CompleteMap(NMapScreen screen, ActMap map)
    {
        if (!GodotObject.IsInstanceValid(screen) || map is not GrandMageFinalActMap { ThirdBossMapPoint: { } third }) return;
        Dictionary<MapCoord, NMapPoint> points = Points(screen);
        if (!points.TryGetValue(third.coord, out NMapPoint? last)) return;
        var paths = (Dictionary<(MapCoord, MapCoord), IReadOnlyList<TextureRect>>)AccessTools.Field(typeof(NMapScreen), "_paths").GetValue(screen)!;
        MapPoint second = map.SecondBossMapPoint!;
        if (!paths.ContainsKey((second.coord, third.coord)))
            AccessTools.Method(typeof(NMapScreen), "DrawPaths").Invoke(screen, [points[second.coord], second]);
        NMapPoint[] bosses = [points[map.BossMapPoint.coord], points[second.coord], last];
        for (int index = 0; index < bosses.Length; index++)
        {
            bosses[index].FocusNeighborLeft = bosses[index].GetPath();
            bosses[index].FocusNeighborRight = bosses[index].GetPath();
            bosses[index].FocusNeighborTop = bosses[Math.Min(2, index + 1)].GetPath();
            bosses[index].FocusNeighborBottom = bosses[Math.Max(0, index - 1)].GetPath();
        }
        AccessTools.Method(typeof(NMapScreen), "RecalculateTravelability").Invoke(screen, null);
        AccessTools.Method(typeof(NMapScreen), "RefreshAllPointVisuals").Invoke(screen, null);
        AccessTools.Method(typeof(NMapScreen), "RefreshAllMapPointVotes").Invoke(screen, null);
    }

    /// <summary>读取原生地图按钮字典。</summary>
    private static Dictionary<MapCoord, NMapPoint> Points(NMapScreen screen)
        => (Dictionary<MapCoord, NMapPoint>)AccessTools.Field(typeof(NMapScreen), "_mapPointDictionary").GetValue(screen)!;
}
