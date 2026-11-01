namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>
/// 大法师老爹注册入口：集中触发怪物和遭遇模型注册。
/// </summary>
public static class GrandMageDadRegistry
{
    private static GrandMageDadEncounter? _encounter;
    private static GrandMageTrialEncounter? _trialEncounter;

    /// <summary>
    /// 确保大法师老爹相关模型被 ModelDb 和 BaseLib 注册。
    /// </summary>
    public static void Initialize()
    {
        bool isCreated = _encounter == null;
        _encounter ??= new GrandMageDadEncounter();
        _trialEncounter ??= new GrandMageTrialEncounter();
        MainFile.Logger.Info($"【大法师老爹】遭遇注册完成：本次新建={isCreated}，调试遭遇={_encounter.Id}，第三章试炼遭遇={_trialEncounter.Id}，均不加入关底或普通精英池。");
    }
}
