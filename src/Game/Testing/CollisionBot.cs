using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.World;

namespace Vespera.Game.Testing;

/// <summary>
/// Bot de colisão da fase (headless). 1) Sondas: para cada face parede→chão da grade e cada lado de cada pegada de
/// objeto, um caçador nasce encostado e empurra contra ela por 2 s (reto e nas duas diagonais); nunca pode terminar
/// nem passar por dentro. 2) Passeio aleatório de 60 s com vários caçadores: sem atravessar nada e sem ficar preso
/// (parado com o caminho livre à frente). Usa uma checagem geométrica própria, independente da física.
/// </summary>
public sealed class CollisionBot(LevelWorld world, SmokeTest host)
{
    private const int PushFrames = 120;
    private const int WalkFrames = 3600;
    private const int Walkers = 8;
    private const float BaseTolerance = 1.5f;
    private const int Batch = 400;
    private const int StuckFrames = 90;
    private const float BaseStuckProbe = 10f;
    private const int MaxReported = 12;
    private const float Diagonal = 0.7071f;
    private const float BaseFaceGap = 6f;
    private const float BaseContactProbe = 4f;
    private const float BaseSlideStep = 12f;
    private const float BasePropGap = 40f;

    private readonly List<string> _failures = [];

    // Distâncias do bot acompanham a escala do caçador na fase (pé menor = folgas menores).
    private float S => world.Data.ActorScale;

    private float Tolerance => Mathf.Max(0.4f, BaseTolerance * S);
    private int _probes;
    private int _skipped;
    private int _touching;

    public async Task<List<string>> Run()
    {
        world.Player.Frozen = true;
        await ProbeAll();
        var walked = await RandomWalk();
        world.Player.Frozen = false;
        Log.Info($"bot de colisão: {_probes} sondas ({_touching} terminaram encostadas, {_skipped} puladas sem lugar livre), " +
                 $"{Walkers} andarilhos x {WalkFrames / 60} s, {walked:0} px andados, {_failures.Count} falha(s)");
        return _failures;
    }

    private async Task ProbeAll()
    {
        var starts = new List<(Vector2 Feet, Vector2 Push, string Label)>();
        foreach (var face in world.Grid.BoundaryFaces())
        {
            var feet = FaceStart(face);
            var push = new Vector2(-face.Dx, -face.Dy);
            var side = new Vector2(push.Y, push.X);
            var label = $"parede ({face.Col},{face.Row})";
            starts.Add((feet, push, label));
            starts.Add((feet, (push + side).Normalized(), label + " diag+"));
            starts.Add((feet, (push - side).Normalized(), label + " diag-"));
        }

        foreach (var prop in world.Props)
        {
            var f = prop.FootprintRect;
            if (f.Size.X <= 0)
            {
                continue;
            }

            var gap = BasePropGap * S;
            var mid = f.GetCenter();
            starts.Add((new Vector2(mid.X, f.Position.Y - gap + (8f * S)), Vector2.Down, $"{prop.Name} (de cima)"));
            starts.Add((new Vector2(mid.X, f.End.Y + gap), Vector2.Up, $"{prop.Name} (de baixo)"));
            starts.Add((new Vector2(f.Position.X - gap, f.End.Y - (4f * S)), Vector2.Right, $"{prop.Name} (da esquerda)"));
            starts.Add((new Vector2(f.End.X + gap, f.End.Y - (4f * S)), Vector2.Left, $"{prop.Name} (da direita)"));
            starts.Add((new Vector2(f.End.X + gap, f.End.Y + gap), new Vector2(-Diagonal, -Diagonal), $"{prop.Name} (diagonal)"));
        }

        for (var first = 0; first < starts.Count; first += Batch)
        {
            await ProbeBatch(starts.GetRange(first, Math.Min(Batch, starts.Count - first)));
        }
    }

    private async Task ProbeBatch(List<(Vector2 Feet, Vector2 Push, string Label)> starts)
    {
        var bots = new List<(Player Bot, ScriptedInput Input, string Label)>();
        foreach (var (wanted, push, label) in starts)
        {
            var feet = FreeSpotNear(wanted, new Vector2(push.Y, push.X));
            if (feet is null)
            {
                _skipped++;
                continue;
            }

            var input = new ScriptedInput { Move = push };
            bots.Add((world.SpawnPlayer(input, feet.Value), input, label));
        }

        _probes += bots.Count;
        await host.PhysicsFrames(2);
        for (var frame = 0; frame < PushFrames; frame++)
        {
            await host.PhysicsFrames(1);
            foreach (var (bot, _, label) in bots)
            {
                if (world.PenetratesSolid(bot.GlobalPosition, Tolerance))
                {
                    Fail($"sonda {label} entrou no sólido no quadro {frame} em {bot.GlobalPosition.Round()}");
                    bot.Input = new ScriptedInput();
                }
            }
        }

        foreach (var (bot, input, _) in bots)
        {
            _touching += world.PenetratesSolid(bot.GlobalPosition + (input.Move.Normalized() * BaseContactProbe * S), 0f) ? 1 : 0;
            bot.QueueFree();
        }

        await host.PhysicsFrames(2);
    }

