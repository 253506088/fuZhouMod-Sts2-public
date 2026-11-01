using Godot;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

/// <summary>圣主封印光纹、灰烬、爪痕与火焰；节点只创建一次，不参与战斗结算。</summary>
internal sealed class ShengZhuVisualEffects
{
    private readonly Node2D _root = new() { Name = "ShengZhuEffects" };
    private readonly Node2D _seal = new() { Position = new(0, -154) };
    private readonly Node2D _burst = new();
    private readonly Node2D _trails = new();
    private readonly Node2D _rays = new();
    private readonly Node2D _fire = new();
    private readonly Node2D _ward = new() { Position = new(0, -150) };
    private readonly List<Sprite2D> _embers = [];
    private readonly List<Line2D> _beams = [];
    private readonly List<Sprite2D> _eyes = [];
    private readonly ShaderMaterial _flameMaterial;
    private readonly Node2D _head;
    private static readonly Color Gold = new(1f, .63f, .20f);
    private static readonly Color Red = new(1f, .15f, .035f);

    /// <summary>构建低开销抗锯齿几何与共享柔光纹理，避免帧内不断创建特效节点。</summary>
    public ShengZhuVisualEffects(Node2D parent, Node2D head)
    {
        _head = head;
        parent.AddChild(_root);
        _root.AddChild(_seal);
        _root.AddChild(_burst);
        _root.AddChild(_trails);
        _root.AddChild(_rays);
        _root.AddChild(_fire);
        _root.AddChild(_ward);
        var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        _root.Material = additive;
        Ring(_seal, 166, Gold, 1.1f);
        Ring(_seal, 172, new Color(1, .42f, .1f, .3f), 3);
        for (int i = 0; i < 12; i++)
        {
            float angle = i * MathF.Tau / 12;
            Vector2 radial = Vector2.FromAngle(angle), tangent = radial.Orthogonal();
            Vector2 center = radial * 159;
            Line(_seal, [center - tangent * 3, center + radial * 5, center + tangent * 3, center - radial * 4, center - tangent * 3], Gold, 1.1f);
        }
        Ring(_burst, 110, Gold, 2.5f);
        _burst.Scale = new Vector2(1, .22f);
        Ring(_ward, 132, new Color(.4f, .8f, 1f), 2);
        Ring(_ward, 139, new Color(.4f, .8f, 1f, .4f), 5);
        for (int i = 0; i < 3; i++)
        {
            Vector2[] arc = Enumerable.Range(0, 20).Select(j =>
            {
                float angle = -.9f + j * .075f;
                return new Vector2(115 + MathF.Cos(angle) * (58 + i * 10), -170 + MathF.Sin(angle) * 90 + i * 12);
            }).ToArray();
            Line(_trails, arc, new Color(1, .35f, .1f, .2f), 12);
            Line(_trails, arc, new Color(1, .9f, .55f), 2);
        }
        for (int i = 0; i < 4; i++) _beams.Add(Line(_rays, [Vector2.Zero, Vector2.One], i % 2 == 0 ? new Color(1, .1f, .02f, .35f) : new Color(1, .8f, .5f), i % 2 == 0 ? 11 : 2));
        _flameMaterial = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>("res://ShengZhuSts2Mod/shaders/dragon_flame.gdshader") };
        _fire.AddChild(new Polygon2D
        {
            Polygon = [new(0, -55), new(210, -55), new(210, 55), new(0, 55)],
            UV = [Vector2.Zero, Vector2.Right, Vector2.One, Vector2.Down],
            // Polygon2D 没有纹理时不上传 UV；提供 1×1 纹理让火焰着色器收到完整坐标。
            Texture = new GradientTexture2D { Width = 1, Height = 1 },
            Material = _flameMaterial
        });
        var gradient = new Gradient { Colors = [new Color(1, 1, 1, 1), new Color(1, 1, 1, 0)], Offsets = [0, 1] };
        var glow = new GradientTexture2D { Width = 32, Height = 32, Gradient = gradient, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new(.5f, .5f), FillTo = new(1, .5f) };
        for (int i = 0; i < 2; i++)
        {
            var eye = new Sprite2D { Texture = glow, Material = additive, Scale = new Vector2(.8f, .55f) };
            _root.AddChild(eye);
            _eyes.Add(eye);
        }
        for (int i = 0; i < 28; i++)
        {
            var sprite = new Sprite2D { Texture = glow, Material = additive, Modulate = Gold, Scale = Vector2.One * (.08f + i % 4 * .035f) };
            _root.AddChild(sprite);
            _embers.Add(sprite);
        }
    }

    /// <summary>依据当前视觉时间更新特效，固定序列取代战斗随机源；不产生新任务或 Tween。</summary>
    public void Update(float time, float human, float transition, string pose, float phase, float death)
    {
        float envelope = MathF.Sin(MathF.PI * Math.Clamp(phase, 0, 1));
        float alive = 1 - death;
        _root.Modulate = new Color(1, 1, 1, alive);
        _seal.Rotation = time * .025f;
        _seal.Modulate = new Color(1, 1, 1, (1 - human) * (.10f + .035f * MathF.Sin(time * 1.5f)) + transition * .65f);
        _burst.Scale = new Vector2(1 + transition * .8f, .22f + transition * .15f);
        _burst.Modulate = new Color(1, 1, 1, transition * .7f);
        _ward.Modulate = new Color(1, 1, 1, pose is "defend" or "buff" ? envelope * .6f : 0);
        _ward.Rotation = -time * .2f;
        _trails.Modulate = new Color(1, 1, 1, pose is "attack" or "heavy" ? MathF.Pow(envelope, 4) * human : 0);
        _trails.Scale = Vector2.One * (pose == "heavy" ? 1.12f : 1);
        _rays.Visible = pose == "laser" && phase > .22f && phase < .78f;
        _fire.Visible = pose == "flame" && phase > .18f && phase < .84f;
        for (int i = 0; i < _eyes.Count; i++)
        {
            _eyes[i].Position = human > .5f ? _root.ToLocal(_head.ToGlobal(new Vector2(13 + i * 15, -18 - i * 3))) : new Vector2(-5 + i * 24, -134 - i * 3);
            _eyes[i].Modulate = new Color(Red, .16f + MathF.Sin(time * 2.2f) * .04f + (pose == "laser" ? envelope * .65f : 0));
        }
        if (_rays.Visible || _fire.Visible)
        {
            // 眼睛与口部坐标跟随头骨，镜像和角色槽位缩放由场景坐标系统处理。
            Vector2 origin = _root.ToLocal(_head.ToGlobal(new Vector2(13, -18)));
            if (human < .5f) origin = new Vector2(6, -133);
            _rays.Modulate = new Color(1, 1, 1, envelope);
            for (int i = 0; i < _beams.Count; i++)
            {
                Vector2 eye = origin + new Vector2(i / 2 * 15, -i / 2 * 3);
                _beams[i].SetPointPosition(0, eye);
                _beams[i].SetPointPosition(1, eye + new Vector2(210 * envelope, 12));
            }
            _fire.Position = human > .5f ? _root.ToLocal(_head.ToGlobal(new Vector2(40, -2))) : new Vector2(15, -98);
            _fire.Rotation = human > .5f ? _head.GlobalRotation - _root.GlobalRotation : 0;
            _fire.Scale = new Vector2(envelope, .6f + envelope * .4f);
            _flameMaterial.SetShaderParameter("flow", time);
            _flameMaterial.SetShaderParameter("strength", envelope);
        }
        for (int i = 0; i < _embers.Count; i++)
        {
            float cycle = (time * (.16f + i % 3 * .02f) + i * .618034f) % 1;
            float angle = i * 2.39996f + time * .07f;
            _embers[i].Position = new Vector2(MathF.Cos(angle) * (92 + cycle * 60), -cycle * 290);
            _embers[i].Modulate = new Color(Gold, MathF.Sin(cycle * MathF.PI) * (.12f * (1 - human) + transition * .85f + (pose is "buff" or "cast" ? envelope * .6f : 0)));
        }
    }

    /// <summary>创建可复用的圆形封印线条。</summary>
    private static void Ring(Node parent, float radius, Color color, float width)
        => Line(parent, Enumerable.Range(0, 97).Select(i => Vector2.FromAngle(i * MathF.Tau / 96) * radius).ToArray(), color, width);

    /// <summary>创建抗锯齿线条，禁止接收输入。</summary>
    private static Line2D Line(Node parent, Vector2[] points, Color color, float width)
    {
        var line = new Line2D { Points = points, DefaultColor = color, Width = width, Antialiased = true, Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } };
        parent.AddChild(line);
        return line;
    }
}
