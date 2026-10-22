using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.World;

namespace Vespera.World.City;

/// <summary>
/// NPC com rotina no mapa (spec 01 §4.3/§6.1, <c>drawNpc</c>, <c>render.js:177-181</c>): sprite <c>npc_{id}_f|b</c> do
/// atlas com sombra, espelho para a esquerda, bob andando <c>-|sin(walkT·9)|·1,2</c> e parado <c>sin(2t + x)·0,3</c>,
/// nome âmbar em <c>y-40</c>. A lógica de andar é o <see cref="TownWalker"/> do Core; o percurso
/// (<see cref="WalkerRoute"/>) é calculado numa thread e o NPC aparece quando ele fica pronto.
/// Interagir (r = 22) emite <c>DialogueRequested</c> com a fala da vez, verbatim.
/// </summary>
public partial class TownNpc : Node2D
{
    public const float TalkRadius = 22f;

    private const float FadeSeconds = 0.5f;

    private Node2D _body = null!;
    private AtlasSprite _sprite = null!;
    private AtlasSprite _shadow = null!;
    private NpcInteractable _interactable = null!;
    private Label? _name;
    private string _frontKey = string.Empty;
    private string _backKey = string.Empty;
    private float _time;
    private float _alpha;
    private bool _leaving;

    public WalkerDef Def { get; set; } = null!;

    public Task<WalkerRoute> Route { get; set; } = null!;

    public WorldLabels? Labels { get; set; }

    /// <summary>Cérebro do Core (null até o percurso ficar pronto).</summary>
    public TownWalker? Walker { get; private set; }

    /// <summary>Saindo do mapa (fim do horário): some e se libera.</summary>
    public bool IsLeaving => _leaving;

    public override void _Ready()
    {
        _frontKey = $"npc_{Def.Id}_f";
        _backKey = $"npc_{Def.Id}_b";
        _shadow = new AtlasSprite { Name = "Shadow", Key = "shadow", Position = new Vector2(0, 2), Modulate = Colors.White with { A = 0.45f } };
        AddChild(_shadow);
        _body = new Node2D { Name = "Body" };
        AddChild(_body);
        _sprite = new AtlasSprite { Name = "Sprite", Key = _frontKey };
        _body.AddChild(_sprite);
        AddChild(PresenceLight());
        _interactable = new NpcInteractable { Name = "Talk", Npc = this, Id = Def.Id, Prompt = $"Falar com {Def.Name}", Radius = TalkRadius, Enabled = false };
        AddChild(_interactable);
        if (Labels is not null)
        {
            _name = Labels.Rent(WorldLabels.Style.NpcName);
            WorldLabels.SetText(_name, Def.Name);
            // O corpo só aparece quando a rota (calculada em thread) fica pronta: até lá o nome também não aparece,
            // senão ficava solto no canto da tela (QA "nome de morador nunca aparece solto").
            _name.Visible = false;
        }

        Modulate = Colors.White with { A = 0f };
        Visible = false;
    }

    /// <summary>
    /// Furo fraco e sem glow em volta de quem anda pela rua (B2-D51): com a noite mais escura, o morador continua legível
    /// sem parecer que carrega uma lanterna.
    /// </summary>
    public static LightEmitter PresenceLight() => new()
    {
        Name = "Presence",
        Radius = 20f,
        Kind = CityLighting.AmberKind,
        Intensity = 0.55f,
        GlowBoost = 0f,
        Position = new Vector2(0, -12),
    };

    public override void _ExitTree()
    {
        Labels?.Return(_name);
        _name = null;
    }

    /// <summary>Some aos poucos e se libera (o horário dele acabou).</summary>
    public void Leave()
    {
        _leaving = true;
        _interactable.Enabled = false;
    }

    /// <summary>
    /// Passo de simulação (o diretor só chama com o mundo andando). <paramref name="others"/> são as posições dos corpos
    /// do mapa (jogador e moradores) e <paramref name="self"/> o índice deste NPC nela, para ele desviar de quem está no caminho.
    /// </summary>
    public void Step(IWalkArea area, double dt, IRandom random, IReadOnlyList<MapPoint>? others = null, int self = -1, WalkPlanner? planner = null)
    {
        if (Walker is null)
        {
            if (!Route.IsCompletedSuccessfully)
            {
                return;
            }

            Walker = new TownWalker(Def, Route.Result, random, planner);
            Position = new Vector2(Walker.X, Walker.Y);
            Visible = true;
            _interactable.Enabled = !_leaving;
        }

        Walker.Update(area, dt, others, self);
        Position = new Vector2(Walker.X, Walker.Y);
    }

    /// <summary>Fala com o jogador: pega a fala da vez, para 4 s, vira para ele e pede o diálogo.</summary>
    public void Talk(Node2D interactor)
    {
        if (Walker is null)
        {
            return;
        }

        var line = Walker.Talk(interactor.GlobalPosition.X, interactor.GlobalPosition.Y);
        UpdatePose();
        EventBus.Instance.EmitSignal(EventBus.SignalName.DialogueRequested, Def.Name, new[] { line });
    }

    public override void _Process(double delta)
    {
        var dt = (float)Math.Min(0.05, delta);
        _time += dt;
        if (Walker is null)
        {
            return;
        }

        _alpha = _leaving ? MathF.Max(0f, _alpha - (dt / FadeSeconds)) : MathF.Min(1f, _alpha + (dt / FadeSeconds));
        Modulate = Colors.White with { A = _alpha };
        if (_leaving && _alpha <= 0f)
        {
            QueueFree();
            return;
        }

        UpdatePose();
        if (_name is not null)
        {
            _name.Visible = Visible;
            _name.Modulate = Colors.White with { A = _alpha };
            WorldLabels.PlaceCentered(_name, WorldLabels.ToScreen(this, GlobalPosition + new Vector2(0, -40)));
        }
    }

    private void UpdatePose()
    {
        if (Walker is null)
        {
            return;
        }

        _sprite.Key = Walker.Facing == TownFacing.Up ? _backKey : _frontKey;
        var bob = Walker.IsMoving
            ? -MathF.Abs(MathF.Sin((float)Walker.WalkT * 9f)) * 1.2f
            : MathF.Sin((_time * 2f) + Walker.X) * 0.3f;
        _body.Position = new Vector2(0, bob);
        _body.Scale = new Vector2(Walker.Facing == TownFacing.Left ? -1f : 1f, 1f);
    }
}
