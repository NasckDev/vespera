// Brilho da água (só nas formas "water" do layout): o recorte da pintura com reflexos que tremem e cintilam.
// Por px do mapa (Nearest), barato: dois senos e um hash. O brilho só aparece onde o pixel já é água (azul escuro
// ou reflexo de lanterna), então postes e cais dentro da forma não brilham.
shader_type canvas_item;

uniform float strength = 0.55;
uniform float ripple_px = 1.0;
uniform vec4 tint : source_color = vec4(1.0, 0.86, 0.62, 1.0);

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

void fragment() {
	vec2 px = floor(UV / TEXTURE_PIXEL_SIZE);
	float row = floor(px.y / 2.0);
	// tremor horizontal das linhas (reflexo na água): desloca a amostra em px inteiros
	float wob = floor(sin(row * 0.9 + TIME * 2.1) * ripple_px + 0.5);
	vec4 base = texture(TEXTURE, UV + vec2(wob * TEXTURE_PIXEL_SIZE.x, 0.0));
	float lum = dot(base.rgb, vec3(0.299, 0.587, 0.114));
	float blue = clamp((base.b - max(base.r, base.g) * 0.75) * 3.0, 0.0, 1.0);
	float warm = smoothstep(0.45, 0.8, lum) * clamp(base.r - base.b + 0.3, 0.0, 1.0);
	float is_water = max(blue, warm);
	// cintilas: aparecem e somem por célula de 3 px
	vec2 cell = floor(px / 3.0);
	float h = hash(cell);
	float twinkle = pow(max(0.0, sin(TIME * (1.2 + h * 2.0) + h * 40.0)), 24.0);
	// ondas largas de luz
	float band = smoothstep(0.82, 1.0, sin(px.x * 0.06 + px.y * 0.21 - TIME * 1.4) * sin(px.y * 0.05 + TIME * 0.7));
	float glow = (twinkle * 0.9 + band * 0.35) * is_water * strength;
	COLOR = vec4(base.rgb + tint.rgb * glow * (0.35 + lum), base.a) * vec4(1.0, 1.0, 1.0, COLOR.a);
}
