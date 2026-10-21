using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Combat;
using Vespera.Core.Combat.Ai;
using Vespera.Core.Common;
using Vespera.Core.Rules;
using Vespera.Core.Save;
using Vespera.Enemies;
using Vespera.Items;
using Vespera.World.Dungeon;

namespace Vespera.Combat;

/// <summary>Golpes, quebra de partes, mortes, drops e coleta (<c>combat.js:164-213, 476-530</c>).</summary>
public partial class CombatDirector
{
    private static readonly string[] SmallNumbers = BuildNumbers();

    /// <summary>
    /// <c>hitMob</c>: aplica o golpe (Core) e todo o feedback: número flutuante, Fraqueza/Resiste, partículas, quebra de
    /// parte, Essência, barra do chefe, morte. Bonecos mostram dano e "frente"/"costas" e contam para a missão.
    /// </summary>
    public bool HitMob(MonsterBody m, double damage, Vector2 source, in HitOptions o)
    {
        if (m.Dead)
        {
            return false;
        }

        var stats = State.Stats;
        var outcome = MonsterHitResolver.Apply(m, damage, source.X, source.Y, o, stats.Crit, _calc, Rng);
        var hit = outcome.Hit;
        var hx = (float)m.X;
        var hy = (float)(m.Y - (m.HitHeight * 0.6));
        if (hit.Element != ElementOutcome.None && !m.IsDummy && o.Element is { } el)
        {
            var weak = hit.Element == ElementOutcome.Weak;
            Overlay.Float(new Vector2((float)m.X, (float)(m.Y - m.HitHeight - 26)), weak ? "Fraqueza!" : "Resiste",
                weak ? CombatPalette.Of(_db.Elements[el].Color) : CombatPalette.Resist, false);
        }

        Glow.Burst(new Vector2(hx, hy), hit.Crit ? 10 : 6, hit.Crit ? CombatPalette.Crit : CombatPalette.Hit,
            new BurstOptions { V1 = hit.Crit ? 130 : 90, Gravity = 20, L1 = 0.35f });

        if (m.IsDummy)
        {
            Overlay.Float(new Vector2((float)m.X, (float)(m.Y - 34)), Number(hit.Damage), hit.Crit ? CombatPalette.Ambar : CombatPalette.Creme, hit.Crit);
            Overlay.Float(new Vector2((float)m.X, (float)(m.Y - 46)), outcome.Side == PartSideFront ? "frente" : "costas", CombatPalette.DummySide, false, 0.8f);
            State.CountQuest(QuestCounters.Dummy);
            return true;
        }

        var big = hit.Crit || hit.Mult > 1.5;
        Overlay.Float(new Vector2(hx, hy - 10), Number(hit.Damage), hit.Crit ? CombatPalette.Ambar : hit.Mult > 1.5 ? CombatPalette.Pink : CombatPalette.Creme, big);
        if (outcome.BrokenPart >= 0)
        {
            BreakPart(m, outcome.BrokenPart);
        }

        PlayerCombat.AddHitEssence();
        if (m.IsBoss)
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.BossHealthChanged, Math.Max(0, JsMath.Round(m.Hp)), JsMath.Round(m.MaxHp));
        }

        if (outcome.Killed)
        {
            KillMob(m);
        }

        return true;
    }

    private const Core.Data.PartSide PartSideFront = Core.Data.PartSide.Front;

    /// <summary><c>breakPart</c>: drops do material, "Quebrou: nome", hitstop ≥ 0,09, tremor ≥ 4, toast das partes de chefe.</summary>
    private void BreakPart(MonsterBody m, int index)
    {
        var part = m.Def.Parts[index];
        var at = new Vector2((float)m.X, (float)(m.Y - (m.HitHeight * 0.5)));
        foreach (var drop in _loot.PartDrops(part))
        {
            DropLoot(drop, at);
        }

        Overlay.Float(new Vector2((float)m.X, (float)(m.Y - m.HitHeight - 18)), $"Quebrou: {part.Name}", CombatPalette.Brecha, true);
        Sfx("brk", at);
        Clock.AtLeast(0.09);
        Shake(4);
        Fx.Burst(at, 22, CombatPalette.Break, new BurstOptions { V1 = 150, Up = 40, Gravity = 180, Size = 2.5f });
        Brecha.OnPartBroken();
        State.CountQuest(QuestCounters.Broke);
        if (part.Toast is { } toast)
        {
            Toast("Parte quebrada", toast);
        }

        EventBus.Instance.EmitSignal(EventBus.SignalName.MonsterPartBroken, m.Def.Id, part.Id, part.Name, m.IsBoss);
    }

    /// <summary><c>killMob</c>: XP, partículas, Estilhaço/Lumes/equipamento, Eco, e o fim do chefe.</summary>
    private void KillMob(MonsterBody m)
    {
        var at = new Vector2((float)m.X, (float)m.Y);
        Sfx("kill", at);
        State.GainXp(m.Def.Xp);
        Glow.Burst(at - new Vector2(0, (float)(m.HitHeight * 0.5)), m.IsBoss ? 60 : 18, CombatPalette.Kill,
            new BurstOptions { V1 = m.IsBoss ? 200 : 110, L1 = 0.9f });
        var dropAt = at - new Vector2(0, 6);
        foreach (var drop in _loot.KillDrops(m.Def, m.Invasion, Rng))
        {
            DropLoot(drop, dropAt);
        }

        if (m.IsBoss)
        {
            Shake(12);
            Clock.Set(0.3);
        }

        if (m.Def.Eco is { } ecoId)
        {
            var owned = State.Save.Ecos.Contains(ecoId);
            if (_loot.RollEco(m.Def, owned, Rng) is not null)
            {
                var eco = _db.Ecos[ecoId];
                AddPickup(Pickup.Create(PickupKind.Eco, ecoId, 1, "i_eco", PickupMotion.Drop(dropAt.X, dropAt.Y, _fxRandom), $"Absorver {eco.Name}"));
                Toast("Um Eco ficou para trás", $"{eco.Name} paira onde o monstro caiu.");
            }
        }

        EventBus.Instance.EmitSignal(EventBus.SignalName.EnemyDied, m.Def.Id, at);
        Brecha.OnMonsterKilled(m);
    }

    /// <summary><c>dropPick</c> de um <see cref="LootDrop"/> das tabelas.</summary>
    public void DropLoot(LootDrop drop, Vector2 at)
    {
        var motion = PickupMotion.Drop(at.X, at.Y, _fxRandom);
        Pickup pickup = drop.Kind switch
        {
            LootKind.Lumes => Pickup.Create(PickupKind.Lume, string.Empty, drop.Amount, "i_coin", motion, null),
            LootKind.Material => Pickup.Create(PickupKind.Material, drop.Id!, drop.Amount, _db.Materials[drop.Id!].Icon, motion, null),
            LootKind.Gear => Pickup.Create(PickupKind.Gear, drop.Id!, 1, _db.Gear[drop.Id!].Icon, motion, null),
            _ => Pickup.Create(PickupKind.Potion, string.Empty, drop.Amount, "ic_potion", motion, null),
        };
        AddPickup(pickup);
    }

    private void StepPickups(float dt)
    {
        var foot = PlayerFoot;
        var dead = PlayerCombat.Dead;
        for (var i = _pickups.Count - 1; i >= 0; i--)
        {
            var k = _pickups[i];
            k.Motion.Step(dt);
            if (!k.NeedsInteract && !dead && k.Motion.Magnet(foot.X, foot.Y, dt))
            {
                Collect(k);
                _pickups.RemoveAt(i);
                RemoveNode(k);
                continue;
            }

            k.Sync(_time);
        }
    }

    private void CollectByInteract(Pickup k)
    {
        if (k.Collected || PlayerCombat.Dead)
        {
            return;
        }

        Collect(k);
        _pickups.Remove(k);
        RemoveNode(k);
    }

    /// <summary><c>collect</c>: dentro da Brecha Lumes/materiais/equipamentos/lanternas vão para a bolsa; poções e Ecos para o save.</summary>
    private void Collect(Pickup k)
    {
        k.Collected = true;
        var bag = Brecha.Bag;
        var at = k.Position;
        switch (k.Kind)
        {
            case PickupKind.Lume:
                if (bag is not null)
                {
                    bag.Lumes += k.Amount;
                }
                else
                {
                    State.AddLumes(k.Amount);
                }

                Sfx("coin", at);
                Overlay.Float(at - new Vector2(0, 10), $"+{k.Amount}", CombatPalette.Ambar, false);
                break;
            case PickupKind.Material:
            {
                if (bag is not null)
                {
                    bag.Materials[k.Id] = bag.Materials.GetValueOrDefault(k.Id) + k.Amount;
                }
                else
                {
                    State.AddMaterial(k.Id, k.Amount);
                }

                Sfx("pick", at);
                var mat = _db.Materials[k.Id];
                Note($"+{k.Amount} {mat.Name}", mat.Icon);
                break;
            }

            case PickupKind.Gear:
            {
                if (bag is not null)
                {
                    bag.Gear.Add(k.Id);
                }
                else
                {
                    State.AddGear(k.Id);
                }

                Sfx("open", at);
                var gear = _db.Gear[k.Id];
                Note($"{gear.Name} ({_db.Rarities[gear.Rarity].Name})", gear.Icon);
                break;
            }

            case PickupKind.Potion:
                State.AddPotions(k.Amount);
                Sfx("pick", at);
                Note($"+{k.Amount} Poção", "ic_potion");
                break;
            case PickupKind.Eco:
            {
                State.AbsorbEco(k.Id);
                PlayerCombat.FillEssence();
                Sfx("eco", at);
                var eco = _db.Ecos[k.Id];
                Toast($"Eco absorvido: {eco.Name}", $"{eco.Skill} — {eco.Desc} [Q]");
                Glow.Burst(at, 40, CombatPalette.Eco, new BurstOptions { V1 = 140 });
                GameManager.Instance.SaveGame();
                break;
            }

            case PickupKind.Lantern:
                if (bag is not null)
                {
                    bag.Lanterns.Add(k.Id);
                }
                else
                {
                    State.Save.CarriedLanterns.Add(k.Id);
                }

                Sfx("open", at);
                Toast($"Lanterna de {k.Id}", "Leve-a até Odete, na Guilda. Alguém está esperando por ela.");
                break;
            case PickupKind.MyLantern:
                Brecha.Lantern.RecoverHere();
                break;
        }

        EventBus.Instance.EmitSignal(EventBus.SignalName.LootCollected, KindId(k.Kind), k.Id, k.Amount);
    }

    private static string KindId(PickupKind kind) => kind switch
    {
        PickupKind.Lume => "lume",
        PickupKind.Material => "mat",
        PickupKind.Gear => "gear",
        PickupKind.Potion => "potion",
        PickupKind.Eco => "eco",
        PickupKind.Lantern => "lantern",
        _ => "mylantern",
    };

    // --- Props ---

    /// <summary>Grupo dos objetos do mundo que aceitam <c>Hit(int dano, Vector2 origem)</c> (frente do motor).</summary>
    public static readonly StringName HittableGroup = "hittable";

    private static readonly StringName HitMethod = "Hit";
    private readonly List<Node2D> _hittables = new(64);
    private ulong _hittablesFrame = ulong.MaxValue;

    /// <summary>
    /// Objetos do grupo <see cref="HittableGroup"/> (barris, caixotes, urnas da cidade e dos interiores). Só pelo grupo e
    /// pelo método <c>Hit</c>, sem depender da classe da outra frente. A lista é refeita no máximo uma vez por frame e
    /// só quando há golpe (a busca no grupo aloca; nunca roda por frame sem ataque).
    /// </summary>
    public List<Node2D> RefreshHittables()
    {
        var frame = Engine.GetPhysicsFrames();
        if (frame == _hittablesFrame)
        {
            return _hittables;
        }

        _hittablesFrame = frame;
        _hittables.Clear();
        foreach (var node in GetTree().GetNodesInGroup(HittableGroup))
        {
            if (node is Node2D n && n.HasMethod(HitMethod))
            {
                _hittables.Add(n);
            }
        }

        return _hittables;
    }

    /// <summary>Última lista de <see cref="RefreshHittables"/> (flechas em voo usam a do disparo, sem alocar).</summary>
    public List<Node2D> CachedHittables => _hittables;

    /// <summary>Ainda vale bater no objeto (não foi liberado nem saiu do grupo ao quebrar).</summary>
    public static bool IsHittable(Node2D node) => IsInstanceValid(node) && node.IsInsideTree() && node.IsInGroup(HittableGroup);

    /// <summary>Bate no objeto do grupo: <c>Hit(dano, origem)</c>, faíscas e um hitstop curto.</summary>
    public void HitObject(Node2D node, double damage, Vector2 origin)
    {
        node.Call(HitMethod, Math.Max(1, JsMath.Round(damage)), origin);
        Fx.Burst(node.GlobalPosition - new Vector2(0, 8), 6, CombatPalette.Wood, new BurstOptions { V1 = 60, Up = 30, Gravity = 200 });
        Clock.AtLeast(0.03);
    }

    /// <summary>Props quebráveis e objetos do grupo <c>hittable</c> no setor do golpe (<c>meleeHit</c>).</summary>
    public void BreakPropsInSector(Vector2 origin, float aim, float arc, float range, double damage)
    {
        for (var i = 0; i < _props.Count; i++)
        {
            var pp = _props[i];
            if (pp.Breakable && !pp.Broken && CombatMath.PropInSector(origin.X, origin.Y, pp.Position.X, pp.Position.Y, aim, arc, range))
            {
                BreakProp(pp);
            }
        }

        var objects = RefreshHittables();
        for (var i = 0; i < objects.Count; i++)
        {
            var o = objects[i];
            if (!IsHittable(o))
            {
                continue;
            }

            var at = o.GlobalPosition;
            if (CombatMath.PropInSector(origin.X, origin.Y, at.X, at.Y, aim, arc, range))
            {
                HitObject(o, damage, origin);
            }
        }
    }

    /// <summary>Props quebráveis e objetos <c>hittable</c> a menos de <paramref name="radius"/> (orbe, ondas).</summary>
    public void BreakPropsNear(Vector2 at, float radius, double damage)
    {
        for (var i = 0; i < _props.Count; i++)
        {
            var pp = _props[i];
            if (pp.Breakable && !pp.Broken && pp.Position.DistanceTo(at) < radius)
            {
                BreakProp(pp);
            }
        }

        var objects = RefreshHittables();
        for (var i = 0; i < objects.Count; i++)
        {
            var o = objects[i];
            if (IsHittable(o) && (o.GlobalPosition - new Vector2(0, 6)).DistanceTo(at) < radius)
            {
                HitObject(o, damage, at);
            }
        }
    }

    /// <summary><c>breakProp</c>: lascas de pedra ou madeira e um sorteio de Lumes, material do tema, erva ou poção.</summary>
    private void BreakProp(DungeonProp pp)
    {
        pp.Break();
        _chase?.SetProp(pp.Position.X, pp.Position.Y, pp.SolidRadius, false);
        Sfx("brk", pp.Position);
        var theme = _db.Themes.GetValueOrDefault(pp.ThemeId);
        Fx.Burst(pp.Position - new Vector2(0, 8), 14, theme?.Stone == true ? CombatPalette.Stone : CombatPalette.Wood,
            new BurstOptions { V1 = 90, Up = 40, Gravity = 200, Size = 2 });
        if (theme is not null && _loot.PropDrop(theme, Rng) is { } drop)
        {
            DropLoot(drop, pp.Position - new Vector2(0, 4));
        }
    }

    /// <summary><c>openChest</c>.</summary>
    private void OpenChest(DungeonProp chest)
    {
        if (chest.Open || PlayerCombat.Dead)
        {
            return;
        }

        chest.OpenChest();
        Sfx("open", chest.Position);
        Shake(2);
        Glow.Burst(chest.Position - new Vector2(0, 8), 24, chest.Purple ? CombatPalette.ChestPurple : CombatPalette.Chest, new BurstOptions { V1 = 80, Up = 60 });
        var at = chest.Position - new Vector2(0, 6);
        foreach (var drop in _loot.ChestDrops(chest.Purple, Rng))
        {
            DropLoot(drop, at);
        }
    }

    // --- Ecos e Supremos que mexem nos monstros ---

    /// <summary>Corrente Votiva acertou: puxa (não-chefe) 18 u à frente do jogador e atordoa 1,3 s (chefe 0,5 s).</summary>
    public void ChainHit(MonsterBody m, double damage, Vector2 origin)
    {
        HitMob(m, damage, origin, new HitOptions(Part: 1));
        var p = PlayerFoot;
        var aim = PlayerCombat.Aim;
        if (!m.IsBoss)
        {
            m.PullX = p.X + (Math.Cos(aim) * 18);
            m.PullY = p.Y + (Math.Sin(aim) * 14);
            m.PullT = EcoNumbers.ChainPullSeconds;
            m.Stun = EcoNumbers.ChainStun;
        }
        else
        {
            m.Stun = EcoNumbers.ChainStunBoss;
        }

        Fx.Line(p - new Vector2(0, 12), new Vector2((float)m.X, (float)(m.Y - (m.HitHeight * 0.5))), 0.3f, CombatPalette.EcoText);
    }

    /// <summary>Isca Hipnótica no chão: atrai monstros a menos de <paramref name="radius"/>.</summary>
    public void LureMonsters(Vector2 at, float radius)
    {
        foreach (var e in _enemies)
        {
            var m = e.Body;
            if (!m.Dead && !m.IsDummy && CombatMath.Hypot(m.X - at.X, m.Y - at.Y) < radius)
            {
                m.Lured = (at.X, at.Y);
            }
        }
    }

    /// <summary>Isca Hipnótica explode: raio <c>50 + m.r</c>, parte 1,3, empurrão 60, atordoa 1,2 s (chefe 0,6).</summary>
    public void LureExplosion(Vector2 at, double damage)
    {
        for (var i = 0; i < _enemies.Count; i++)
        {
            var m = _enemies[i].Body;
            if (!m.Dead && m.SpawnT <= 0 && CombatMath.Hypot(m.X - at.X, m.Y - (m.HitHeight * 0.4) - at.Y) < EcoNumbers.LureExplodeRadius + m.Radius)
            {
                HitMob(m, damage, at, new HitOptions(EcoNumbers.LurePart, 0, EcoNumbers.LureKnockback));
                m.Stun = m.IsBoss ? EcoNumbers.LureStunBoss : EcoNumbers.LureStun;
            }
        }
    }

    /// <summary>Flecha da Chuva de Vésperas caiu: acerta quem estiver em <c>hypot(dx, dy·1,2) &lt; r + 12</c>.</summary>
    public void RainArrowImpact(Vector2 at, double damage)
    {
        var el = State.Stats.Element;
        for (var i = 0; i < _enemies.Count; i++)
        {
            var m = _enemies[i].Body;
            if (IsFoe(m) && CombatMath.Hypot(m.X - at.X, (m.Y - at.Y) * 1.2) < m.Radius + UltimateNumbers.BowHitRadius)
            {
                HitMob(m, damage, at - new Vector2(0, 20), new HitOptions(0.8, 0, 0, el));
            }
        }
    }

    /// <summary>Alvo de Eco/Supremo (<c>ultFoes</c>): vivo, não boneco, já nascido.</summary>
    public static bool IsFoe(MonsterBody m) => !m.Dead && !m.IsDummy && m.SpawnT <= 0;

    // --- Jogador ---

    public void MovePlayer(Vector2 delta) => World.Player.MoveAndCollide(delta);

    /// <summary>Tela rosada da isca do Abissal (<c>G.charmed</c>).</summary>
    public void Charm() => Overlay.Charm();

    /// <summary>Corrente do Ceifador pegou o jogador: puxa para 30 u do chefe em 0,3 s.</summary>
    public void PullPlayerTowards(MonsterBody boss)
    {
        var p = PlayerFoot;
        var a = Math.Atan2(p.Y - boss.Y, p.X - boss.X);
        PlayerCombat.Pull(new Vector2((float)(boss.X + (Math.Cos(a) * 30)), (float)(boss.Y + (Math.Sin(a) * 20))), 0.3f);
        Fx.Line(new Vector2((float)boss.X, (float)(boss.Y - (boss.HitHeight * 0.6))), p - new Vector2(0, 12), 0.35f, CombatPalette.EcoText);
    }

    private static string Number(int n) => n >= 0 && n < SmallNumbers.Length ? SmallNumbers[n] : n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string[] BuildNumbers()
    {
        var numbers = new string[1000];
        for (var i = 0; i < numbers.Length; i++)
        {
            numbers[i] = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return numbers;
    }
}
