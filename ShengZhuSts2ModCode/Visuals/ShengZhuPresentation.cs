using System.Runtime.CompilerServices;
using Godot;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

/// <summary>每个圣主视觉实例独立持有动作和变身进度；只读取已同步的目标形态。</summary>
public sealed class ShengZhuPresentation
{
    private static readonly ConditionalWeakTable<Node, ShengZhuPresentation> Instances = new();
    private readonly Node2D _root;
    private readonly Node2D _statue;
    private readonly Node2D _human;
    private readonly PortraitRigPlayer _rig;
    private readonly ShaderMaterial _humanMaterial;
    private readonly ShaderMaterial _stoneMaterial;
    private readonly ShengZhuVisualEffects _effects;
    private SceneTree? _tree;
    private bool _targetHuman;
    private float _mix;
    private float _time;
    private float _poseTime;
    private float _death;
    private string _pose = "idle";

    /// <summary>查找实例内的节点，兼容战斗、商店、营火的共享外观子场景。</summary>
    public static Node2D? Find(Node? root, string name)
        => root != null && GodotObject.IsInstanceValid(root) ? root.FindChild(name, true, false) as Node2D : null;

    /// <summary>首次进入房间直接应用存档形态，后续状态变化才播放双向过渡。</summary>
    public static void Refresh(Node? root, bool human)
    {
        if (root == null || !GodotObject.IsInstanceValid(root) || !root.IsInsideTree()) return;
        if (!Instances.TryGetValue(root, out var instance))
        {
            Node2D? statue = Find(root, "ShengZhuStatueVisual"), body = Find(root, "ShengZhuTrueFormVisual");
            if (statue == null || body?.GetNodeOrNull<AnimationPlayer>("AnimationPlayer") == null) return;
            instance = new ShengZhuPresentation((Node2D)statue.GetParent(), statue, body, human);
            Instances.Add(root, instance);
        }
        instance.SetForm(human);
    }

    /// <summary>向指定玩家自己的外观发送动作；没有视觉节点时保持静默。</summary>
    public static void Play(Node? root, string pose)
    {
        if (root != null && GodotObject.IsInstanceValid(root) && Instances.TryGetValue(root, out var instance)) instance.PlayPose(pose);
    }

    /// <summary>初始化本地材质与一次性特效，生命周期与角色节点一致。</summary>
    private ShengZhuPresentation(Node2D root, Node2D statue, Node2D human, bool showHuman)
    {
        _root = root;
        _statue = statue;
        _human = human;
        _targetHuman = showHuman;
        _mix = showHuman ? 1 : 0;
        _rig = new PortraitRigPlayer(human);
        var shader = ResourceLoader.Load<Shader>("res://ShengZhuSts2Mod/shaders/portrait_energy.gdshader");
        _humanMaterial = new ShaderMaterial { Shader = shader };
        _stoneMaterial = new ShaderMaterial { Shader = shader };
        _stoneMaterial.SetShaderParameter("invert_order", true);
        human.GetNode<Polygon2D>("PortraitMesh").Material = _humanMaterial;
        statue.Material = _stoneMaterial;
        var head = (Node2D)human.FindChild("Head", true, false);
        _effects = new ShengZhuVisualEffects(root, head);
        _tree = root.GetTree();
        _tree.ProcessFrame += OnFrame;
        root.TreeExiting += OnExit;
        UpdateForm();
        if (root.GetParent().Name.ToString().Contains("Rest")) _rig.SetIdle("rest");
        else PlayPose("enter");
        MainFile.Logger.Info($"【圣主动画】原画蒙皮就绪：节点={root.GetPath()}，初始形态={(showHuman ? "真身" : "石板")}，20根骨骼。");
    }

    /// <summary>只更新视觉目标，快速连续反向切换从当前进度衔接。</summary>
    private void SetForm(bool human)
    {
        if (_targetHuman == human || _rig.IsDead) return;
        _targetHuman = human;
        PlayPose(human ? "enter" : "defend");
        MainFile.Logger.Info($"【圣主动画】播放形态过渡：目标={(human ? "真身" : "石板")}。");
    }

    /// <summary>记录当前动作供纯视觉特效使用，不等待动画完成。</summary>
    private void PlayPose(string pose)
    {
        if (_rig.IsDead || !_root.IsInsideTree()) return;
        _pose = pose;
        _poseTime = 0;
        _rig.Play(pose);
    }

    /// <summary>使用原生帧信号更新外观，不定义自定义 Node 子类或战斗时钟。</summary>
    private void OnFrame()
    {
        if (!GodotObject.IsInstanceValid(_root) || !_root.IsInsideTree() || !_root.IsVisibleInTree()) return;
        float delta = Math.Min((float)_root.GetProcessDeltaTime(), .1f);
        _time += delta;
        _poseTime += delta;
        _mix = Mathf.MoveToward(_mix, _targetHuman ? 1 : 0, delta / .85f);
        if (_rig.IsDead) _death = Math.Min(1, _death + delta / 1.15f);
        float duration = (float)(_rig.Player.GetAnimation(_pose)?.Length ?? 1);
        float phase = Math.Clamp(_poseTime / Math.Max(.01f, duration), 0, 1);
        if (phase >= 1 && !_rig.IsDead) _pose = "idle";
        UpdateForm();
        float transition = MathF.Sin(_mix * MathF.PI);
        _effects.Update(_time, _mix, transition, transition > .05f ? "transform" : _pose, phase, _death);
        _statue.Rotation = _pose == "hurt" ? MathF.Sin(phase * MathF.Tau * 2) * .025f * (1 - phase) : 0;
    }

    /// <summary>统一脚底锚点与溶解边缘，终点严格只显示一种形态。</summary>
    private void UpdateForm()
    {
        _human.Visible = _mix > 0;
        _statue.Visible = _mix < 1;
        _humanMaterial.SetShaderParameter("dissolve", Math.Max(1 - _mix, _death));
        _humanMaterial.SetShaderParameter("stone_mix", MathF.Sin(_mix * MathF.PI) * .65f);
        _humanMaterial.SetShaderParameter("pulse", .10f + MathF.Sin(_time * 2) * .04f);
        _stoneMaterial.SetShaderParameter("dissolve", Math.Max(_mix, _death));
        _stoneMaterial.SetShaderParameter("pulse", .18f + MathF.Sin(_time * 2.3f) * .10f);
    }

    /// <summary>离开场景取消帧订阅，避免旧角色继续持有粒子和材质。</summary>
    private void OnExit()
    {
        if (_tree != null && GodotObject.IsInstanceValid(_tree)) _tree.ProcessFrame -= OnFrame;
        _tree = null;
    }
}
