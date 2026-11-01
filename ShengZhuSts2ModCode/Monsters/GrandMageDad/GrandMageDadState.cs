using System.Text.Json;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>
/// 大法师老爹战斗状态：保存后续符咒封存、循环位置和随机结果等需要联机一致的数据。
/// </summary>
public sealed class GrandMageDadState
{
    /// <summary>
    /// 当前状态结构版本，后续字段变化时用于兼容旧存档。
    /// </summary>
    public int StateVersion { get; set; } = 5;

    /// <summary>本场构筑评分是否已经冻结；重建怪物时仍按快照应用一次生命成长。</summary>
    public bool GrowthInitialized { get; set; }

    /// <summary>开局固定的参与人数。</summary>
    public int PlayerCount { get; set; } = 1;

    /// <summary>本场进阶等级，用于力量折算与符咒剥夺数量。</summary>
    public int Ascension { get; set; }

    /// <summary>人数调整前的基础生命，用来解释完整血量构成。</summary>
    public int BaseHp { get; set; }

    /// <summary>全队平均成长评分，不再限制三百分上限。</summary>
    public int AverageGrowth { get; set; }

    /// <summary>各玩家开局成长明细；旧存档缺少明细时留空，不用当前牌组冒充开局来源。</summary>
    public List<GrandMageGrowthSource> GrowthSources { get; set; } = [];

    /// <summary>原生人数缩放后追加的生命。</summary>
    public int GrowthHp { get; set; }

    /// <summary>生命成长按进阶除数和开局人数四舍五入得到的力量。</summary>
    public int GrowthStrength { get; set; }

    /// <summary>本场初始化完成时的最大生命，用于软上限与破封阈值。</summary>
    public int InitialMaxHp { get; set; }

    /// <summary>当前五行阵阶段；零表示没有玩家压制。</summary>
    public int CycleStage { get; set; }

    /// <summary>本轮是否已经通过累计伤害解除过封存。</summary>
    public bool ReleasedSealsThisTurn { get; set; }

    /// <summary>按玩家归属保存的临时符咒封存，不移除遗物本体。</summary>
    public List<GrandMageTalismanSeal> SealedTalismans { get; set; } = [];

    /// <summary>
    /// 老爹已经执行过的行动次数。
    /// </summary>
    public int TurnIndex { get; set; }

    /// <summary>
    /// 当前副意图代码；第一版只记录，后续接双意图 UI 时读取。
    /// </summary>
    public string SecondaryIntentCode { get; set; } = string.Empty;

    /// <summary>
    /// 本回合累计承伤，后续给正气不灭和符咒返还使用。
    /// </summary>
    public int DamageTakenThisTurn { get; set; }

    /// <summary>
    /// 是否正在终极封印吟唱中；处决回合会读取它判断是否继续处决。
    /// </summary>
    public bool IsChanting { get; set; }

    /// <summary>
    /// 吟唱开始时记录的护盾值，用于读档和 UI 展示。
    /// </summary>
    public int ChantShieldAmount { get; set; }

    /// <summary>
    /// 序列化为存档字符串。
    /// </summary>
    /// <returns>JSON 字符串。</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

    /// <summary>
    /// 从存档字符串恢复老爹状态；损坏时返回空状态，避免读档直接中断。
    /// </summary>
    /// <param name="json">存档里的 JSON 字符串。</param>
    /// <returns>恢复后的老爹状态。</returns>
    public static GrandMageDadState FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new GrandMageDadState();
        }

        try
        {
            var state = JsonSerializer.Deserialize<GrandMageDadState>(json) ?? new GrandMageDadState();
            state.SealedTalismans ??= [];
            state.GrowthSources ??= [];
            state.GrowthSources.RemoveAll(source => source == null);
            foreach (var source in state.GrowthSources)
            {
                source.Items ??= [];
                source.Items.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.Entry));
            }
            state.SealedTalismans.RemoveAll(seal => seal == null || string.IsNullOrWhiteSpace(seal.RelicId) || seal.RemainingTurns <= 0);
            return state;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【联机异常兜底】【大法师老爹】阶段=读取自定义状态，原因=JSON解析失败，处理=使用空状态，异常={ex.Message}。");
            return new GrandMageDadState();
        }
    }
}
