// Iluminação da v2 (spec 01 §4.5 e §9.1) numa passada de tela cheia:
//   escuridão rgba(9,7,22,dk) "furada" pelas luzes: alpha = dk × Π(1 − a_i·g(d_i / (1,6 r_i)))
//   + glows coloridos aditivos (raio 0,7 r, alpha 0,14/0,22) por cima da escuridão
//   + escurecimento extra (Lanterna perdida) e tinta de estação por cima de tudo.
// Saída em alpha pré-multiplicado: cor = soma das camadas, alpha = cobertura sobre a cena.
// light_data: coluna i = luz i. Linha 0 = (x, y, raio do furo, alpha do furo) em px da tela;
// linha 1 = (r, g, b, alpha do glow). O raio do glow é 0,7/1,6 do raio do furo.
shader_type canvas_item;
render_mode unshaded, blend_premul_alpha;

uniform sampler2D light_data : filter_nearest, repeat_disable;
uniform int light_count = 0;
uniform float darkness = 0.0;
uniform vec3 dark_color = vec3(0.0353, 0.0275, 0.0863);
uniform float extra_dark = 0.0;
uniform vec4 tint = vec4(0.0);
uniform vec4 pre_tint = vec4(0.0);
uniform vec2 view_size = vec2(640.0, 360.0);

const float GLOW_SCALE = 0.4375; // 0,7 / 1,6
const float GLOW_TEXTURE_ALPHA = 0.9;

float profile(float d) {
	if (d >= 1.0) {
		return 0.0;
	}
	return d < 0.4 ? mix(1.0, 0.55, d / 0.4) : mix(0.55, 0.0, (d - 0.4) / 0.6);
}

void fragment() {
	vec2 p = UV * view_size;
	float keep = 1.0;
	vec3 glow = vec3(0.0);
	for (int i = 0; i < light_count; i++) {
		vec4 a = texelFetch(light_data, ivec2(i, 0), 0);
		vec2 dv = p - a.xy;
		float d2 = dot(dv, dv);
		if (d2 >= a.z * a.z) {
			continue;
		}
		float d = sqrt(d2) / a.z;
		keep *= 1.0 - a.w * profile(d);
		float g = d / GLOW_SCALE;
		if (g < 1.0) {
			vec4 b = texelFetch(light_data, ivec2(i, 1), 0);
			glow += b.rgb * (b.a * GLOW_TEXTURE_ALPHA * profile(g));
		}
	}

	// camadas de baixo para cima, cada uma "over" a anterior (cor pré-multiplicada, alpha de cobertura)
	vec3 col = pre_tint.rgb * pre_tint.a;
	float cover = pre_tint.a;
	float dk = darkness * keep;
	col = dark_color * dk + col * (1.0 - dk);
	cover = dk + cover * (1.0 - dk);
	col += glow; // aditivo (globalCompositeOperation = 'lighter')
	col = dark_color * extra_dark + col * (1.0 - extra_dark);
	cover = extra_dark + cover * (1.0 - extra_dark);
	col = tint.rgb * tint.a + col * (1.0 - tint.a);
	cover = tint.a + cover * (1.0 - tint.a);
	COLOR = vec4(col, cover);
}
