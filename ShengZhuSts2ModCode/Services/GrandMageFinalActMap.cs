using MegaCrit.Sts2.Core.Map;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

/// <summary>保留原版普通路线和两位 Boss，只在末端连接老爹；额外节点单独交给原生地图保存。</summary>
public sealed class GrandMageFinalActMap : ActMap
{
    public override MapPoint BossMapPoint { get; }
    public override MapPoint StartingMapPoint { get; }
    public override MapPoint? SecondBossMapPoint { get; }
    public MapPoint? ThirdBossMapPoint { get; }
    public MapPoint FinalBossPoint => ThirdBossMapPoint ?? SecondBossMapPoint!;
    protected override MapPoint?[,] Grid { get; }

    /// <summary>复用已有节点，读档时复用已保存的第三节点，避免多次刷新增加重复 Boss。</summary>
    public GrandMageFinalActMap(ActMap source, bool tripleBoss)
    {
        BossMapPoint = source.BossMapPoint;
        StartingMapPoint = source.StartingMapPoint;
        Grid = new MapPoint?[source.GetColumnCount(), BossMapPoint.coord.row];
        foreach (MapPoint point in source.GetAllMapPoints())
            if (point.coord.row < Grid.GetLength(1)) Grid[point.coord.col, point.coord.row] = point;
        foreach (MapPoint point in source.startMapPoints) startMapPoints.Add(point);
        SecondBossMapPoint = source.SecondBossMapPoint ?? CreateBoss(BossMapPoint.coord.row + 1);
        BossMapPoint.AddChildPoint(SecondBossMapPoint);
        if (tripleBoss)
        {
            int row = SecondBossMapPoint.coord.row + 1;
            ThirdBossMapPoint = source.GetPoint(BossMapPoint.coord.col, row) ?? CreateBoss(row);
            SecondBossMapPoint.AddChildPoint(ThirdBossMapPoint);
        }
    }

    /// <summary>为原生选图、加载和网络坐标查找提供第三个 Boss。</summary>
    public override MapPoint? GetPoint(MapCoord coord)
        => ThirdBossMapPoint?.coord == coord ? ThirdBossMapPoint : base.GetPoint(coord);

    /// <summary>创建不可被随机地图效果改写的 Boss 节点。</summary>
    private MapPoint CreateBoss(int row)
        => new(BossMapPoint.coord.col, row) { PointType = MapPointType.Boss, CanBeModified = false };
}
