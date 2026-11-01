using Godot;
using MegaCrit.Sts2.Core.Combat;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

/// <summary>默认只显示情报按钮，点击后展开摘要与说明页；控件只读取状态。</summary>
internal sealed class GrandMageBattleInfoPanel
{
    private readonly GrandMageDad _monster;
    private readonly Control _root = new() { Name = "GrandMageBattleInfo", TopLevel = true, MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 200, ZAsRelative = false };
    private readonly PanelContainer _panel = new() { Name = "InfoPanel", MouseFilter = Control.MouseFilterEnum.Stop };
    private readonly Label _title = GmUiManager.CreateLabel(string.Empty, 18);
    private readonly Label _summary = GmUiManager.CreateLabel(string.Empty, 16);
    private readonly Label _details = GmUiManager.CreateLabel(string.Empty, 18);
    private readonly ScrollContainer _scroll = new() { Name = "DetailsScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
    private readonly Button _close = GmUiManager.CreateTextButton(string.Empty);
    private readonly Button _toggle = GmUiManager.CreateTextButton(string.Empty);
    private readonly List<Button> _tabs = [];
    private SceneTree? _tree;
    private double _refreshTime;
    private int _page = -1;
    private bool _expanded;

    /// <summary>使用统一 UI 样式构建紧凑条与滚动详情，场景里不挂自定义 Godot 虚方法。</summary>
    private GrandMageBattleInfoPanel(GrandMageDad monster)
    {
        _monster = monster;
        _root.Visible = false;
        _panel.Visible = false;
        _toggle.Name = "InfoToggleButton";
        _toggle.Pressed += Toggle;
        _toggle.GuiInput += HandleInput;
        _root.AddChild(_toggle);
        var style = GmUiManager.CreatePanelStyle();
        // 长篇说明需要遮住身后的能力悬停文字，避免两层文字混在一起。
        style.BgColor = new Color(0.08f, 0.07f, 0.06f, 0.98f);
        _panel.AddThemeStyleboxOverride("panel", style);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 9);
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        _title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _title.AutowrapMode = TextServer.AutowrapMode.Off;
        _title.MouseFilter = Control.MouseFilterEnum.Ignore;
        header.AddChild(_title);
        for (int page = 0; page < 3; page++)
        {
            int selected = page;
            var button = GmUiManager.CreateTextButton(string.Empty);
            button.Name = new[] { "GrowthButton", "CycleButton", "RulesButton" }[page];
            button.Pressed += () => OpenPage(selected);
            button.GuiInput += HandleInput;
            _tabs.Add(button);
            header.AddChild(button);
        }
        _close.Name = "CollapseButton";
        _close.Pressed += Collapse;
        _close.GuiInput += HandleInput;
        header.AddChild(_close);
        column.AddChild(header);
        _summary.Name = "BattleSummary";
        _summary.MouseFilter = Control.MouseFilterEnum.Ignore;
        column.AddChild(_summary);
        _details.Name = "BattleDetails";
        _details.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _details.MouseFilter = Control.MouseFilterEnum.Ignore;
        _scroll.AddChild(_details);
        _scroll.GuiInput += HandleInput;
        column.AddChild(_scroll);
        _panel.AddChild(column);
        _root.AddChild(_panel);
        _panel.GuiInput += HandleInput;
        _root.TreeEntered += EnterTree;
        _root.TreeExiting += ExitTree;
    }

    /// <summary>独立于人物动画挂载情报，备用图片也能提供同样说明。</summary>
    internal static void Attach(Node parent, GrandMageDad monster)
    {
        var panel = new GrandMageBattleInfoPanel(monster);
        parent.AddChild(panel._root);
    }

    /// <summary>进入当前场景后订阅轻量刷新。</summary>
    private void EnterTree()
    {
        _tree = _root.GetTree();
        _tree.ProcessFrame += RefreshFrame;
        Refresh();
    }

    /// <summary>离开战斗时解除订阅，不让旧局的说明继续显示或持有对象。</summary>
    private void ExitTree()
    {
        if (_tree != null && GodotObject.IsInstanceValid(_tree)) _tree.ProcessFrame -= RefreshFrame;
        _tree = null;
    }

