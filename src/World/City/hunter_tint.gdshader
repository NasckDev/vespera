// Caçadores ambiente da v2 (render.js:175): filter 'saturate(.35) brightness(.78)', alpha .92.
shader_type canvas_item;

void fragment() {
	vec4 c = texture(TEXTURE, UV) * COLOR;
	float luma = dot(c.rgb, vec3(0.2126, 0.7152, 0.0722));
	COLOR = vec4(mix(vec3(luma), c.rgb, 0.35) * 0.78, c.a * 0.92);
}