    /// <summary>Pé do caçador encostado (a <see cref="BaseFaceGap"/> x escala px) do lado andável de uma face da grade.</summary>
    private Vector2 FaceStart(GridFace face)
    {
        var s = world.Data.ActorScale;
        var hw = Player.FeetHalfWidth * s;
        var h = 2 * Player.FeetHalfHeight * s;
        var wall = world.Grid.CellRect(face.Col, face.Row);
        var (cx, cy) = world.Grid.CellCenter(face.Col, face.Row);
        return (face.Dx, face.Dy) switch
        {
            (1, _) => new Vector2(wall.Right + hw + (BaseFaceGap * S), cy + (h / 2f)),
            (-1, _) => new Vector2(wall.X - hw - (BaseFaceGap * S), cy + (h / 2f)),
            (_, 1) => new Vector2(cx, wall.Bottom + h + (BaseFaceGap * S)),
            _ => new Vector2(cx, wall.Y - (BaseFaceGap * S)),
        };
    }

    /// <summary>O próprio ponto, ou deslizado ao longo da face até 2 passos, se ali o pé couber sem tocar em nada.</summary>
    private Vector2? FreeSpotNear(Vector2 feet, Vector2 along)
    {
        foreach (var k in new[] { 0f, 1f, -1f, 2f, -2f })
        {
            var spot = feet + (along * BaseSlideStep * S * k);
            if (!world.PenetratesSolid(spot, -S))
            {
                return spot;
            }
        }

        return null;
    }

    private async Task<float> RandomWalk()
    {
        var rng = new Random(20261022);
        var walkers = new List<Walker>();
        var spots = new List<Vector2>();
        for (var r = 0; r < world.Grid.Rows; r++)
        {
            for (var c = 0; c < world.Grid.Cols; c++)
            {
                var (x, y) = world.Grid.CellCenter(c, r);
                if (!world.PenetratesSolid(new Vector2(x, y), 0f))
                {
                    spots.Add(new Vector2(x, y));
                }
            }
        }

        for (var i = 0; i < Walkers; i++)
        {
            var input = new ScriptedInput();
            walkers.Add(new Walker(world.SpawnPlayer(input, spots[rng.Next(spots.Count)]), input));
        }

        var walked = 0f;
        await host.PhysicsFrames(2);
        for (var frame = 0; frame < WalkFrames; frame++)
        {
            foreach (var w in walkers)
            {
                w.Steer(rng);
            }

            await host.PhysicsFrames(1);
            foreach (var w in walkers)
            {
                walked += w.Advance();
                CheckWalker(w, frame);
            }
        }

        foreach (var w in walkers)
        {
            w.Bot.QueueFree();
        }

        return walked;
    }

    private void CheckWalker(Walker w, int frame)
    {
        var feet = w.Bot.GlobalPosition;
        if (world.PenetratesSolid(feet, Tolerance) || !world.Bounds.HasPoint(feet))
        {
            Fail($"andarilho atravessou sólido/borda no quadro {frame} em {feet.Round()}");
            w.Bot.Teleport(w.LastGood);
            return;
        }

        w.LastGood = feet;
        var ahead = feet + (w.Input.Move.Normalized() * BaseStuckProbe * S);
        var pathFree = w.Input.Move != Vector2.Zero && !world.PenetratesSolid(ahead, -S);
        w.StuckFor = pathFree && w.LastStep < 0.05f ? w.StuckFor + 1 : 0;
        if (w.StuckFor == StuckFrames)
        {
            Fail($"andarilho preso em {feet.Round()} empurrando {w.Input.Move} com caminho livre (quadro {frame})");
        }
    }

    private void Fail(string message)
    {
        if (_failures.Count < MaxReported)
        {
            _failures.Add($"colisão: {message}");
        }
        else if (_failures.Count == MaxReported)
        {
            _failures.Add("colisão: mais falhas omitidas");
        }
    }

    private sealed class Walker(Player bot, ScriptedInput input)
    {
        private int _turnIn;
        private Vector2 _previous = bot.GlobalPosition;

        public Player Bot { get; } = bot;

        public ScriptedInput Input { get; } = input;

        public Vector2 LastGood { get; set; } = bot.GlobalPosition;

        public float LastStep { get; private set; }

        public int StuckFor { get; set; }

        public void Steer(Random rng)
        {
            if (--_turnIn > 0)
            {
                return;
            }

            _turnIn = rng.Next(30, 120);
            var angle = (float)(rng.NextDouble() * Math.Tau);
            var strength = rng.NextDouble() < 0.15 ? 0f : 0.4f + (0.6f * (float)rng.NextDouble());
            Input.Move = Vector2.FromAngle(angle) * strength;
        }

        public float Advance()
        {
            LastStep = Bot.GlobalPosition.DistanceTo(_previous);
            _previous = Bot.GlobalPosition;
            return LastStep;
        }
    }
}
