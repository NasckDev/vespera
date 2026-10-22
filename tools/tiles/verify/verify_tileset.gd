## Verificacao dos tilesets no Godot (NAO headless: precisa renderizar).
## Pinta os mapas de amostra (verify/maps.json) com set_cells_terrain_connect, confere colisao x kind
## e salva capturas 4x em assets/v2/tiles/preview/.
## Uso: godot --path <raiz> --script res://tools/tiles/verify/verify_tileset.gd
extends SceneTree

const DIR := "res://assets/v2/tiles/"
const OUT := "res://assets/v2/tiles/preview/"
const TILE := 16
const SCALE := 4

var _fails := 0


class CollisionDraw extends Node2D:
	var layers: Array[TileMapLayer] = []

	func _draw() -> void:
		for layer in layers:
			var ts := layer.tile_set
			for cell in layer.get_used_cells():
				var td := layer.get_cell_tile_data(cell)
				if td == null:
					continue
				var origin := Vector2(cell * ts.tile_size) + Vector2(ts.tile_size) / 2.0
				for i in td.get_collision_polygons_count(0):
					var pts := td.get_collision_polygon_points(0, i)
					var moved := PackedVector2Array()
					for p in pts:
						moved.append(p + origin)
					draw_colored_polygon(moved, Color(0.9, 0.15, 0.25, 0.55))
					moved.append(moved[0])
					draw_polyline(moved, Color(1, 1, 1, 0.9), 0.5)


func _initialize() -> void:
	_run()


func _run() -> void:
	var maps = JSON.parse_string(FileAccess.get_file_as_string("res://tools/tiles/verify/maps.json"))
	for tileset_name in ["praca", "esgoto"]:
		await _verify(tileset_name, maps[tileset_name])
	print("RESULTADO: %s (%d falhas)" % ["OK" if _fails == 0 else "FALHOU", _fails])
	quit(1 if _fails > 0 else 0)


func _check(ok: bool, msg: String) -> void:
	if not ok:
		_fails += 1
		print("FALHA: " + msg)


func _verify(tileset_name: String, map: Dictionary) -> void:
	var ts := load(DIR + tileset_name + ".tres") as TileSet
	var layout = JSON.parse_string(FileAccess.get_file_as_string(DIR + tileset_name + "_layout.json"))
	var lookup := {}  # nome -> [source_id, coords]
	for src in layout["sources"]:
		var atlas := ts.get_source(int(src["id"])) as TileSetAtlasSource
		for t in src["tiles"]:
			var coords := Vector2i(int(t["x"]), int(t["y"]))
			lookup[t["name"]] = [int(src["id"]), coords]
			var td := atlas.get_tile_data(coords, 0)
			var polys := td.get_collision_polygons_count(0)
			var blocks: bool = t["kind"] == "wall" or t["kind"] == "water"
			_check(String(td.get_custom_data("kind")) == t["kind"], "kind errado em " + t["name"])
			_check((polys > 0) == blocks, "colisao x kind em %s (kind=%s, poligonos=%d)" % [t["name"], t["kind"], polys])

	var rows: Array = map["rows"]
	var h := rows.size()
	var w := String(rows[0]).length()
	var vp := SubViewport.new()
	vp.size = Vector2i(w * TILE, h * TILE)
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	vp.canvas_item_default_texture_filter = Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_NEAREST
	root.add_child(vp)
	var ground := TileMapLayer.new()
	ground.tile_set = ts
	vp.add_child(ground)
	var overlay := TileMapLayer.new()
	overlay.tile_set = ts
	vp.add_child(overlay)

	var terrain_of: Dictionary = map["terrain_of"]
	for terrain in map["paint_order"]:
		var cells: Array[Vector2i] = []
		for y in h:
			for x in w:
				if int(terrain_of[String(rows[y])[x]]) == int(terrain):
					cells.append(Vector2i(x, y))
		ground.set_cells_terrain_connect(cells, 0, int(terrain), false)
	for o in map["overlay"]:
		var info: Array = lookup[o[2]]
		overlay.set_cell(Vector2i(int(o[0]), int(o[1])), info[0], info[1])

	var cd := CollisionDraw.new()
	cd.layers = [ground, overlay]
	cd.visible = false
	vp.add_child(cd)

	await create_timer(0.3).timeout
	_save(vp, tileset_name + "_godot_4x")
	await create_timer(0.5).timeout  # outro quadro da animacao da agua
	_save(vp, tileset_name + "_godot_4x_t2")
	cd.visible = true
	cd.queue_redraw()
	await create_timer(0.2).timeout
	_save(vp, tileset_name + "_godot_collision_4x")
	print("%s: %d celulas no chao, %d no overlay" % [tileset_name, ground.get_used_cells().size(), overlay.get_used_cells().size()])
	vp.queue_free()


func _save(vp: SubViewport, stem: String) -> void:
	var img := vp.get_texture().get_image()
	img.resize(img.get_width() * SCALE, img.get_height() * SCALE, Image.INTERPOLATE_NEAREST)
	var err := img.save_png(ProjectSettings.globalize_path(OUT + stem + ".png"))
	_check(err == OK, "falha ao salvar " + stem)
