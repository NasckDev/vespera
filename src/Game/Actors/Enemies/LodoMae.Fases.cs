using System.Collections.Generic;
using System.Linq;
using Godot;
using Vespera.Core.Rift;
using Vespera.Game.Combat;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Actors.Enemies;

/// <summary>Lodo-Mãe, parte 2: investida de engolir (fase 2), Lodos esparramados e a divisão da fase 3.</summary>
public partial class LodoMae
{
    private Vector2 TickLunge(Player? target)
    {
        if (target is not null && target.Combat.HitCenter.DistanceTo(MouthPoint) < HitRadius * 0.75f && target.Combat.TrySwallow())
        {
            Go(Phase.Swallowing);
            _digestTick = 0f;
            Sprite.Play("parado");
            World.Feel.Shake(0.4f);
            World.Feel.Toast("Engolido! Aguente firme…");
            return Vector2.Zero;
        }

        if (StateTime >= LungeSeconds)
        {
            Go(Phase.Spit);
            Play("parado");
            return Vector2.Zero;
        }

        var ease = 1.3f - StateTime / LungeSeconds;
        return _laneDir * (LaneLength * A / LungeSeconds) * ease;
    }

    private Vector2 MouthPoint => HitCenter + (_laneDir * HitRadius * 0.6f);

    private Vector2 TickSwallow(float dt, Player? target)
    {
        var tuning = RiftSession.Brecha.Boss;
        var hunter = World.Player;
        hunter.Teleport(GlobalPosition + new Vector2(0, 2f));
        _digestTick += dt;
        Sprite.Scale = Vector2.One * Sheet.SpriteScale * (1f + (0.04f * Mathf.Sin(StateTime * 18f)));
        if (_digestTick >= 0.5f)
        {
            _digestTick = 0f;
            hunter.Combat.Digest((int)(tuning.SwallowDamagePerSecond * 0.5));
            Flash.Pulse(new Color(1f, 0.6f, 0.3f), 0.02f, 0.2f, 0.5f);
        }

        if (StateTime >= tuning.SwallowSeconds || hunter.Combat.IsDead)
        {
            Sprite.Scale = Vector2.One * Sheet.SpriteScale;
            var away = (_laneDir == Vector2.Zero ? Vector2.Down : _laneDir) * HitRadius * 1.4f;
            hunter.Teleport(World.ClampToFloor(GlobalPosition + away));
            hunter.Combat.Release(_laneDir * 520f * A);
            CombatSfx.Play(this, CombatSound.Split, Vector2.Zero, -4f);
            Go(Phase.Spit);
        }

        return Vector2.Zero;
    }

    private void SpawnMinions(int count)
    {
        _minions.RemoveAll(m => !IsInstanceValid(m) || m.Dead);
        var max = RiftSession.Brecha.Boss.MaxMinions;
        for (var i = 0; i < count && _minions.Count < max; i++)
        {
            var angle = (Mathf.Tau * i / count) + (GD.Randf() * 0.8f);
            var at = World.ClampToFloor(GlobalPosition + (Vector2.FromAngle(angle) * HitRadius * 1.5f));
            var lodo = new Lodo(World) { StartDelay = 0.5f + (0.3f * i), Position = at };
            World.AddEnemy(lodo);
            _minions.Add(lodo);
        }
    }

    private void BeginSplit()
    {
        _splitHandled = true;
        _marker?.Release();
        if (World.Player.Combat.State == HunterState.Swallowed)
        {
            World.Player.Combat.Release(Vector2.Down * 400f * A);
        }

        Sprite.Position = Vector2.Zero;
        Sprite.Scale = Vector2.One * Sheet.SpriteScale;
        Flash.Tint = 0f;
        var splits = RiftSession.Rules.SplitCount(CoresBroken);
        if (splits == 0)
        {
            _phase = Phase.Wait;
            World.Feel.Toast("Sem núcleos, a Lodo-Mãe não consegue se dividir!");
            _moveCount = 1;
            return;
        }

        Go(Phase.Splitting);
        Play("dividir");
        CombatSfx.Play(this, CombatSound.Split, Vector2.Zero, 3f);
        World.Feel.Toast(splits == 1 ? "A Lodo-Mãe se contorce…" : $"A Lodo-Mãe se divide em {splits}!");
    }

    private void FinishSplit()
    {
        var splits = RiftSession.Rules.SplitCount(CoresBroken);
        var share = Hp / splits;
        for (var i = 0; i < splits; i++)
        {
            var offset = new Vector2((i - ((splits - 1) / 2f)) * HitRadius * 1.1f, 0);
            var child = new Lodo(World, ChildSize) { StartDelay = 0.3f * i, Position = World.ClampToFloor(GlobalPosition + offset), DropsLoot = false };
            child.SetHealth(share);
            World.AddEnemy(child);
            _children.Add(child);
        }

        World.Combat.Unregister(this);
        foreach (var core in _cores)
        {
            World.Combat.Unregister(core);
        }

        Sprite.Visible = false;
        Bar.Conceal();
        CollisionLayer = 0;
        CollisionMask = 0;
        _phase = Phase.Split;
    }

    private void CheckChildren()
    {
        if (_children.Count == 0 || _children.Any(c => IsInstanceValid(c) && !c.Dead))
        {
            var alive = _children.Where(c => IsInstanceValid(c) && !c.Dead).ToList();
            if (alive.Count > 0)
            {
                GlobalPosition = alive[0].GlobalPosition;
            }

            return;
        }

        Hp = 0;
        Die();
    }
}
