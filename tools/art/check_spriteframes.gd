# Uso: godot --headless --path . --script tools/art/check_spriteframes.gd
# Carrega cada SpriteFrames de assets/v2/sprites/** e imprime animações, quadros e se a textura do atlas existe.
extends SceneTree


func _init() -> void:
	var failures := 0
	for path in _find_tres("res://assets/v2/sprites"):
		var frames := load(path) as SpriteFrames
		if frames == null:
			print("FALHA ao carregar ", path)
			failures += 1
			continue
		var names := frames.get_animation_names()
		var total := 0
		for anim_name in names:
			total += frames.get_frame_count(anim_name)
			if frames.get_frame_texture(anim_name, 0) == null:
				print("FALHA textura nula ", path, " ", anim_name)
				failures += 1
		print("OK ", path, " animacoes=", names.size(), " quadros=", total)
		for anim_name in names:
			print("   ", anim_name, " n=", frames.get_frame_count(anim_name), " fps=", frames.get_animation_speed(anim_name), " loop=", frames.get_animation_loop(anim_name))
	print("FALHAS=", failures)
	quit(1 if failures > 0 else 0)


func _find_tres(dir_path: String) -> PackedStringArray:
	var result := PackedStringArray()
	var dir := DirAccess.open(dir_path)
	if dir == null:
		return result
	for sub in dir.get_directories():
		result.append_array(_find_tres(dir_path + "/" + sub))
	for file in dir.get_files():
		if file.ends_with(".tres"):
			result.append(dir_path + "/" + file)
	return result
