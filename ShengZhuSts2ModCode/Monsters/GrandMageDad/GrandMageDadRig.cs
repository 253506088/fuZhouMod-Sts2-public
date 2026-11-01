using System.Runtime.CompilerServices;
using Godot;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>由原生节点组成的老爹部件动画，不依赖定制 Godot 的 C# 虚方法桥接。</summary>
public sealed class GrandMageDadRig
{
    internal const string RigScenePath = "res://ShengZhuSts2Mod/scenes/portraits/grand_mage_dad_rig.tscn";
    private static readonly ConditionalWeakTable<Node2D, GrandMageDadRig> Rigs = new();
    private readonly GrandMageDad _monster;
    private readonly Node2D _root = new() { Name = "Visuals" };
    private readonly Node2D _body;
    private readonly PortraitRigPlayer _portrait;
    private readonly Node2D _halo = new() { Position = new Vector2(0, -192) };
    private Sprite2D? _auraFlame;
    private Sprite2D? _auraGlow;
    private const string AuraFlamePath = "res://ShengZhuSts2Mod/images/monsters/grand_mage_dad/redrawn/aura_flame.png";
    private const string AuraGlowPath = "res://ShengZhuSts2Mod/images/monsters/grand_mage_dad/redrawn/green_aura_glow.png";
    private const string AuraFlameShaderCode = """
        shader_type canvas_item;

        void fragment() {
            vec2 uv = UV;
            float upper = 1.0 - UV.y;
            float wave = sin(TIME * 2.3 + UV.y * 9.0 + UV.x * 4.0);
            // 顶部受扰动，底部锚住人物轮廓，避免整张火焰平移。
            uv.x += wave * 0.014 * upper;
            uv.y += sin(TIME * 1.8 + UV.x * 7.0 + UV.y * 3.0) * 0.005 * upper;
            COLOR = texture(TEXTURE, uv) * COLOR;
            COLOR.a *= 0.97 + 0.03 * sin(TIME * 5.0 + UV.x * 8.0 + UV.y * 4.0);
        }
        """;
    private SceneTree? _tree;
    private string _pose = "idle";
    private double _time;
    private double _poseTime;

    /// <summary>一次性建立部件和法阵，所有纹理由 Godot 缓存复用。</summary>
    private GrandMageDadRig(GrandMageDad monster)
    {
        _monster = monster;
        BuildGeometry();
        _body = ResourceLoader.Load<PackedScene>(RigScenePath).Instantiate<Node2D>();
        _body.Name = "BodyRig";
        // 袖型可见性不能跨片段混合，否则被打断的旧动作会重新显示隐藏手臂。
        _portrait = new PortraitRigPlayer(_body, 0);
        _root.AddChild(_body);
        _root.TreeEntered += OnTreeEntered;
        _root.TreeExiting += OnTreeExiting;
    }

    /// <summary>创建动画根节点并用弱引用表关联控制器。</summary>
    internal static Node2D Create(GrandMageDad monster)
    {
        var rig = new GrandMageDadRig(monster);
        Rigs.Add(rig._root, rig);
        return rig._root;
    }

    /// <summary>给当前仍存在的视觉节点播放姿态。</summary>
    internal static void Play(Node2D root, string pose)
    {
        if (GodotObject.IsInstanceValid(root) && Rigs.TryGetValue(root, out var rig)) rig.PlayPose(pose);
    }

    /// <summary>进入场景时订阅原生主循环信号，避免自定义 Node 子类桥接。</summary>
    private void OnTreeEntered()
    {
        _tree = _root.GetTree();
        _tree.ProcessFrame += OnFrame;
    }

    /// <summary>退出场景时解除帧订阅，不让旧战斗节点被永久保留。</summary>
    private void OnTreeExiting()
    {
        if (_tree != null && GodotObject.IsInstanceValid(_tree)) _tree.ProcessFrame -= OnFrame;
        _tree = null;
    }

    /// <summary>切换动画片段；未知片段退回待机。</summary>
    public void PlayPose(string pose)
    {
        if (_pose == "death") return;
        if (pose == "attack") pose = "thunder";
        if (pose == "chant")
        {
            _portrait.SetIdle("chant");
            pose = "chant_start";
        }
        _pose = _portrait.Player.HasAnimation(pose) ? pose : "idle";
        _poseTime = 0;
        _portrait.Play(_pose);
    }

