## Monta os TileSets (.tres) a partir dos layouts JSON gerados por gen_tiles.py.
## Uso: godot --headless --path <raiz> --script res://tools/tiles/build_tilesets.gd
## (rode `godot --headless --path <raiz> --import` antes se os PNGs forem novos.)
extends SceneTree

const DIR := "res://assets/v2/tiles/"
const CORNER_BITS := [
	TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER,
	TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER,
	TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER,
	TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER,
]


func _initialize() -> void:
	var failed := false
	for tileset_name in ["praca", "esgoto"]:
		if not _build(tileset_name):
			failed = true
	quit(1 if failed else 0)


func _build(tileset_name: String) -> bool:
	var layout = JSON.parse_string(FileAccess.get_file_as_string(DIR + tileset_name + "_layout.json"))
	if layout == null:
		push_error("layout invalido: " + tileset_name)
		return false
	var size := int(layout["tile_size"])
	var ts := TileSet.new()
	ts.tile_size = Vector2i(size, size)

	# camada de fisica 0 (paredes/agua), dado customizado "kind", conjunto de terrenos por cantos
	ts.add_physics_layer()
	ts.set_physics_layer_collision_layer(0, 1)
	ts.set_physics_layer_collision_mask(0, 0)
	ts.add_custom_data_layer()
	ts.set_custom_data_layer_name(0, "kind")
	ts.set_custom_data_layer_type(0, TYPE_STRING)
	ts.add_terrain_set()
	ts.set_terrain_set_mode(0, TileSet.TERRAIN_MODE_MATCH_CORNERS)
	var terrains: Array = layout["terrains"]
	for i in terrains.size():
		ts.add_terrain(0)
		ts.set_terrain_name(0, i, terrains[i]["name"])
		ts.set_terrain_color(0, i, Color(terrains[i]["color"]))

	for src in layout["sources"]:
		var atlas := TileSetAtlasSource.new()
		atlas.texture = load(src["png"]) as Texture2D
		if atlas.texture == null:
			push_error("textura nao importada: " + src["png"])
			return false
		atlas.texture_region_size = Vector2i(size, size)
		var frames := int(src.get("frames", 1))
		ts.add_source(atlas, int(src["id"]))
		for t in src["tiles"]:
			var coords := Vector2i(int(t["x"]), int(t["y"]))
			atlas.create_tile(coords)
			if src["animated"]:
				atlas.set_tile_animation_columns(coords, frames)
				atlas.set_tile_animation_frames_count(coords, frames)
				for f in frames:
					atlas.set_tile_animation_frame_duration(coords, f, float(src["frame_seconds"]))
			_configure_tile(atlas.get_tile_data(coords, 0), t)

	var path := DIR + tileset_name + ".tres"
	var err := ResourceSaver.save(ts, path)
	print("%s -> %s (erro=%d)" % [tileset_name, path, err])
	return err == OK


func _configure_tile(td: TileData, t: Dictionary) -> void:
	td.set_custom_data("kind", t["kind"])
	td.probability = float(t["probability"])
	for poly in t["collision"]:
		var idx := td.get_collision_polygons_count(0)
		td.add_collision_polygon(0)
		var pts := PackedVector2Array()
		for p in poly:
			pts.append(Vector2(p[0], p[1]))
		td.set_collision_polygon_points(0, idx, pts)
	if int(t["terrain"]) >= 0:
		td.terrain_set = 0
		td.terrain = int(t["terrain"])
		var corners = t["corners"]
		if corners != null:
			for i in 4:
				td.set_terrain_peering_bit(CORNER_BITS[i], int(corners[i]))
