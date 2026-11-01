using System.Text.Json.Serialization;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>保存一名玩家开局时的成长来源，供评分与战斗说明共用，避免显示时重新采样。</summary>
public sealed class GrandMageGrowthSource
{
    /// <summary>拥有这些构筑内容的玩家编号。</summary>
    public ulong OwnerNetId { get; set; }

    /// <summary>具体卡牌、遗物与魔气的开局账目，旧档可以缺少此列表。</summary>
    public List<GrandMageGrowthItem> Items { get; set; } = [];

    /// <summary>开局拥有的动物符咒数量。</summary>
    public int Talismans { get; set; }

    /// <summary>开局拥有的不同面具数量，无尽黑暗按十种计算。</summary>
    public int Masks { get; set; }

    /// <summary>开局拥有的不同魔气数量。</summary>
    public int DemonKinds { get; set; }

    /// <summary>开局永久牌组中的不同觉醒牌数量。</summary>
    public int Awakenings { get; set; }

    /// <summary>开局永久牌组中已升级的非基础牌数量。</summary>
    public int UpgradedCards { get; set; }

    /// <summary>符咒评分，每枚12分，前两枚同样计分。</summary>
    [JsonIgnore] public int TalismanScore => Math.Clamp(Talismans, 0, 12) * 12;

    /// <summary>不同面具评分，最多十种。</summary>
    [JsonIgnore] public int MaskScore => Math.Clamp(Masks, 0, 10) * 6;

    /// <summary>不同魔气评分，最多八种。</summary>
    [JsonIgnore] public int DemonScore => Math.Clamp(DemonKinds, 0, 8) * 10;

    /// <summary>不同觉醒牌评分，最多三种。</summary>
    [JsonIgnore] public int AwakeningScore => Math.Clamp(Awakenings, 0, 3) * 40;

    /// <summary>已升级非基础牌评分，最多十二张。</summary>
    [JsonIgnore] public int UpgradeScore => Math.Clamp(UpgradedCards, 0, 12) * 2;

    /// <summary>五项构筑评分合计。</summary>
    [JsonIgnore] public int RawScore => TalismanScore + MaskScore + DemonScore + AwakeningScore + UpgradeScore;

    /// <summary>实际用于队伍平均的个人评分。</summary>
    [JsonIgnore] public int Score => RawScore;
}