    /// <summary>按时间插值部件，不加载新贴图、不访问战斗随机源。</summary>
    private void OnFrame()
    {
        if (!GodotObject.IsInstanceValid(_root) || !_root.IsInsideTree() || !GodotObject.IsInstanceValid(_body)) return;
        double delta = _root.GetProcessDeltaTime();
        _time += delta;
        _poseTime += delta;
        bool chanting = _monster.VisualCreature?.CombatState?.Encounter is GrandMageDadEncounter encounter && encounter.State.IsChanting;
        _portrait.SetIdle(chanting ? "chant" : "idle");
        double duration = _portrait.Player.GetAnimation(_pose)?.Length ?? 1;
        if (_pose is not ("idle" or "chant" or "death") && _poseTime >= duration)
        {
            _pose = chanting ? "chant" : "idle";
            _poseTime = 0;
        }
        float dying = _pose == "death" ? Math.Clamp(((float)_poseTime - .35f) / 1f, 0, 1) : 0;
        // 待机不再整体抖动，头颈也没有周期摇摆；动作只由骨骼片段驱动。
        _body.Position = new Vector2(0, dying * 8);
        _body.Modulate = new Color(1, 1, 1, 1 - dying);
        AnimateAura(dying);
        _halo.Rotation += (float)delta * (chanting ? -0.28f : -0.08f);
        _halo.Scale = Vector2.One * (1 + (chanting ? MathF.Sin((float)_time * 4) * 0.035f : 0));
        _halo.Modulate = new Color(1, 1, 1, (chanting ? .6f : 0) * (1 - dying));
    }

    /// <summary>创建背焰贴图、柔光、脚下阴影和吟唱氛围光。</summary>
    private void BuildGeometry()
    {
        _auraGlow = AddAuraSprite(AuraGlowPath, "GrandMageDadAuraGlow", new Color(1, 1, 1, 0.55f));
        _auraFlame = AddAuraSprite(AuraFlamePath, "GrandMageDadAuraFlame", new Color(1, 1, 1, 0.72f));
        _root.AddChild(new Polygon2D { Polygon = CirclePoints(74, 32), Scale = new Vector2(1, 0.22f), Position = new Vector2(0, 3), Color = new Color(0.04f, 0.12f, 0.1f, 0.5f) });
        _root.AddChild(_halo);
        _halo.AddChild(new Polygon2D { Polygon = CirclePoints(128, 64), Color = new Color(0.3f, 0.8f, 0.45f, 0.06f) });
    }

    /// <summary>按本地时间驱动背焰明暗和柔光呼吸，不读取战斗状态或随机数。</summary>
    private void AnimateAura(float dying)
    {
        if (GodotObject.IsInstanceValid(_auraFlame))
        {
            float flicker = MathF.Sin((float)_time * 4.1f) * 0.025f;
            _auraFlame!.Modulate = new Color(1, 1, 1, (0.72f + flicker) * (1 - dying));
        }
        if (GodotObject.IsInstanceValid(_auraGlow))
        {
            float pulse = MathF.Sin((float)_time * 1.6f) * 0.045f;
            _auraGlow!.Modulate = new Color(1, 1, 1, (0.55f + pulse) * (1 - dying));
        }
    }

    /// <summary>把透明背焰和柔光直接挂到视觉根节点；背焰使用局部着色器做连续摆动。</summary>
    private Sprite2D? AddAuraSprite(string texturePath, string nodeName, Color modulate)
    {
        Texture2D? texture = ResourceLoader.Load<Texture2D>(texturePath);
        MainFile.Logger.Info($"【大法师老爹】光效加载：节点={nodeName}，路径={texturePath}，成功={texture != null}");
        if (texture == null) return null;
        var sprite = new Sprite2D
        {
            Name = nodeName,
            Texture = texture,
            Position = new Vector2(-156.3f, -407.1f),
            Scale = Vector2.One * 0.47830188f,
            Modulate = modulate,
            ZIndex = 10,
            Centered = false,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear
        };
        if (nodeName == "GrandMageDadAuraFlame")
            sprite.Material = new ShaderMaterial { Shader = new Shader { Code = AuraFlameShaderCode } };
        _root.AddChild(sprite);
        return sprite;
    }

    /// <summary>生成缓存使用的圆形顶点，运行帧不重新分配。</summary>
    private static Vector2[] CirclePoints(float radius, int count) => Enumerable.Range(0, count)
        .Select(index => Vector2.FromAngle(index * MathF.Tau / count) * radius).ToArray();

}
