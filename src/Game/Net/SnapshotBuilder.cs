using System;
using System.Collections.Generic;
using Godot;
using Vespera.Core.Net;
using Vespera.Game.Actors;
using Vespera.Game.World;

namespace Vespera.Game.Net;

/// <summary>Monta o <see cref="WorldSnapshot"/> do host a partir da sala atual (caçadores, monstros vivos e a fala aberta).</summary>
public static class SnapshotBuilder
{
    /// <summary>Sala falsa enquanto o host está fora do jogo (título, telas de save): o convidado espera.</summary>
    public const string WaitingLevel = "espera";

    public static WorldSnapshot Waiting() => new((uint)Engine.GetPhysicsFrames(), WaitingLevel, [], []);

    public static WorldSnapshot Build(Level level, NetIdMap enemyIds)
    {
        var world = level.World;
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
            hunters.Add(new NetHunter((byte)p.Slot, p.GlobalPosition.X, p.GlobalPosition.Y, facing, p.Combat.IsDowned,
                Math.Clamp(p.Combat.Hp, 0, max), max, (byte)p.Hunter));
        }

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
            enemies.Add(new NetEnemy(enemyIds.IdOf(key), e.GlobalPosition.X, e.GlobalPosition.Y, Math.Clamp((float)e.Hp, 0f, max), max,
                SafeId(e.MonsterId)));
        }

        enemyIds.Retain(alive);
        var dialogue = level.DialogueOpen ? DialogueLine(level) : string.Empty;
        return new WorldSnapshot((uint)Engine.GetPhysicsFrames(), SafeId(world.Data.Id) is { Length: > 0 } id ? id : WaitingLevel, hunters, enemies, dialogue);
    }

    private static string DialogueLine(Level level)
    {
        var speaker = level.Dialogue.SpeakerName;
        var text = level.Dialogue.FullText;
        return speaker.Length > 0 ? $"{speaker}: {text}" : text;
    }

    /// <summary>Só ids que o protocolo aceita (letras, dígitos, _ - / .); outro vira vazio.</summary>
    private static string SafeId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > NetProtocol.MaxIdBytes)
        {
            return string.Empty;
        }

        foreach (var c in id)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '/' or '.'))
            {
                return string.Empty;
            }
        }

        return id;
    }
}
