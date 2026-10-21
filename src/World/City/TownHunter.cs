using System;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;

namespace Vespera.World.City;

/// <summary>
/// Caçador ambiente (spec 01 §4.3/§6.2, <c>drawHunter</c>, <c>render.js:172-176</c>): <c>c_{arma}_{back|front|side}</c>
/// em escala 0,85, dessaturado e escurecido (<c>saturate(.35) brightness(.78)</c>, alpha 0,92), sombra 0,35, bob
/// <c>-|sin(walkT·11)|·1,4</c>; nome + Selo colorido em <c>y-38</c> e balão de fala acima. A lógica é o
/// <see cref="AmbientHunter"/> do Core. Não é interagível.
/// </summary>
public partial class TownHunter : Node2D
{
    /// <summary>O balão some nos últimos 0,4 s (a v2 cortava seco).</summary>
    private const float BubbleFade = 0.4f;

    private static ShaderMaterial? _tint;

    private Node2D _body = null!;
    private AtlasSprite _sprite = null!;
    private Label? _name;
    private Label? _rank;
    private Label? _bubble;
    private string[] _keys = new string[3];
    private string? _shownSay;

    public AmbientHunter Brain { get; set; } = null!;

    public WorldLabels? Labels { get; set; }

    /// <summary>Texto do balão na tela agora (testes), ou null.</summary>
    public string? Bubble => _bubble is { Visible: true } b ? b.Text : null;

    public override void _Ready()
    {
        _tint ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://src/World/City/hunter_tint.gdshader") };
        var w = Brain.Def.Weapon;
        _keys = [$"c_{w}_front", $"c_{w}_back", $"c_{w}_side"];
        AddChild(new AtlasSprite { Name = "Shadow", Key = "shadow", SpriteScale = 0.85f, Position = new Vector2(0, 2), Modulate = Colors.White with { A = 0.35f } });
        _body = new Node2D { Name = "Body" };
        AddChild(_body);
        _sprite = new AtlasSprite { Name = "Sprite", Key = _keys[0], SpriteScale = 0.85f, Material = _tint };
        _body.AddChild(_sprite);
        Position = new Vector2(Brain.X, Brain.Y);

        if (Labels is not null)
        {
            _name = Labels.Rent(WorldLabels.Style.HunterName);
            WorldLabels.SetText(_name, Brain.Def.Name);
            _rank = Labels.Rent(WorldLabels.Style.HunterRank);
            WorldLabels.SetText(_rank, Brain.Def.Rank);
            _rank.Modulate = RankColor(Brain.Def.Rank);
            _bubble = Labels.Rent(WorldLabels.Style.Bubble);
            _bubble.Visible = false;
        }
    }

    public override void _ExitTree()
    {
        Labels?.Return(_name);
        Labels?.Return(_rank);
        Labels?.Return(_bubble);
        _name = _rank = _bubble = null;
    }

    public void Step(IWalkArea area, float mapWidth, float mapHeight, double dt)
    {
        Brain.Update(area, mapWidth, mapHeight, dt);
        Position = new Vector2(Brain.X, Brain.Y);
    }

    public override void _Process(double delta)
    {
        _sprite.Key = Brain.Facing switch
        {
            TownFacing.Up => _keys[1],
            TownFacing.Down => _keys[0],
            _ => _keys[2],
        };
        var bob = Brain.IsMoving ? -MathF.Abs(MathF.Sin((float)Brain.WalkT * 11f)) * 1.4f : 0f;
        _body.Position = new Vector2(0, bob);
        _body.Scale = new Vector2(Brain.Facing == TownFacing.Left ? -1f : 1f, 1f);

        if (_name is null || _rank is null || _bubble is null)
        {
            return;
        }

        // render.js:221 — nome em (x, y-38) e o Selo logo à direita.
        var head = WorldLabels.ToScreen(this, GlobalPosition + new Vector2(0, -38));
        var nameWidth = _name.Size.X;
        WorldLabels.PlaceCentered(_name, head + new Vector2(-((_rank.Size.X + 2f) / 2f), 0));
        _rank.Position = new Vector2(_name.Position.X + nameWidth + 1, _name.Position.Y);

        if (Brain.Say is { } say)
        {
            if (!ReferenceEquals(say, _shownSay))
            {
                _shownSay = say;
                WorldLabels.SetText(_bubble, say);
            }

            _bubble.Visible = true;
            _bubble.Modulate = Colors.White with { A = (float)Math.Clamp(Brain.SayT / BubbleFade, 0, 1) };
            WorldLabels.PlaceCentered(_bubble, new Vector2(head.X, _name.Position.Y - 1));
        }
        else if (_bubble.Visible)
        {
            _bubble.Visible = false;
            _shownSay = null;
        }
    }

    private static Color RankColor(string rankId)
    {
        foreach (var r in GameManager.Db.Ranks)
        {
            if (r.Id == rankId)
            {
                return new Color(r.Color);
            }
        }

        return new Color("#F4E7C6");
    }
}
