using Godot;
using Vespera.Game.Combat;
using Vespera.Game.Hunter;
using Vespera.Game.UI;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Hunt;

/// <summary>
/// HUD das caçadas (canto de baixo à esquerda e topo à direita): poções na bolsa com a tecla de beber, ícone do Eco
/// Viscoso com a recarga girando (só depois da Lodo-Mãe), número da sala na caçada e Lumes na cidade. Lê o estado a
/// cada quadro; não toca na <see cref="CombatHud"/>.
/// </summary>
public partial class HuntHud : Control
{
    public const string FontPath = "res://assets/v2/fonts/PixelifySans.ttf";
    private static readonly Color Potion = new("e2566e");
    private static readonly Color Slime = new("7fd36a");

    private LevelWorld? _world;
    private Label _potions = null!;
    private Label _room = null!;
    private Label _lumes = null!;
    private EcoIcon _eco = null!;
    private PotionIcon _potionIcon = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        _potionIcon = new PotionIcon { Position = new Vector2(30, 952), Size = new Vector2(64, 84) };
        AddChild(_potionIcon);
        _potions = MakeLabel(40, Palette.Cream);
        _potions.Position = new Vector2(100, 966);
        AddChild(_potions);
        _eco = new EcoIcon { Position = new Vector2(300, 948), Size = new Vector2(92, 92), Visible = false };
        AddChild(_eco);
        _room = MakeLabel(40, Palette.Cream);
        _room.HorizontalAlignment = HorizontalAlignment.Right;
        _room.Position = new Vector2(1320, 28);
        _room.Size = new Vector2(570, 50);
        AddChild(_room);
        _lumes = MakeLabel(34, Palette.Amber);
        _lumes.HorizontalAlignment = HorizontalAlignment.Right;
        _lumes.Position = new Vector2(1320, 80);
        _lumes.Size = new Vector2(570, 44);
        AddChild(_lumes);
    }

    public void Bind(LevelWorld world) => _world = world;

    public override void _Process(double delta)
    {
        if (_world is null || !IsInstanceValid(_world) || !Progression.IsActive)
        {
            Visible = false;
            return;
        }

        Visible = true;
        var state = HuntService.State;
        var max = HuntService.Config.Potions.Max;
        _potions.Text = $"{state.Potions}/{max}  [R]";
        _potionIcon.Filled = state.Potions > 0;
        var combat = _world.Player.Combat;
        _eco.Visible = HuntService.HasEcoViscoso;
        _eco.Fraction = combat.EcoCooldownFraction;
        _room.Text = RiftSession.InRift && HuntService.Plan is { } plan
            ? $"Sala {RiftSession.RoomIndex + 1}/{plan.Rooms.Count}{(plan.HasGuardian ? "  ·  Covil da Lodo-Mãe" : string.Empty)}"
            : HuntService.GuardianUnlocked ? "A Brecha está mais forte…" : $"Caçadas completas: {state.RunsCompleted}/{HuntService.Config.GuardianAfterRuns}";
        _lumes.Text = $"{Progression.State.Lumes} Lumes · {state.Flowers} flores";
    }

    public static Label MakeLabel(int size, Color color)
    {
        var label = new Label { MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", GD.Load<Font>(FontPath));
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.04f, 0.1f));
        label.AddThemeConstantOverride("outline_size", 8);
        return label;
    }

    /// <summary>Frasco de poção desenhado (cheio = vermelho; vazio = vidro).</summary>
    private partial class PotionIcon : Control
    {
        public bool Filled { get; set; }

        public override void _Process(double delta) => QueueRedraw();

        public override void _Draw()
        {
            var s = Size;
            var center = new Vector2(s.X / 2f, s.Y * 0.62f);
            var r = s.X * 0.4f;
            DrawCircle(center, r + 4f, new Color(0.05f, 0.04f, 0.1f));
            DrawCircle(center, r, new Color(0.75f, 0.85f, 0.95f, 0.35f));
            if (Filled)
            {
                DrawCircle(center + new Vector2(0, r * 0.18f), r * 0.82f, Potion);
                DrawCircle(center + new Vector2(-r * 0.3f, -r * 0.1f), r * 0.2f, new Color(1f, 0.85f, 0.9f, 0.8f));
            }

            DrawRect(new Rect2(s.X * 0.38f, s.Y * 0.08f, s.X * 0.24f, s.Y * 0.22f), new Color(0.75f, 0.85f, 0.95f, 0.6f));
            DrawRect(new Rect2(s.X * 0.34f, s.Y * 0.04f, s.X * 0.32f, s.Y * 0.08f), new Color(0.55f, 0.4f, 0.25f));
        }
    }

    /// <summary>Ícone do Eco Viscoso: gota verde com a recarga em setor escuro girando e a tecla Q.</summary>
    private partial class EcoIcon : Control
    {
        private Label? _key;

        /// <summary>0 = pronto; 1 = recém-usado.</summary>
        public float Fraction { get; set; }

        public override void _Ready()
        {
            _key = MakeLabel(30, Palette.Cream);
            _key.Text = "Q";
            _key.Position = new Vector2(Size.X - 26, Size.Y - 34);
            AddChild(_key);
        }

        public override void _Process(double delta) => QueueRedraw();

        public override void _Draw()
        {
            var c = Size / 2f;
            var r = Size.X * 0.46f;
            DrawCircle(c, r + 4f, new Color(0.05f, 0.04f, 0.1f));
            DrawCircle(c, r, Fraction <= 0f ? Slime : Slime.Darkened(0.55f));
            DrawCircle(c + new Vector2(-r * 0.25f, -r * 0.2f), r * 0.32f, new Color(0.85f, 1f, 0.8f, 0.85f));
            if (Fraction > 0f)
            {
                var points = new System.Collections.Generic.List<Vector2> { c };
                const int steps = 32;
                for (var i = 0; i <= steps; i++)
                {
                    var a = (-Mathf.Pi / 2f) + (Mathf.Tau * Fraction * i / steps);
                    points.Add(c + (Vector2.FromAngle(a) * r));
                }

                DrawColoredPolygon(points.ToArray(), new Color(0f, 0f, 0f, 0.55f));
            }
            else
            {
                DrawArc(c, r + 2f, 0f, Mathf.Tau, 40, new Color(1f, 1f, 0.8f, 0.9f), 3f);
            }
        }
    }
}
