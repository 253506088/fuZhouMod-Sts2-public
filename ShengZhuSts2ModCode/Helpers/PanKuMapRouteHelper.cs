using MegaCrit.Sts2.Core.Map;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>沿实际地图连线选择可依次挑战的恶魔门，避免同层或互斥岔路浪费魔气。</summary>
internal static class PanKuMapRouteHelper
{
    /// <summary>先保证可收集数量，再保留旧门、优先天然精英；固定排序不消耗游戏随机数。</summary>
    internal static List<MapPoint> ChoosePoints(ActMap map, MapCoord? currentCoord, int needed,
        PanKuBox box, Func<MapPoint, uint> score)
    {
        if (needed <= 0) return [];
        MapPoint? start = currentCoord.HasValue ? map.GetPoint(currentCoord.Value) : map.StartingMapPoint;
        if (start == null) return [];

        int currentRow = currentCoord?.row ?? start.coord.row;
        List<MapPoint> points = map.GetAllMapPoints().Append(map.BossMapPoint).Append(start).Distinct().ToList();
        List<PanKuBox> otherBoxes = box.Owner.RunState.Players.Select(player => player.GetRelic<PanKuBox>())
            .OfType<PanKuBox>().Where(other => other != box).ToList();
        Dictionary<int, MapCoord> occupiedRows = points
            .Where(point => otherBoxes.Any(other => other.HasMarkedCoord(point.coord)))
            .GroupBy(point => point.coord.row).ToDictionary(group => group.Key, group => group.First().coord);
        Dictionary<MapPoint, List<MapPoint>> routes = [];
        foreach (MapPoint point in points.OrderByDescending(point => point.coord.row).ThenBy(point => point.coord.col))
        {
            // 从上往下计算最佳后续路线，只沿前进连线，避免把另一条岔路当成可达节点。
            List<MapPoint> tail = point.Children.Where(child => child.coord.row > point.coord.row && routes.ContainsKey(child))
                .OrderByDescending(child => Math.Min(routes[child].Count, needed))
                .ThenByDescending(child => Math.Min(routes[child].Count(p => box.HasMarkedCoord(p.coord)), needed))
                .ThenByDescending(child => Math.Min(routes[child].Count(p => p.PointType == MapPointType.Elite), needed))
                .ThenBy(score).ThenBy(child => child.coord.col)
                .Select(child => routes[child]).FirstOrDefault() ?? [];
            bool available = point.coord.row > currentRow
                && (point.CanBeModified || box.HasMarkedCoord(point.coord))
                && point.PointType is MapPointType.Elite or MapPointType.Monster
                && (!occupiedRows.TryGetValue(point.coord.row, out MapCoord occupied) || occupied.Equals(point.coord));
            routes[point] = available ? new List<MapPoint> { point }.Concat(tail).ToList() : tail;
        }

        return routes[start].OrderByDescending(point => box.HasMarkedCoord(point.coord))
            .ThenByDescending(point => point.PointType == MapPointType.Elite)
            .ThenBy(score).ThenBy(point => point.coord.col).Take(needed)
            .OrderBy(point => point.coord.row).ToList();
    }
}
