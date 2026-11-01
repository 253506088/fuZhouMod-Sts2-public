shader_type canvas_item;
// 每个角色使用独立材质实例，所有参数只影响画面。
uniform float dissolve : hint_range(0.0, 1.0) = 0.0;
uniform float stone_mix : hint_range(0.0, 1.0) = 0.0;
uniform float pulse : hint_range(0.0, 1.0) = 0.0;
uniform bool invert_order = false;
uniform vec4 energy_color : source_color = vec4(1.0, 0.48, 0.12, 1.0);

float grain(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

// 连续噪声形成细碎石蚀边缘，避免方块状的像素马赛克。
float erosion(vec2 p) {
    vec2 cell = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(grain(cell), grain(cell + vec2(1.0, 0.0)), f.x), mix(grain(cell + vec2(0.0, 1.0)), grain(cell + vec2(1.0)), f.x), f.y);
}

void fragment() {
    vec4 base = COLOR;
    float field = UV.y * 0.80 + (erosion(UV * 18.0) * 0.7 + erosion(UV * 51.0) * 0.3) * 0.20;
    if (invert_order) field = 1.0 - field;
    float coverage = dissolve < 0.001 ? 1.0 : (dissolve > 0.999 ? 0.0 : smoothstep(dissolve - 0.04, dissolve + 0.04, field));
    float edge = (dissolve > 0.001 && dissolve < 0.999) ? (1.0 - smoothstep(0.0, 0.025, abs(field - dissolve))) : 0.0;
    float grey = dot(base.rgb, vec3(0.299, 0.587, 0.114));
    vec3 stone = grey * vec3(0.85, 0.79, 0.58);
    vec3 tint = mix(base.rgb, stone, stone_mix);
    float red_eye = smoothstep(0.22, 0.52, base.r - max(base.g, base.b));
    COLOR = vec4(tint + energy_color.rgb * (edge * 0.85 + pulse * red_eye * 0.75), base.a * coverage);
}