    /// <summary>每五分之一秒读取一次状态，只在文本变化时更新标签。</summary>
    private void RefreshFrame()
    {
        _refreshTime += _root.GetProcessDeltaTime();
        if (_refreshTime < .2) return;
        _refreshTime = 0;
        Refresh();
    }

    /// <summary>打开某页只改变本地控件，滚动条回到页首并保持按钮可用。</summary>
    private void OpenPage(int page)
    {
        if (_monster.VisualCreature?.IsAlive != true || !CombatManager.Instance.IsInProgress) return;
        _page = Math.Clamp(page, 0, 2);
        _expanded = true;
        _scroll.ScrollVertical = 0;
        Refresh();
        MainFile.Logger.Info($"【老爹战斗情报】打开说明页={_page}，界面仅展示当前战斗状态。");
    }

    /// <summary>收起整块情报，仅保留入口按钮；焦点返回入口。</summary>
    private void Collapse()
    {
        _page = -1;
        _expanded = false;
        Refresh();
        _toggle.GrabFocus();
        MainFile.Logger.Info("【老爹战斗情报】整块情报已收起，仅保留本地入口按钮。");
    }

    /// <summary>入口按钮展开实时摘要，再次点击隐藏整块情报。</summary>
    private void Toggle()
    {
        if (_monster.VisualCreature?.IsAlive != true || !CombatManager.Instance.IsInProgress) return;
        if (_expanded) { Collapse(); return; }
        _expanded = true;
        _page = -1;
        Refresh();
        MainFile.Logger.Info("【老爹战斗情报】展开实时摘要，未修改战斗状态。");
    }

    /// <summary>详情获得键盘焦点时允许按 Escape 收起，防止输入继续传到战斗。</summary>
    private void HandleInput(InputEvent input)
    {
        if (!_expanded || input is not InputEventKey { Pressed: true, Keycode: Key.Escape }) return;
        Collapse();
        _panel.AcceptEvent();
    }

    /// <summary>只在有效老爹战斗内显示；窗口大小改变后让说明保持在手牌区域上方。</summary>
    private void Refresh()
    {
        if (!GodotObject.IsInstanceValid(_root)) return;
        var creature = _monster.VisualCreature;
        bool visible = creature?.IsAlive == true && CombatManager.Instance.IsInProgress
            && creature.CombatState?.Encounter is GrandMageDadEncounter { State.GrowthInitialized: true }
            && _monster.NextMove != null;
        _root.Visible = visible;
        if (!visible) { _expanded = false; _page = -1; }
        if (!visible || creature!.CombatState!.Encounter is not GrandMageDadEncounter encounter) return;
        Vector2 viewport = _root.GetViewportRect().Size;
        _toggle.Text = GmUiText.Get(_expanded ? "dad.info.hide" : "dad.info.show");
        _toggle.TooltipText = GmUiText.Get("dad.info.toggleHint");
        _toggle.Size = new Vector2(144, 34);
        _toggle.Position = new Vector2((viewport.X - _toggle.Size.X) / 2, 84);
        _panel.Visible = _expanded;
        if (!_expanded) return;
        _title.Text = GmUiText.Get("dad.info.title");
        string[] keys = ["growthTab", "cycleTab", "rulesTab"];
        for (int index = 0; index < _tabs.Count; index++) _tabs[index].Text = GmUiText.Get("dad.info." + keys[index]);
        _close.Text = GmUiText.Get("dad.info.close");
        _close.Visible = true;
        _scroll.Visible = _page >= 0;
        _summary.Visible = _page < 0;
        string text = _page < 0 ? GrandMageBattleInfoService.BuildSummary(_monster, encounter.State)
            : GrandMageBattleInfoService.BuildDetails(_monster, encounter.State, _page);
        Label label = _page < 0 ? _summary : _details;
        if (label.Text != text) label.Text = text;
        float width = Math.Min(_page < 0 ? 760 : 980, viewport.X - 64);
        float height = _page < 0 ? 0 : Math.Max(240, Math.Min(560, viewport.Y - 340));
        _panel.Position = new Vector2((viewport.X - width) / 2, 126);
        _panel.CustomMinimumSize = new Vector2(width, height);
        _panel.Size = new Vector2(width, height);
        _scroll.CustomMinimumSize = new Vector2(0, _page < 0 ? 0 : height - 70);
    }
}
