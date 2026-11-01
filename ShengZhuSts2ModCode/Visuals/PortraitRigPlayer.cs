using Godot;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

/// <summary>管理原画蒙皮的原生动画播放器，统一动作回正、循环恢复与死亡优先级。</summary>
public sealed class PortraitRigPlayer
{
    public Node2D Root { get; }
    public AnimationPlayer Player { get; }
    public bool IsDead { get; private set; }
    private string _idle = "idle";
    private readonly double _blendTime;
    private readonly Callable _finished;

    /// <summary>绑定已经实例化的原生节点，不依赖定制引擎的 C# Node 虚方法桥接。</summary>
    public PortraitRigPlayer(Node2D root, double blendTime = 0.10)
    {
        Root = root;
        _blendTime = blendTime;
        Player = root.GetNode<AnimationPlayer>("AnimationPlayer");
        _finished = Callable.From<StringName>(OnFinished);
        Player.Connect(AnimationMixer.SignalName.AnimationFinished, _finished);
        root.TreeExiting += OnExit;
    }

    /// <summary>在游戏主线程播放已存在的片段；死亡不被后续出牌或受击打断。</summary>
    public void Play(string name)
    {
        if (IsDead || !GodotObject.IsInstanceValid(Player) || !Player.IsInsideTree() || !Player.HasAnimation(name)) return;
        IsDead = name == "death";
        Player.Play(name, name == "hurt" ? Math.Min(0.035, _blendTime) : _blendTime);
        Player.Seek(0, true);
    }

    /// <summary>切换默认循环；同一个循环重复刷新不会从头播放。</summary>
    public void SetIdle(string name)
    {
        if (_idle == name || IsDead || !Player.HasAnimation(name)) return;
        string previous = _idle;
        _idle = name;
        if (Player.CurrentAnimation == previous || !Player.IsPlaying()) Play(name);
    }

    /// <summary>动作结束后恢复指定循环，死亡保持最终姿势。</summary>
    private void OnFinished(StringName animation)
    {
        if (!IsDead) Play(_idle);
    }

    /// <summary>游戏可能先释放播放器子节点；只解除仍然存在的连接，避免重复断开报错。</summary>
    private void OnExit()
    {
        if (GodotObject.IsInstanceValid(Player) && Player.IsConnected(AnimationMixer.SignalName.AnimationFinished, _finished))
            Player.Disconnect(AnimationMixer.SignalName.AnimationFinished, _finished);
    }
}
