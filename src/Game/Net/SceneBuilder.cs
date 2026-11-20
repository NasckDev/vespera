using System.Collections.Generic;
using Godot;
using Vespera.Core.Net;
using Vespera.Game.World;

namespace Vespera.Game.Net;

/// <summary>Monta o <see cref="SceneSnapshot"/> do host: variante do chão e os objetos que existem na sala agora.</summary>
public static class SceneBuilder
{
    public static SceneSnapshot? Build(Level level)
    {
        var world = level.World;
        if (world is null || !GodotObject.IsInstanceValid(world) || SnapshotBuilder.SafeId(world.Data.Id) is not { Length: > 0 } id)
        {
            return null;
        }

        var entries = new List<SceneEntry>();
        foreach (var prop in world.Props)
        {
            if (!GodotObject.IsInstanceValid(prop) || SnapshotBuilder.SafeId(prop.Type) is not { Length: > 0 } type || entries.Count >= SceneSnapshot.MaxEntries)
            {
                continue;
            }

            entries.Add(new SceneEntry(type, prop.GlobalPosition.X, prop.GlobalPosition.Y, prop.Flip));
        }

        foreach (var breakable in world.Breakables)
        {
            if (!GodotObject.IsInstanceValid(breakable) || breakable.Broken || SnapshotBuilder.SafeId(breakable.Type) is not { Length: > 0 } type
                || entries.Count >= SceneSnapshot.MaxEntries)
            {
                continue;
            }

            entries.Add(new SceneEntry(type, breakable.GlobalPosition.X, breakable.GlobalPosition.Y, false));
        }

        return new SceneSnapshot(id, (byte)(world.GroundVariantIndex() + 1), entries);
    }
}
