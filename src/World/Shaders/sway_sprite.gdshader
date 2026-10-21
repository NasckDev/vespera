// Balanço de objeto do catálogo (planta, bandeira, toldo): inclina os vértices do sprite em volta do pé (y local 0),
// mais forte no topo. É um sprite próprio (não um recorte da pintura), então mexer a geometria não mostra "duas imagens".
// Também recebe o fade de oclusão pelo modulate (COLOR.a) como qualquer sprite.
shader_type canvas_item;

uniform float height = 64.0;   // altura do sprite em px locais (pé em y 0, topo em -height)
uniform float amp = 2.0;       // deslocamento máximo no topo, em px locais
uniform float speed = 1.3;
uniform float phase = 0.0;

void vertex() {
	float lift = clamp(-VERTEX.y / max(height, 1.0), 0.0, 1.0);
	float t = TIME * speed + phase;
	float wave = sin(t) * 0.75 + sin(t * 2.3 + 1.7) * 0.25;
	VERTEX.x += wave * amp * lift * lift;
}
