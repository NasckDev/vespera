using Godot;
using Vespera.Core.Common;
using Vespera.Core.World;
using Vespera.Game.Boot;
using Vespera.Game.Components;
using Vespera.Game.Story;
using Vespera.Game.World.Rift;

namespace Vespera.Game.World;

/// <summary>
/// Ato III (ROTEIRO-V2 §13, "O Farol que Não Acendeu"): a lanterna "M." na pedra seca do Mercado Submerso (sala alagada,
/// da 2ª caçada no cais em diante) e a pesca na véspera (evento <c>OpenFishing</c>: barra com agulha, puxe quando o olho
/// de vidro brilhar; usa <see cref="FishingGame"/>). Ganchos: <c>OnWorldMounted</c> e o fim de cada conversa.
/// </summary>
public partial class Level
{
    public const string LanternRoom = "cais_mercado";
    public const string LanternDialogue = "ato3/lanterna_m";
    public const string FishedFlag = "pescou";
    private const string LanternTexture = "res://assets/v2/cenarios/praca/objetos/lanterna_chao.png";
    private const string EventOpenFishing = "OpenFishing";
    private const int FishLumes = 6;
    private static readonly Vector2 LanternStone = new(837, 437);

    private FishingOverlay? _fishing;

    /// <summary>A lanterna "M." está nesta sala agora (o caçador ainda não pegou e já fez uma caçada no cais).</summary>
    public static bool LanternWaiting(string roomId) =>
        roomId == LanternRoom
        && StoryService.Engine.HasFlag("cais_afogado:hunt_1")
        && !StoryService.Engine.HasFlag("lanterna_m");

    /// <summary>Chamado ao montar cada fase (gancho em <c>OnWorldMounted</c>).</summary>
    private void ShowActThree()
    {
        if (!RiftSession.InRift || !LanternWaiting(World.Data.Id))
        {
            return;
        }

        var human = World.Data.HumanHeight;
        var texture = GD.Load<Texture2D>(LanternTexture);
        var lantern = new Node2D { Name = "LanternaM", Position = LanternStone };
        var sprite = new Sprite2D
        {
            Texture = texture,
            Centered = true,
            Scale = Vector2.One * (human * 0.4f / Mathf.Max(1, texture.GetHeight())),
        };
        sprite.Position = new Vector2(0, -texture.GetHeight() * sprite.Scale.Y * 0.5f);
        lantern.AddChild(sprite);
        lantern.AddChild(new PointLight2D
        {
            Texture = BreachPortalGlow(),
            Color = new Color("f2a541"),
            Energy = 0.8f,
            TextureScale = human / 64f,
        });
        World.Entities.AddChild(lantern);
        var zone = new Interactable("talk", "Pegar a lanterna", 60f)
        {
            Name = "zone_lanterna_m",
            Dialogue = LanternDialogue,
            Position = LanternStone,
            PromptAnchor = LanternStone - new Vector2(0, human * 0.8f),
        };
        World.Entities.AddChild(zone);
        Log.Info("ato3: lanterna 'M.' na pedra seca do Mercado Submerso");
    }

    /// <summary>Some com a lanterna da pedra depois de pegar (a fala liga <c>lanterna_m</c>).</summary>
    private void HideTakenLantern()
    {
        if (!StoryService.Engine.HasFlag("lanterna_m"))
        {
            return;
        }

        World.Entities.GetNodeOrNull("LanternaM")?.QueueFree();
        if (World.Entities.GetNodeOrNull<Interactable>("zone_lanterna_m") is { } zone)
        {
            zone.Enabled = false;
            zone.QueueFree();
        }
    }

