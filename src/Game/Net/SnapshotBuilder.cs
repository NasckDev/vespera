using System;
using System.Collections.Generic;
using Godot;
using Vespera.Core.Net;
using Vespera.Game.Actors;
using Vespera.Game.World;

namespace Vespera.Game.Net;

/// <summary>
/// Monta o <see cref="WorldSnapshot"/> do host a partir da sala atual: caçadores (vida, fôlego, pose), monstros vivos (pose),
/// flechas em voo, NPCs, a fala aberta e se o host está pausado.
/// </summary>
public static class SnapshotBuilder
{
    /// <summary>Sala falsa enquanto o host está fora do jogo (título, telas de save): o convidado espera.</summary>
    public const string WaitingLevel = "espera";

    public static WorldSnapshot Waiting() => new((uint)Engine.GetPhysicsFrames(), WaitingLevel, [], []);

    public static WorldSnapshot Build(Level level, NetIdMap enemyIds)
    {
        var world = level.World;
        var enemies = new List<NetEnemy>();
        var alive = new List<ulong>();
        foreach (var e in world.Enemies)
        {
            if (!GodotObject.IsInstanceValid(e) || e.Dead || enemies.Count >= WorldSnapshot.MaxEnemies)
            {
                continue;
            }

            var key = e.GetInstanceId();
            alive.Add(key);
            var max = (float)Math.Max(1, e.MaxHp);
            var sprite = e.SpriteNode;
            enemies.Add(new NetEnemy(enemyIds.IdOf(key), e.GlobalPosition.X, e.GlobalPosition.Y, Math.Clamp((float)e.Hp, 0f, max), max,
                SafeId(e.MonsterId), AnimTable.IndexOf(sprite), AnimTable.FrameOf(sprite), sprite is not null && sprite.FlipH));
        }

        enemyIds.Retain(alive);
        var dialogue = level.DialogueOpen ? DialogueLine(level) : string.Empty;
        var levelId = SafeId(world.Data.Id) is { Length: > 0 } id ? id : WaitingLevel;
        var flags = level.GetTree().Paused ? WorldSnapshot.FlagHostPaused : (byte)0;
        return new WorldSnapshot((uint)Engine.GetPhysicsFrames(), levelId, Hunters(world), enemies, dialogue, Projectiles(world), Npcs(world), flags);
    }

    private static List<NetHunter> Hunters(LevelWorld world)
    {
        var hunters = new List<NetHunter>();
        foreach (var p in world.Players)
        {
            if (!GodotObject.IsInstanceValid(p) || hunters.Count >= WorldSnapshot.MaxHunters)
            {
                continue;
            }

            var max = Math.Max(1, p.Combat.MaxHp);
            var facing = p.Facing switch
            {
                Facing.Down => (byte)0,
                Facing.Up => (byte)1,
                _ => p.FacingLeft ? (byte)3 : (byte)2,
            };
            var stamina = (byte)Math.Clamp((int)Math.Round(p.Combat.StaminaFraction * 255.0), 0, 255);
            hunters.Add(new NetHunter((byte)p.Slot, p.GlobalPosition.X, p.GlobalPosition.Y, facing, p.Combat.IsDowned,
                Math.Clamp(p.Combat.Hp, 0, max), max, (byte)p.Hunter, stamina, AnimTable.IndexOf(p.Sprite), AnimTable.FrameOf(p.Sprite)));
        }

        return hunters;
    }

    private static List<NetProjectile> Projectiles(LevelWorld world)
    {
        var arrows = new List<NetProjectile>();
        foreach (var arrow in world.Projectiles)
        {
            if (GodotObject.IsInstanceValid(arrow) && arrows.Count < WorldSnapshot.MaxProjectiles)
            {
                arrows.Add(new NetProjectile(arrow.GlobalPosition.X, arrow.GlobalPosition.Y, arrow.GlobalRotation, arrow.Pierce));
            }
        }

        return arrows;
    }

    private static List<NetNpc> Npcs(LevelWorld world)
    {
        var npcs = new List<NetNpc>();
        foreach (var npc in world.Npcs)
        {
            if (!GodotObject.IsInstanceValid(npc) || npcs.Count >= WorldSnapshot.MaxNpcs || SafeId(npc.NpcId) is not { Length: > 0 } id)
            {
                continue;
            }

            var facing = npc.Facing switch
            {
                Facing.Down => (byte)0,
                Facing.Up => (byte)1,
                _ => npc.FacingLeft ? (byte)3 : (byte)2,
            };
            npcs.Add(new NetNpc(id, npc.GlobalPosition.X, npc.GlobalPosition.Y, facing, npc.IsWalking));
        }

        return npcs;
    }

    private static string DialogueLine(Level level)
    {
        var speaker = level.Dialogue.SpeakerName;
        var text = level.Dialogue.FullText;
        return speaker.Length > 0 ? $"{speaker}: {text}" : text;
    }

    /// <summary>Só ids que o protocolo aceita (<see cref="NetProtocol.IsValidId"/>); outro vira vazio.</summary>
    public static string SafeId(string id) => !string.IsNullOrEmpty(id) && NetProtocol.IsValidId(id) ? id : string.Empty;
}
