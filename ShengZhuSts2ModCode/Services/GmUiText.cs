using ShengZhuSts2Mod.ShengZhuSts2ModCode.Keywords;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

/// <summary>
/// 圣主 Mod UI 文案表。
/// 选人界面的代码生成控件无法直接使用原版 JSON LocString，这里集中管理中英文文本。
/// </summary>
public static partial class GmUiText
{
    /// <summary>
    /// 获取当前语言下的 UI 文案。
    /// </summary>
    /// <param name="key">文案键。</param>
    /// <returns>文案内容。</returns>
    public static string Get(string key)
    {
        if (key.StartsWith("dad.info.", StringComparison.Ordinal))
            return GetGrandMageText(key, KeywordRegistry.LoadedLanguage == "zhs");
        return KeywordRegistry.LoadedLanguage == "zhs"
            ? GetChinese(key)
            : GetEnglish(key);
    }

    /// <summary>
    /// 获取剧情事件在 UI 上显示的名称。配置、存档和联机仍使用中文名作为稳定键。
    /// </summary>
    /// <param name="eventName">剧情事件中文配置名。</param>
    /// <returns>当前语言下的显示名称。</returns>
    public static string GetStoryEventName(string eventName)
    {
        if (KeywordRegistry.LoadedLanguage == "zhs")
        {
            return eventName;
        }

        return eventName switch
        {
            "远古封印" => "Ancient",
            "恶魔小龙" => "Drago",
            "刀龙黑气" => "Dark Chi",
            "西瓦的手镯" => "Shiva",
            "岁月史书" => "Book",
            _ => eventName
        };
    }

    /// <summary>
    /// 中文文案。
    /// </summary>
    /// <param name="key">文案键。</param>
    /// <returns>中文文本。</returns>
    private static string GetChinese(string key)
    {
        return key switch
        {
            "story_event_config.title" => "圣主事件池",
            "story_event_config.act" => "楼层",
            "story_event_config.events" => "事件",
            "story_event_config.reset" => "重置",
            "story_event_config.closed" => "不选事件=关闭",
            "story_event_config.act1" => "1层",
            "story_event_config.act2" => "2层",
            "story_event_config.act3" => "3层",
            "dad.growth" => "正气试炼 · {0}人 · 成长{1}/300 · 力量+{2}",
            "dad.stage.0" => "阵法解除：抓住进攻窗口",
            "dad.stage.1" => "金阵：前两张攻击每段伤害-2",
            "dad.stage.2" => "木阵：回合末消耗一张牌，或承受4伤害",
            "dad.stage.3" => "水阵：前三张牌各反震2伤害",
            "dad.stage.4" => "火阵：每张牌使后续伤害/格挡-10%，保底60%",
            "dad.stage.5" => "土阵：破盾打断吟唱，全队解除封存",
            "dad.shield" => "待破护盾 {0}",
            "dad.seals" => "封存（剩余回合）：{0}",
            "dad.none" => "无",
            "dad.owner" => "{0}号·{1}({2})",
            _ => key
        };
    }

    /// <summary>
    /// 英文文案。
    /// </summary>
    /// <param name="key">文案键。</param>
    /// <returns>英文文本。</returns>
    private static string GetEnglish(string key)
    {
        return key switch
        {
            "story_event_config.title" => "Events",
            "story_event_config.act" => "Act",
            "story_event_config.events" => "Events",
            "story_event_config.reset" => "Reset",
            "story_event_config.closed" => "No events = off",
            "story_event_config.act1" => "Act 1",
            "story_event_config.act2" => "Act 2",
            "story_event_config.act3" => "Act 3",
            "dad.growth" => "Chi Trial · {0} players · Growth {1}/300 · Strength +{2}",
            "dad.stage.0" => "Formation broken: attack window",
            "dad.stage.1" => "Metal: first 2 Attacks deal 2 less per hit",
            "dad.stage.2" => "Wood: exhaust 1 card at turn end, or take 4 damage",
            "dad.stage.3" => "Water: first 3 cards reflect 2 damage each",
            "dad.stage.4" => "Fire: -10% damage/Block per card, minimum 60%",
            "dad.stage.5" => "Earth: break Block to interrupt and release seals",
            "dad.shield" => "Block to break: {0}",
            "dad.seals" => "Sealed (turns remaining): {0}",
            "dad.none" => "None",
            "dad.owner" => "P{0} {1}({2})",
            _ => key
        };
    }
}
