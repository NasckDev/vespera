using Godot;
using Vespera.Game.Boot;
using Vespera.Game.Combat;
using Vespera.Game.Hunter;
using Vespera.Game.UI;
using Vespera.Game.World;
using System.Linq;
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
    private Label _goal = null!;
    private Label _contracts = null!;
    private Label _lumes = null!;
    private Label _soup = null!;
    private Label _map = null!;
    private static readonly Color MapBlue = new("6fb7ff");

    /// <summary>Linhas da sopa da Nilza e do mapa da Lis (testes e capturas; vazias quando inativas).</summary>
    public string PerksText => (_soup.Visible ? _soup.Text : string.Empty) + (_map.Visible ? "\n" + _map.Text : string.Empty);
    private readonly EcoIcon[,] _ecos = new EcoIcon[2, 2];
    private PotionIcon _potionIcon = null!;

    /// <summary>Linha do objetivo da sala (testes e capturas).</summary>
    public string GoalText => _goal.Text;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        _potionIcon = new PotionIcon { Position = new Vector2(30, 952), Size = new Vector2(64, 84) };
        AddChild(_potionIcon);
        _potions = MakeLabel(40, Palette.Cream);
        _potions.Position = new Vector2(100, 966);
        AddChild(_potions);
        for (var player = 0; player < 2; player++)
        {
            for (var slot = 0; slot < 2; slot++)
            {
                // J1 embaixo à esquerda ao lado das poções; J2 embaixo à direita (espelhado).
                var x = player == 0 ? 300 + (slot * 104) : 1920 - 300 - 92 - (slot * 104);
                _ecos[player, slot] = new EcoIcon { Position = new Vector2(x, 948), Size = new Vector2(92, 92), Visible = false };
                AddChild(_ecos[player, slot]);
            }
        }
        _room = RightLabel(40, Palette.Amber, 28);
        _goal = RightLabel(34, Palette.Cream, 76);
        _contracts = RightLabel(28, Palette.CreamDim, 124);
        _contracts.AutowrapMode = TextServer.AutowrapMode.Off;
        _lumes = MakeLabel(30, Palette.Amber);
        _lumes.Position = new Vector2(160, 180);
        AddChild(_lumes);
        _soup = MakeLabel(30, Slime);
        _soup.Position = new Vector2(160, 222);
        _soup.Text = Core.Hunt.RunPerks.SoupLabel;
        AddChild(_soup);
        _map = RightLabel(28, MapBlue, 254);
        _map.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _map.Size = new Vector2(870, 80);
    }

    private Label RightLabel(int size, Color color, float y)
    {
        var label = MakeLabel(size, color);
        label.HorizontalAlignment = HorizontalAlignment.Right;
        label.Position = new Vector2(1020, y);
        label.Size = new Vector2(870, size + 10);
        AddChild(label);
        return label;
    }

    private const float RoomRefreshSeconds = 0.25f;

    private (int, int, string) _potionKey = (-1, -1, "");
    private (Vespera.Core.Ranks.Rank?, int, int, int) _xpKey = (null, -1, -1, -1);
    private (bool SoupActive, bool SecondLifeReady) _soupKey = (false, false);
    private float _roomTimer;

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
        var potionKey = (state.Potions, HuntService.Config.Potions.Max, InputDevices.KeyName(InputActions.Potion));
        if (potionKey != _potionKey)
        {
            _potionKey = potionKey;
            _potions.Text = $"{state.Potions}/{HuntService.Config.Potions.Max}  [{potionKey.Item3}]";
        }

        _potionIcon.Filled = state.Potions > 0;
        UpdateEcos();
        var status = Progression.Status;
        var lumes = Progression.State.Lumes;
        var xpKey = (status.Next, status.XpIntoRank, status.XpSpan, lumes);
        if (xpKey != _xpKey)
        {
            _xpKey = xpKey;
            var xp = status.Next is { } next ? $"XP {status.XpIntoRank}/{status.XpSpan} até o rank {next}" : "XP no topo";
            _lumes.Text = $"{xp}  ·  {lumes} Lumes";
        }

        var inRift = RiftSession.InRift && HuntService.Plan is not null;
        _room.Visible = _goal.Visible = _contracts.Visible = inRift;
        var soupKey = (HuntService.SoupActive, HuntService.SecondLifeReady);
        _soup.Visible = inRift && (soupKey.SoupActive || soupKey.SecondLifeReady);
        if (soupKey != _soupKey)
        {
            _soupKey = soupKey;
            _soup.Text = string.Join("  ·  ", new[] { soupKey.SoupActive ? Core.Hunt.RunPerks.SoupLabel : null, soupKey.SecondLifeReady ? Core.Hunt.RunPerks.SecondLifeLabel : null }.Where(s => s is not null));
        }

        _map.Visible = inRift && HuntService.MapActive;
        _roomTimer -= (float)delta;
        if (!inRift)
        {
            _roomTimer = 0f;
            return;
        }

        if (_roomTimer > 0f)
        {
            return;
        }

        // Sala, objetivo, contratos e mapa mudam devagar: refrescar 4x por segundo basta e poupa strings/LINQ por quadro.
        _roomTimer = RoomRefreshSeconds;
        UpdateRoom();
        if (_map.Visible)
        {
            _map.Text = Core.Hunt.RunPerks.MapLine(HuntService.Plan!.Rooms.Select(r => r.Kind).ToList(), RiftSession.RoomIndex);
        }
    }

    /// <summary>Os dois slots de Eco de cada caçador: só aparece o que tem Eco equipado; o segundo só depois do Ato V.</summary>
    private void UpdateEcos()
    {
        for (var player = 0; player < 2; player++)
        {
            var hunter = player == 0 ? _world!.Player : _world!.PlayerAt(player);
            var present = hunter is not null && IsInstanceValid(hunter);
            for (var slot = 0; slot < 2; slot++)
            {
                var icon = _ecos[player, slot];
                var id = present ? hunter!.Combat.EcoInSlot(slot) : null;
                icon.Visible = id is not null;
                if (id is null)
                {
                    continue;
                }

                icon.Fraction = hunter!.Combat.EchoCooldownFraction(slot);
                icon.EcoId = id;
                icon.KeyText = KeyLabel(player, slot == 0 ? Boot.InputActions.Skill : Boot.InputActions.Skill2);
            }
        }
    }

    private static string KeyLabel(int player, string action) => player == 0
        ? Boot.InputDevices.KeyName(action)
        : Boot.GlyphNames.Describe(Boot.InputSetup.TableFor(player).Get(action, Boot.BindingSlot.Primary), Boot.GlyphNames.DetectFamily());

    private void UpdateRoom()
    {
        var plan = HuntService.Plan!;
        var director = _world!.Director;
        var waves = director is { WaveCount: > 0 } d ? $"  ·  Onda {System.Math.Max(1, d.WavesReleased)}/{d.WaveCount}" : string.Empty;
        _room.Text = $"Sala {RiftSession.RoomIndex + 1}/{plan.Rooms.Count}{waves}";
        _goal.Text = RoomGoal(director);
        var board = HuntService.Board;
        var lines = HuntService.Contracts.Select(c => board.Evaluate(c, HuntService.Report))
            .Select(p => $"{(p.Done ? "✔" : "·")} {p.Title} {p.Have}/{p.Need}");
        _contracts.Text = string.Join('\n', lines);
        _contracts.Size = new Vector2(870, 40 * 3);
    }

    /// <summary>O que fazer nesta sala, numa linha.</summary>
    public static string RoomGoal(RoomDirector? director)
    {
        if (director is null)
        {
            return string.Empty;
        }

        if (director.Boss is { Dead: false })
        {
            return "Derrote a Lodo-Mãe";
        }

        // Final: o Reflexo não abre fenda; depois dele é o sino de baixo.
        if (director.Guardian is Actors.Enemies.Reflexo reflexo)
        {
            return reflexo.Dead ? "Suba a escada e toque o sino de baixo" : "Vença o Reflexo: leia o âmbar antes de cada golpe";
        }

        if (!director.Cleared)
        {
            return director.Kind == "elite" ? "Vença os monstros de elite" : "Vença os monstros para abrir a passagem";
        }

        if (director.RestAvailable)
        {
            return "Descanse na lanterna, depois siga em frente";
        }

        if (director.Chest is { IsOpen: false })
        {
            return "Abra o baú do tesouro";
        }

        return RiftSession.IsLastRoom ? "Saia pela fenda e volte para a Praça" : "Siga pela passagem";
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

        /// <summary>Eco no slot (muda a cor do ícone).</summary>
        public string EcoId { get; set; } = string.Empty;

        /// <summary>Tecla ou botão que aciona este slot, como o jogador remapeou.</summary>
        public string KeyText
        {
            set
            {
                if (_key is not null && _key.Text != value)
                {
                    _key.Text = value;
                }
            }
        }

        public override void _Ready()
        {
            _key = MakeLabel(30, Palette.Cream);
            _key.Text = string.Empty;
            _key.Position = new Vector2(Size.X - 26, Size.Y - 34);
            AddChild(_key);
        }

        private Color BaseColor => EcoId == "eco_viscoso" || EcoId.Length == 0 ? Slime : Palette.Amber;

        public override void _Process(double delta) => QueueRedraw();

        public override void _Draw()
        {
            var c = Size / 2f;
            var r = Size.X * 0.46f;
            DrawCircle(c, r + 4f, new Color(0.05f, 0.04f, 0.1f));
            DrawCircle(c, r, Fraction <= 0f ? BaseColor : BaseColor.Darkened(0.55f));
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
