namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>开局成长账目中的具体物品；只保存模型标识和数量，显示时再按当前语言取名称。</summary>
public sealed class GrandMageGrowthItem
{
    /// <summary>所属计分项：符咒、面具、魔气、觉醒、升级或不计分。</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>名称所在的本地化表；魔气使用独立枚举标识。</summary>
    public string Table { get; set; } = string.Empty;

    /// <summary>原始模型标识，不保存某个客户端的语言文本。</summary>
    public string Entry { get; set; } = string.Empty;

    /// <summary>附魔卡对应的魔气种类，便于把魔气评分追溯到具体卡牌。</summary>
    public string DemonKind { get; set; } = string.Empty;

    /// <summary>永久牌组或遗物栏中的实际数量。</summary>
    public int Count { get; set; }
}
