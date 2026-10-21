// Balanço de oclusor (árvore, bandeira, toldo): desloca a amostra da textura, não os vértices, então o recorte
// continua cobrindo a arte de baixo e nunca aparece "duas imagens". A base (baseY, y local 0) fica parada; o topo
// balança até amp_px px do mapa. Amostragem Nearest pelo filtro do nó: o deslocamento anda em px inteiros.
shader_type canvas_item;

uniform float height = 64.0;       // altura do recorte em u (base em y local 0, topo em -height)
uniform float amp_px = 1.5;        // deslocamento máximo no topo, em px do mapa
uniform float speed = 1.3;
uniform float phase = 0.0;

varying float v_lift;

void vertex() {
	v_lift = clamp(-VERTEX.y / max(height, 1.0), 0.0, 1.0);
}

void fragment() {
	float t = TIME * speed + phase;
	float wave = sin(t) * 0.75 + sin(t * 2.3 + 1.7) * 0.25;
	float shift = floor(wave * amp_px * v_lift * v_lift + 0.5);
	vec2 uv = UV + vec2(shift * TEXTURE_PIXEL_SIZE.x, 0.0);
	vec4 c = texture(TEXTURE, uv);
	COLOR = vec4(c.rgb, c.a * COLOR.a); // COLOR.a = modulate (fade quando cobre o jogador)
}
