using System;
using System.Collections.Generic;
using Godot;
using Vespera.Combat;
using Vespera.Core.Combat.Ai;
using Vespera.World;

namespace Vespera.Enemies;

/// <summary>
/// Monstro no mundo: só a apresentação de um <see cref="MonsterBody"/> (estado puro do Core). O
/// <see cref="CombatDirector"/> roda a IA e chama <see cref="Sync"/> a cada frame; este nó não tem <c>_Process</c>.
/// Sprite <c>{spr}_{f|a|b|d|s}</c> como <c>mobSpr</c> (<c>render.js:11-18</c>), espelho pela face, bob, fade de nascimento e
/// de morte, flash branco ao ser atingido e pulso branco enquanto mira um golpe forte (<c>render.js:145-156</c>).
/// Fica em <see cref="World.World.Entities"/> (y-sort pelo pé) e é liberado na troca de mapa.
/// </summary>
public partial class Enemy : Node2D
{
    private static readonly Dictionary<string, SpriteKeys> KeysCache = [];

    private AtlasSprite _shadow = null!;
    private Node2D _body = null!;
    private AtlasSprite _sprite = null!;
    private ShaderMaterial _flash = null!;
    private SpriteKeys _keys;
    private float _lastFlash = -1;

    public MonsterBody Body { get; private set; } = null!;

    /// <summary>Monstro de invasão (conta para resolver a ruptura).</summary>
    public bool Invasion => Body.Invasion;

    /// <summary>Cria o nó para o corpo (chame antes de adicionar à árvore).</summary>
    public static Enemy Create(MonsterBody body)
    {
        var enemy = new Enemy { Name = body.Def.Id, Body = body };
        enemy._keys = body.IsDummy ? default : KeysFor(body.Def.Sprite);
        enemy._shadow = new AtlasSprite { Key = "shadow", SpriteScale = (float)(body.Radius / 8), Position = new Vector2(0, 1) };
        enemy._body = new Node2D();
        enemy._flash = FlashMaterial.Create();
        enemy._sprite = new AtlasSprite { Key = enemy._keys.Front ?? string.Empty, SpriteScale = (float)body.Def.Scale, Material = enemy._flash };
        enemy._body.AddChild(enemy._sprite);
        enemy.AddChild(enemy._shadow);
        enemy.AddChild(enemy._body);
        enemy.Position = new Vector2((float)body.X, (float)body.Y);
        if (body.IsDummy)
        {
            enemy.Visible = false; // os bonecos fazem parte da pintura da Guilda (render.js:36)
        }

        return enemy;
    }

    /// <summary>
    /// Carrega antes todos os recortes do monstro (<c>_f/_a/_b/_d/_s</c>). Os PNG soltos são lidos e recortados na
    /// primeira vez; fazer isso no meio do combate trava o frame (chame atrás do fade, ao entrar na Brecha).
    /// </summary>
    public static void Preload(Vespera.Core.Data.MonsterDef def)
    {
        var keys = KeysFor(def.Sprite);
        AtlasLibrary.TryGet(keys.Front, out _);
        AtlasLibrary.TryGet(keys.Attack, out _);
        AtlasLibrary.TryGet(keys.Dead, out _);
        if (keys.Back is { } back)
        {
            AtlasLibrary.TryGet(back, out _);
        }

        if (keys.Special is { } special)
        {
            AtlasLibrary.TryGet(special, out _);
        }
    }

    /// <summary>Altura do centro de acerto (<c>hh</c>): o dado, ou 80% da altura do sprite de frente × escala, ou 20.</summary>
    public static double HitHeightFor(Vespera.Core.Data.MonsterDef def)
    {
        if (def.HitHeight is { } hh)
        {
            return hh;
        }

        if (AtlasLibrary.TryGet(KeysFor(def.Sprite).Front, out var frame))
        {
            var h = Math.Round(frame.Texture.GetHeight() / frame.Density * def.Scale * 0.8);
            if (h > 0)
            {
                return h;
            }
        }

        return 20;
    }

    /// <summary>Aplica o estado do corpo ao visual. <paramref name="time"/> = relógio de jogo (pulsos).</summary>
    public void Sync(float time, Vector2 player)
    {
        var m = Body;
        Position = new Vector2((float)m.X, (float)m.Y);
        if (m.IsDummy)
        {
            return;
        }

        string key;
        if (m.Dead)
        {
            key = _keys.Dead;
        }
        else if (m.IsSpecial && _keys.Special is { } special && m.T > 0.1)
        {
            key = special;
        }
        else if (m.IsAttacking)
        {
            key = _keys.Attack;
        }
        else if (player.Y < m.Y - 14 && m.State != MonsterState.Idle && _keys.Back is { } back)
        {
            key = back;
        }
        else
        {
            key = _keys.Front;
        }

        _sprite.Key = key;

        var a = 1.0;
        if (m.Dead)
        {
            a = Math.Clamp(1 - ((m.DeadT - (m.IsBoss ? 2.4 : 0.9)) / 0.6), 0, 1);
        }

        if (m.SpawnT > 0)
        {
            a = 1 - (m.SpawnT / MonsterBody.SpawnSeconds);
        }

        a *= m.Alpha;
        Visible = a > 0;
        if (!Visible)
        {
            return;
        }

        var bob = m.Fly && !m.Dead
            ? (Math.Sin(m.Bob * 3) * 2) - 3
            : (m.State == MonsterState.Chase ? -Math.Abs(Math.Sin(m.Bob * 9)) : 0);
        var shakeX = m.State == MonsterState.Wind || (m.State == MonsterState.Slam && m.T < 0.75) ? (float)((Math.Sin(time * 97) * 0.7)) : 0f;
        _body.Position = new Vector2(shakeX, (float)bob);
        _body.Scale = new Vector2(m.Face < 0 ? -1 : 1, 1);
        _sprite.Modulate = new Color(1, 1, 1, (float)a);
        _shadow.Modulate = new Color(1, 1, 1, (float)(0.45 * a));

        // Flash do golpe (0,12 s, alfa ×6) e brilho pulsante enquanto mira um golpe forte.
        var flash = (float)Math.Min(1, m.Flash * 6);
        if (m.IsTelegraphing && !m.Dead)
        {
            flash = MathF.Max(flash, 0.18f + (0.15f * MathF.Sin(time * 30)));
        }

        if (!Mathf.IsEqualApprox(flash, _lastFlash))
        {
            _lastFlash = flash;
            _flash.SetShaderParameter(FlashMaterial.FlashParam, flash);
        }
    }

    private static SpriteKeys KeysFor(string sprite)
    {
        if (KeysCache.TryGetValue(sprite, out var keys))
        {
            return keys;
        }

        string front = $"{sprite}_f";
        keys = new SpriteKeys(
            front,
            Exists($"{sprite}_a") ? $"{sprite}_a" : front,
            Exists($"{sprite}_b") ? $"{sprite}_b" : null,
            Exists($"{sprite}_d") ? $"{sprite}_d" : front,
            Exists($"{sprite}_s") ? $"{sprite}_s" : null);
        KeysCache[sprite] = keys;
        return keys;
    }

    /// <summary>Recorte existe no atlas ou como PNG solto (sem o aviso do <see cref="AtlasLibrary"/>).</summary>
    private static bool Exists(string key) =>
        AtlasLibrary.Index.TryGet(key, out _) || ResourceLoader.Exists($"res://assets/sprites/enemies/{key}.png");

    private readonly record struct SpriteKeys(string Front, string Attack, string? Back, string Dead, string? Special);
}
