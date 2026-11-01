using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

/// <summary>在真实遗物栏显示独立的封印倒计时，不覆盖遗物原有冷却计数或激发状态。</summary>
internal static class GrandMageSealRelicDisplay
{
    /// <summary>封印变化后使用原生显示事件通知已存在的遗物格刷新。</summary>
    internal static void Notify(RelicModel relic)
    {
        if (relic?.IsMutable != true) return;
        AccessTools.Method(typeof(RelicModel), "InvokeDisplayAmountChanged").Invoke(relic, null);
    }

    /// <summary>刷新灰显和红色封印标记，解除后隐藏自己的标记并恢复原生显示。</summary>
    internal static void Refresh(NRelicInventoryHolder holder)
    {
        if (!GodotObject.IsInstanceValid(holder) || holder.Relic == null) return;
        var label = holder.GetNodeOrNull<Label>("GrandMageSealBadge");
        int turns = GrandMageSealInfoService.Remaining(holder.Relic.Model);
        if (label == null && turns > 0)
        {
            label = GmUiManager.CreateLabel(string.Empty, 17);
            label.Name = "GrandMageSealBadge";
            label.MouseFilter = Control.MouseFilterEnum.Ignore;
            label.AutowrapMode = TextServer.AutowrapMode.Off;
            label.Size = new Vector2(56, 24);
            label.Position = new Vector2(-4, -6);
            label.ZIndex = 20;
            label.AddThemeColorOverride("font_color", new Color("ffcc80"));
            label.AddThemeColorOverride("font_shadow_color", new Color("3b0000"));
            label.AddThemeConstantOverride("shadow_offset_x", 2);
            label.AddThemeConstantOverride("shadow_offset_y", 2);
            holder.AddChild(label);
        }
        if (label != null)
        {
            label.Visible = turns > 0;
            label.Text = turns > 0 ? GrandMageSealInfoService.Text("sealBadge", turns) : string.Empty;
        }
        AccessTools.Method(typeof(NRelicInventoryHolder), "RefreshStatus").Invoke(holder, null);
    }
}