    private static Texture2D BreachPortalGlow()
    {
        var gradient = new Gradient { Colors = [Colors.White, new Color(1, 1, 1, 0)] };
        return new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
            Width = 128,
            Height = 128,
        };
    }

    /// <summary>Eventos de fala do Ato III (chamado no fim de cada conversa). Verdadeiro se tratou.</summary>
    private bool HandleActThreeEvent(string name)
    {
        HideTakenLantern();
        if (name != EventOpenFishing)
        {
            return false;
        }

        OpenFishing();
        return true;
    }

    private void OpenFishing()
    {
        if (_fishing is not null && IsInstanceValid(_fishing))
        {
            return;
        }

        var game = new FishingGame(new NumRange(1.2, 3.5), 0.18, 0.9, 4, new SeededRandom((int)(Time.GetTicksMsec() % int.MaxValue)));
        Player.Frozen = true;
        _fishing = new FishingOverlay(game) { Name = "Pesca" };
        _fishing.Finished += OnFishingFinished;
        AddChild(_fishing);
    }

    private void OnFishingFinished(bool caught)
    {
        Player.Frozen = false;
        _fishing = null;
        if (!caught)
        {
            CombatHud.Toast("Escapou. O peixe de lá não morde duas vezes seguidas.");
            return;
        }

        StoryService.SetFlag(FishedFlag);
        if (Hunter.Progression.IsActive)
        {
            Hunter.Progression.AddLumes(FishLumes);
        }

        CombatHud.Toast($"Peixe com olho de vidro! A Nilza paga: +{FishLumes} Lumes");
        GameSession.Autosave();
    }

    /// <summary>Barra da pesca: espera a mordida (o olho de vidro brilha), depois puxe com a agulha na faixa âmbar.</summary>
    private sealed partial class FishingOverlay(FishingGame game) : CanvasLayer
    {
        private const float BarWidth = 420f;
        private const float BarHeight = 26f;
        private Control _root = null!;
        private Label _label = null!;
        private bool _done;

        public event System.Action<bool>? Finished;

        public override void _Ready()
        {
            Layer = 20;
            _root = new Control { Name = "Barra" };
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.Draw += DrawBar;
            AddChild(_root);
            _label = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Text = "Espere o olho de vidro brilhar...",
            };
            _label.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
            _label.Position = new Vector2(-BarWidth * 0.5f, -170f);
            _label.Size = new Vector2(BarWidth, 30f);
            AddChild(_label);
        }

        public override void _Process(double delta)
        {
            if (_done)
            {
                return;
            }

            if (game.Update(delta) && game.State == FishingState.Biting)
            {
                _label.Text = "Mordeu! Puxe com a agulha na faixa âmbar.";
            }

            if (Input.IsActionJustPressed(InputActions.Interact) || Input.IsActionJustPressed(InputActions.Attack))
            {
                game.Pull();
            }

            _root.QueueRedraw();
            if (game.IsOver)
            {
                Finish(game.State == FishingState.Caught);
            }
        }

        private void Finish(bool caught)
        {
            _done = true;
            Finished?.Invoke(caught);
            QueueFree();
        }

        private void DrawBar()
        {
            var size = _root.GetViewportRect().Size;
            var at = new Vector2((size.X - BarWidth) * 0.5f, size.Y - 130f);
            _root.DrawRect(new Rect2(at - new Vector2(4, 4), new Vector2(BarWidth + 8, BarHeight + 8)), new Color(0.1f, 0.12f, 0.23f, 0.9f));
            _root.DrawRect(new Rect2(at, new Vector2(BarWidth, BarHeight)), new Color("3c7a8a"));
            var biting = game.State != FishingState.Waiting;
            _root.DrawRect(new Rect2(at + new Vector2((float)game.ZoneStart * BarWidth, 0), new Vector2((float)game.ZoneWidth * BarWidth, BarHeight)),
                new Color("f2a541") with { A = biting ? 0.9f : 0.3f });
            var eye = at + new Vector2(BarWidth * 0.5f, -26f);
            _root.DrawCircle(eye, 10f, biting ? new Color("f4e7c6") : new Color(0.4f, 0.45f, 0.5f));
            if (biting)
            {
                var x = at.X + ((float)game.Needle * BarWidth);
                _root.DrawRect(new Rect2(x - 2, at.Y - 6, 4, BarHeight + 12), Colors.White);
            }
        }
    }
}
