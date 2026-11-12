using System.Collections.Generic;
using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Coop;
using Vespera.Game.World.Rift;

namespace Vespera.Game.World;

/// <summary>Os caçadores da sala (co-op local): nascer em grupo, entrar e sair no meio, alvo dos monstros e a coleira.</summary>
public partial class LevelWorld
{
    /// <summary>J1 sozinho (teclado + controles) ou, no co-op local, um caçador por jogador, o J2 ao lado do J1.</summary>
    private void SpawnParty()
    {
        if (!CoopSession.Active)
        {
            Player = SpawnPlayer(MergedInput.LocalSolo(), SpawnPoint, RiftSession.Hunter);
            _players.Add(Player);
            return;
        }

        foreach (var member in CoopSession.Members)
        {
            AddPartyMember(member);
        }
    }

    /// <summary>Põe um jogador do co-op na sala (J1 no ponto de chegada; os outros ao lado dele) com contorno e etiqueta.</summary>
    public Player AddPartyMember(CoopMember member)
    {
        var feet = member.Slot == 0 || _players.Count == 0 ? SpawnPoint : BesidePoint(_players[0].GlobalPosition, member.Slot);
        // O J1 usa a arma do save (a equipada, ou a do suporte nesta caçada), como no jogo solo.
        var hunter = member.Slot == 0 ? RiftSession.Hunter : CoopSession.HunterOf(member);
        var player = SpawnPlayer(CoopSession.CreateInput(member), feet, hunter, member.Slot);
        player.Name = $"Player_{CoopSession.TagOf(member.Slot)}";
        if (member.Slot == 0)
        {
            Player = player;
        }

        _players.Add(player);
        _players.Sort((a, b) => a.Slot.CompareTo(b.Slot));

        // O caçador já entrou na árvore (e passou pelo _Ready) no AddChild: o contorno pode ir agora.
        player.ApplyIdentity(CoopSession.ColorOf(member.Slot), member.CapeTint);
        PlayerAdded?.Invoke(player);
        return player;
    }

    /// <summary>Tira um caçador do co-op da sala (J2 saiu pelo menu de pausa).</summary>
    public void RemovePartyMember(Player player)
    {
        if (player == Player || !_players.Remove(player))
        {
            return;
        }

        player.QueueFree();
    }

    /// <summary>Ponto ao lado de <paramref name="anchor"/> para o jogador <paramref name="slot"/> (fora de parede).</summary>
    public Vector2 BesidePoint(Vector2 anchor, int slot)
    {
        var side = slot % 2 == 1 ? 1f : -1f;
        return ClampToFloor(anchor + new Vector2(side * 0.85f * _data.HumanHeight, 0.15f * _data.HumanHeight));
    }

    public Player? PlayerAt(int slot)
    {
        foreach (var p in _players)
        {
            if (p.Slot == slot && IsInstanceValid(p))
            {
                return p;
            }
        }

        return null;
    }

    /// <summary>Caçador de pé mais próximo (alvo dos monstros); null se todos caíram.</summary>
    public Player? NearestHunter(Vector2 from)
    {
        Player? best = null;
        var bestDistance = float.MaxValue;
        foreach (var p in _players)
        {
            if (!IsInstanceValid(p) || p.Combat.IsDead)
            {
                continue;
            }

            var d = p.GlobalPosition.DistanceSquaredTo(from);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = p;
            }
        }

        return best;
    }

    /// <summary>
    /// Coleira do co-op: a velocidade do caçador não deixa o grupo abrir além do que cabe na tela no piso do zoom
    /// (caçador ≥ 85 px). Borda invisível relativa aos outros (ninguém arrasta ninguém) e puxão suave se já estiver fora.
    /// </summary>
    public Vector2 ConstrainToParty(Player player, Vector2 velocity, float dt)
    {
        var others = new List<System.Numerics.Vector2>(_players.Count);
        foreach (var p in _players)
        {
            if (p != player && IsInstanceValid(p))
            {
                others.Add(new System.Numerics.Vector2(p.GlobalPosition.X, p.GlobalPosition.Y));
            }
        }

        var span = Camera.PartySpan;
        var v = Vespera.Core.Coop.CoopFraming.Constrain(new System.Numerics.Vector2(player.GlobalPosition.X, player.GlobalPosition.Y),
            new System.Numerics.Vector2(velocity.X, velocity.Y), others, new System.Numerics.Vector2(span.X, span.Y),
            (float)CoopSession.Rules.Camera.TetherPull, dt, TetherMaxPull * player.MaxSpeed);
        return new Vector2(v.X, v.Y);
    }
}
