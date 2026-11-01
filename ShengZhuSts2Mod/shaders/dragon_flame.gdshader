shader_type canvas_item;
render_mode blend_add;
// 局部火焰由平滑噪声塑形，细长白炽核心、金焰和红色外焰共用一个面片。
uniform float flow = 0.0;
uniform float strength : hint_range(0.0, 1.0) = 1.0;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
    vec2 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0)), f.x), f.y);
}
void fragment() {
    float x = UV.x;
    float turbulence = noise(vec2(x * 10.0 - flow * 3.0, UV.y * 6.0)) * 0.65 + noise(vec2(x * 23.0 - flow * 6.0, UV.y * 14.0)) * 0.35;
    float center = 0.5 + sin(x * 9.0 - flow * 9.0) * x * 0.06;
    float spread = (0.05 + x * 0.30) * pow(max(0.0, 1.0 - x), 0.35);
    float distance = abs(UV.y - center) / max(0.01, spread);
    float density = 1.0 - distance + (turbulence - 0.45) * x * 0.9;
    float alpha = smoothstep(-0.18, 0.16, density) * smoothstep(1.0, 0.78, x) * strength;
    vec3 color = mix(vec3(1.0, 0.07, 0.005), vec3(1.0, 0.55, 0.025), smoothstep(0.0, 0.55, density));
    color = mix(color, vec3(1.0, 0.95, 0.63), smoothstep(0.55, 1.1, density - x * 0.4));
    COLOR = vec4(color, alpha);
}
