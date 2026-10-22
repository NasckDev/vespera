using System.Collections.Generic;
using Godot;

namespace Vespera.Combat;

/// <summary>Cores do combate da v2 (<c>C.*</c> e literais de <c>combat.js</c>), parseadas uma vez.</summary>
public static class CombatPalette
{
    public static readonly Color Creme = new("#F4E7C6");
    public static readonly Color Ambar = new("#F2A541");
    public static readonly Color Brecha = new("#E64A8E");
    public static readonly Color Roxo = new("#4A2C6B");
    public static readonly Color Pink = new("#ff8ab8");
    public static readonly Color HurtText = new("#ff5d72");
    public static readonly Color HealText = new("#7cf0a0");
    public static readonly Color EcoText = new("#d99cff");
    public static readonly Color Resist = new("#9d93a8");
    public static readonly Color DummySide = new("#b8a9c4");
    public static readonly Color HpBarTop = new("#e2475e");
    public static readonly Color HpBarBottom = new("#8f1f3a");
    public static readonly Color PartBroken = new("#4a3d52");
    public static readonly Color Cyan = new("#5fe0d0");
    public static readonly Color Orb = new("#c06cf0");

    /// <summary>
    /// Telegraph no chão (<c>render.js:98-103</c>, formas e alfas da v2) em âmbar (DECISIONS B1-D47): o magenta da v2 some
    /// nas salas roxas/magenta das Brechas e no Núcleo; o âmbar lê como "atenção" em todos os temas.
    /// </summary>
    public static readonly Color TelegraphFill = new(242 / 255f, 165 / 255f, 65 / 255f);
    public static readonly Color TelegraphLine = new(1f, 214 / 255f, 140 / 255f);
    public static readonly Color SlamFill = new(110 / 255f, 230 / 255f, 90 / 255f);
    public static readonly Color LureFill = new(242 / 255f, 165 / 255f, 65 / 255f);

    public static readonly Color[] Hit = [Creme, Brecha, Pink];
    public static readonly Color[] Crit = [Ambar, new("#fff3c4")];
    public static readonly Color[] Kill = [new("#2a1a35"), Brecha, Pink, Roxo];
    public static readonly Color[] Break = [new("#2a1a35"), Roxo, Brecha, Ambar];
    public static readonly Color[] Hurt = [HurtText, HpBarBottom];
    public static readonly Color[] Death = [Ambar, new("#ff8a3a"), Brecha];
    public static readonly Color[] Dash = [new("#8a7a90"), new("#5a4c63")];
    public static readonly Color[] Spawn = [Brecha, new("#2a1a35"), Pink];
    public static readonly Color[] Potion = [HealText, new("#e2475e"), Colors.White];
    public static readonly Color[] OrbTrail = [Orb, Brecha];
    public static readonly Color[] OrbBoom = [Orb, Brecha, Colors.White];
    public static readonly Color[] Smash = [new("#6b5a6e"), new("#4a3d52"), Ambar, Brecha];
    public static readonly Color[] Slam = [new("#5a4c3a"), new("#3b5a2a"), new("#6fd36a")];
    public static readonly Color[] Wall = [Creme, new("#8a7a90")];
    public static readonly Color[] Stun = [Ambar];
    public static readonly Color[] Wood = [new("#6a4428"), new("#3b2414"), new("#9a6a3a")];
    public static readonly Color[] Stone = [new("#4a3d52"), new("#2b2333"), new("#8a7a90")];
    public static readonly Color[] Chest = [Ambar, new("#fff3c4"), Ambar];
    public static readonly Color[] ChestPurple = [Ambar, new("#fff3c4"), Brecha];
    public static readonly Color[] Eco = [Orb, Brecha, Colors.White];
    public static readonly Color[] Votive = [Cyan, Ambar];
    public static readonly Color[] Charge = [Brecha, Pink];
    public static readonly Color[] Teleport = [Brecha, new("#2a1a35")];
    public static readonly Color[] Shard = [Brecha, new("#2a1a35"), Pink];
    public static readonly Color[] Wave = [new("#7fd0e8"), new("#cfeef7")];
    public static readonly Color[] LureBoom = [Brecha, new("#ffd2e6"), Ambar];
    public static readonly Color[] EcoLureBoom = [Ambar, Brecha, new("#fff3c4")];
    public static readonly Color[] Ambers = [Ambar];
    public static readonly Color[] Brechas = [Brecha];

    private static readonly Dictionary<string, Color> Cache = [];

    /// <summary>Cor de um hex dos dados (<c>#rrggbb</c>), em cache.</summary>
    public static Color Of(string hex)
    {
        if (!Cache.TryGetValue(hex, out var color))
        {
            color = new Color(hex);
            Cache[hex] = color;
        }

        return color;
    }

    private static readonly Dictionary<Color, Color[]> Singles = [];

    /// <summary>Array de uma cor só (para partículas), em cache.</summary>
    public static Color[] Single(Color color)
    {
        if (!Singles.TryGetValue(color, out var arr))
        {
            arr = [color];
            Singles[color] = arr;
        }

        return arr;
    }
}

/// <summary>Material do flash branco (<c>white:1</c> da v2): uma cópia por sprite, uniform <c>flash</c> 0–1.</summary>
public static class FlashMaterial
{
    public static readonly StringName FlashParam = "flash";

    private static Shader? _shader;

    private static Shader Shader => _shader ??= new Shader
    {
        Code = """
            shader_type canvas_item;
            uniform float flash : hint_range(0.0, 1.0) = 0.0;
            void fragment() {
                // COLOR já chega como textura × modulate no Godot 4: multiplicar a textura de novo escurecia todo
                // monstro e o caçador (cores ao quadrado). DECISIONS C2-D8.
                COLOR = vec4(mix(COLOR.rgb, vec3(1.0), flash), COLOR.a);
            }
            """,
    };

    public static ShaderMaterial Create() => new() { Shader = Shader };
}
