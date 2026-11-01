namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>一枚符咒的临时封存记录；同名遗物通过玩家编号区分。</summary>
public sealed class GrandMageTalismanSeal
{
    /// <summary>符咒拥有者的稳定联机编号。</summary>
    public ulong OwnerNetId { get; set; }

    /// <summary>符咒模型编号，不保存运行时对象引用。</summary>
    public string RelicId { get; set; } = string.Empty;

    /// <summary>剩余玩家回合数。</summary>
    public int RemainingTurns { get; set; } = 2;
}
