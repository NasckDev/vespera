using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Boot;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Testing;

/// <summary>
/// Contrato de escala na engine (<c>--escala</c>, PLANO-V2 §2.1): em cada mapa (Praça e salas da Brecha), instancia
/// cada ator que pode aparecer ali, mede a altura opaca renderizada (pé até o pixel opaco mais alto do quadro
/// parado, × escala global do sprite) em px de mundo e falha se fugir mais de ±8% do alvo. Com janela e
/// <c>--shots</c>, fotografa a "fila de escala" de cada mapa: caçadores, Lodo, Espectro e Lodo-Mãe ao lado da porta-régua.
/// </summary>
public sealed class ScaleCheck(Level level, SmokeTest host, bool capture)
{
    private const float Alpha = 0.5f;
    private static readonly string[] Monsters = ["lodo", "espectro", "lodo_mae"];
    private static readonly HunterClass[] Hunters = [HunterClass.Lamina, HunterClass.Arco, HunterClass.Martelo];

    private readonly List<string> _failures = [];

    private LevelWorld World => level.World;

    public async Task<List<string>> Run()
    {
        var rooms = new List<string> { Level.HubLevelId };
        rooms.AddRange(RiftSession.Brecha.Rooms);
        foreach (var room in rooms)
        {
            await level.GoToRoom(room, withDirector: false);
            await host.PhysicsFrames(3);
            await MeasureRoom(room);
        }

        await level.GoToRoom(Level.HubLevelId);
        await host.PhysicsFrames(3);
        return _failures;
    }

    private async Task MeasureRoom(string room)
    {
        var human = World.Data.HumanHeight;
        var door = World.Data.DoorAt is { } d ? new Vector2(d[0], d[1]) : World.SpawnPoint;
        var start = World.ClampToFloor(door + (new Vector2(0f, 50f) * World.Data.ActorScale));
        var dir = start.X <= World.PlayArea.GetCenter().X ? 1f : -1f;
        var gap = human * 0.75f;
        var x = 0f;
        var spawned = new List<Node>();
        foreach (var hunter in Hunters)
        {
            var p = World.SpawnPlayer(new ScriptedInput(), World.ClampToFloor(start + new Vector2(dir * x, 0)), hunter);
            p.Combat.Disabled = true;
            spawned.Add(p);
            x += gap;
        }

        var enemies = new List<Enemy>();
        foreach (var id in Monsters)
        {
            x += id == "lodo_mae" ? gap * 1.6f : 0f;
            Enemy e = id switch
            {
                "lodo" => new Lodo(World),
                "espectro" => new Espectro(World),
                _ => new LodoMae(World),
            };
            e.Position = World.ClampToFloor(start + new Vector2(dir * x, 0));
            World.AddEnemy(e);
            enemies.Add(e);
            spawned.Add(e);
            x += id == "lodo_mae" ? gap * 2f : gap;
        }

        await host.PhysicsFrames(2);
        foreach (var e in enemies)
        {
            e.ProcessMode = Node.ProcessModeEnum.Disabled;
            e.PlayAnimation("parado");
        }

        foreach (var node in spawned)
        {
            var (id, sprite) = node switch
            {
                Player p => (HunterSprites.ClassFolder(p.Hunter), p.Sprite),
                Enemy e => (e.MonsterId, e.SpriteNode),
                _ => (string.Empty, null!),
            };
            var measured = Measure(sprite);
            var target = ScaleRegistry.TargetHeight(id, human);
            var ok = ScaleRegistry.WithinTolerance(measured, target);
            Log.Info($"escala {room}: {id} {measured:0.0} px (alvo {target:0.0}, humano {human:0.0}) {(ok ? "ok" : "FORA")}");
            if (!ok)
            {
                _failures.Add($"escala: {id} em {room} mede {measured:0.0} px, alvo {target:0.0} ±8%");
            }
        }

        if (capture)
        {
            World.Camera.FixedFocus = start + new Vector2(dir * x * 0.45f, -human * 0.8f);
            await host.PhysicsFrames(20);
            await host.CaptureNow($"c34_escala_{room}");
            World.Camera.FixedFocus = null;
        }

        foreach (var node in spawned)
        {
            node.QueueFree();
        }

        await host.PhysicsFrames(2);
    }

    /// <summary>
    /// Mediana, nos quadros da animação parada que está tocando, da distância do pé (origem do nó) ao pixel opaco mais
    /// alto, × escala global do sprite: a mesma definição de <c>tools/art/medir_altura_em_pe.py</c>, mas medida no jogo.
    /// </summary>
    private static float Measure(AnimatedSprite2D sprite)
    {
        var count = sprite.SpriteFrames.GetFrameCount(sprite.Animation);
        var heights = new List<float>();
        for (var i = 0; i < count; i++)
        {
            heights.Add(MeasureFrame(sprite, i));
        }

        heights.Sort();
        return count % 2 == 1 ? heights[count / 2] : (heights[(count / 2) - 1] + heights[count / 2]) / 2f;
    }

    private static float MeasureFrame(AnimatedSprite2D sprite, int frame)
    {
        var texture = sprite.SpriteFrames.GetFrameTexture(sprite.Animation, frame);
        Image image;
        if (texture is AtlasTexture atlas)
        {
            var whole = atlas.Atlas.GetImage() ?? Image.LoadFromFile(ProjectSettings.GlobalizePath(atlas.Atlas.ResourcePath));
            image = whole.GetRegion((Rect2I)atlas.Region);
        }
        else
        {
            image = texture.GetImage();
        }

        var top = image.GetHeight();
        for (var y = 0; y < image.GetHeight() && top == image.GetHeight(); y++)
        {
            for (var x = 0; x < image.GetWidth(); x++)
            {
                if (image.GetPixel(x, y).A >= Alpha)
                {
                    top = y;
                    break;
                }
            }
        }

        var footY = (image.GetHeight() / 2f) - sprite.Offset.Y;
        return (footY - top) * sprite.GlobalScale.Y;
    }
}
