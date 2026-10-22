using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.Rules;
using Vespera.Core.World;

namespace Vespera.World.City;

/// <summary>
/// Moradores do mapa atual (spec 01 §1.8, §6): NPCs com rotina (<c>db.Npcs.Walkers</c>) nos mapas e horários da v2,
/// caçadores ambiente (<c>RI(1, 2)</c> por mapa da cidade) e o chat simulado a cada <c>R(14, 28)</c> s, mostrado como
/// balão sobre um caçador do mapa (e emitido em <c>ChatMessage</c> para quem quiser um log). B2-D41: além da entrada
/// no mapa (v2), a presença dos NPCs é revista a cada hora do relógio; quem chega aparece, quem sai some.
/// </summary>
public sealed class TownsfolkDirector(CityLife city)
{
    /// <summary>
    /// Percursos já calculados (ou em cálculo) por mapa, máscara e NPC, sempre sobre a área andável carregada (outra
    /// máscara = outro percurso; nada depende de waypoints pensados para uma máscara específica).
    /// </summary>
    private static readonly Dictionary<string, Task<WalkerRoute>> Routes = [];

    private readonly List<TownNpc> _npcs = [];
    private readonly List<TownHunter> _hunters = [];

    /// <summary>Posições dos corpos do mapa neste frame: 0 = jogador, depois NPCs, depois caçadores (reaproveitada).</summary>
    private readonly List<MapPoint> _bodies = new(16);
    private WalkPlanner? _planner;
    private double _chatT = TownChat.FirstDelay;
    private MapDef? _map;

    public IReadOnlyList<TownNpc> Npcs => _npcs;

    public IReadOnlyList<TownHunter> Hunters => _hunters;

    private static IRandom Random => GameManager.Instance.State.Random;

    /// <summary>Mapa novo (<c>setupMap</c>): caçadores e NPCs presentes na hora atual.</summary>
    public void Populate(MapDef map)
    {
        Clear();
        _map = map;
        if (map.Dungeon)
        {
            return;
        }

        var state = GameManager.Instance.State;
        var world = city.World;
        if (_planner is null || !ReferenceEquals(_planner.Area, world.WalkArea))
        {
            _planner = new WalkPlanner(world.WalkArea); // replanejamento no frame (A* com buffers reaproveitados)
            _planner.Prepare(); // rotula as regiões já na troca de mapa (~10 ms), não no meio do passeio
        }

        var count = AmbientHunter.CountFor(map, state.Save.BrechasClosed, Random);
        var player = world.Player.GlobalPosition;
        foreach (var def in AmbientHunter.Pick(GameManager.Db.Social, state.Save.ClassId, count, Random))
        {
            var at = WalkMover.RandomPoint(world.WalkArea, map.ImageWidth, map.ImageHeight, Random, player.X, player.Y, minDistance: 70f);
            var hunter = new TownHunter
            {
                Name = $"Hunter_{def.Name}",
                Brain = new AmbientHunter(def, at.X, at.Y, GameManager.Db.Social, Random, _planner),
                Labels = city.Labels,
            };
            world.Entities.AddChild(hunter);
            _hunters.Add(hunter);
        }

        SpawnPresentWalkers(map);
    }

    /// <summary>Esquece os moradores (o World já liberou os nós na troca de mapa).</summary>
    public void Clear()
    {
        foreach (var npc in _npcs)
        {
            if (GodotObject.IsInstanceValid(npc))
            {
                npc.QueueFree();
            }
        }

        foreach (var hunter in _hunters)
        {
            if (GodotObject.IsInstanceValid(hunter))
            {
                hunter.QueueFree();
            }
        }

        _npcs.Clear();
        _hunters.Clear();
    }

    /// <summary>A hora mudou: NPCs cujo horário começou entram; os que terminaram saem andando para fora da vista (somem).</summary>
    public void OnHourChanged(MapDef map)
    {
        if (map != _map || map.Dungeon)
        {
            return;
        }

        var clock = GameManager.Instance.State.Save.ClockMinutes;
        foreach (var npc in _npcs)
        {
            if (!npc.IsLeaving && !SeasonCalendar.IsWalkerPresent(npc.Def, clock))
            {
                npc.Leave();
            }
        }

        SpawnPresentWalkers(map);
    }

    public void Step(double dt)
    {
        if (_map is null)
        {
            return;
        }

        var area = city.World.WalkArea;
        for (var i = _npcs.Count - 1; i >= 0; i--)
        {
            if (!GodotObject.IsInstanceValid(_npcs[i]) || _npcs[i].IsQueuedForDeletion())
            {
                _npcs.RemoveAt(i);
            }
        }

        for (var i = _hunters.Count - 1; i >= 0; i--)
        {
            if (!GodotObject.IsInstanceValid(_hunters[i]))
            {
                _hunters.RemoveAt(i);
            }
        }

        // Quem está onde no começo do frame: cada um desvia dos outros e do jogador (B2-D48).
        _bodies.Clear();
        var player = city.World.Player.GlobalPosition;
        _bodies.Add(new MapPoint(player.X, player.Y));
        foreach (var npc in _npcs)
        {
            _bodies.Add(new MapPoint(npc.Position.X, npc.Position.Y));
        }

        foreach (var hunter in _hunters)
        {
            _bodies.Add(new MapPoint(hunter.Position.X, hunter.Position.Y));
        }

        for (var i = 0; i < _npcs.Count; i++)
        {
            _npcs[i].Step(area, dt, Random, _bodies, 1 + i, _planner);
        }

        for (var i = 0; i < _hunters.Count; i++)
        {
            _hunters[i].Step(area, _map.ImageWidth, _map.ImageHeight, dt, _bodies, 1 + _npcs.Count + i);
        }

        // world.js:154 — chat simulado.
        _chatT -= dt;
        if (_chatT <= 0)
        {
            _chatT = Random.Range(TownChat.Interval);
            var who = Random.Pick(GameManager.Db.Social.Hunters);
            Chat(who.Name, who.Rank, TownChat.RandomLine(GameManager.Db.Social, Random));
        }
    }

    /// <summary>
    /// Uma mensagem do chat de Lumeira: o sinal <c>ChatMessage</c> (log) e, se quem fala é um caçador presente neste mapa,
    /// o balão sobre ele. Fala de quem não está aqui (a Lúcia na Estalagem, um caçador em outro bairro) não sai na boca
    /// de outro (B2-D50).
    /// </summary>
    public void Chat(string who, string rank, string text)
    {
        EventBus.Instance.EmitSignal(EventBus.SignalName.ChatMessage, who, rank, text);
        TownHunter? speaker = null;
        foreach (var h in _hunters)
        {
            if (GodotObject.IsInstanceValid(h) && h.Brain.Def.Name == who)
            {
                speaker = h;
                break;
            }
        }

        if (speaker is not null && GodotObject.IsInstanceValid(speaker))
        {
            speaker.Brain.Speak(text);
        }
    }

    private void SpawnPresentWalkers(MapDef map)
    {
        var clock = GameManager.Instance.State.Save.ClockMinutes;
        foreach (var def in GameManager.Db.Npcs.Walkers)
        {
            if (def.Map != map.Id || !SeasonCalendar.IsWalkerPresent(def, clock) || HasActive(def.Id))
            {
                continue;
            }

            var npc = new TownNpc
            {
                Name = $"Npc_{def.Id}",
                Def = def,
                Route = RouteFor(map, def),
                Labels = city.Labels,
            };
            city.World.Entities.AddChild(npc);
            _npcs.Add(npc);
        }
    }

    private bool HasActive(string id)
    {
        foreach (var npc in _npcs)
        {
            if (GodotObject.IsInstanceValid(npc) && !npc.IsLeaving && npc.Def.Id == id)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Percurso do NPC neste mapa: calculado uma vez por área andável, numa thread (A* sobre a área carregada), e reaproveitado.</summary>
    private Task<WalkerRoute> RouteFor(MapDef map, WalkerDef def)
    {
        var key = $"{map.Id}:{map.Mask}:{map.MaskCell}:{def.Id}";
        if (!Routes.TryGetValue(key, out var task) || task.IsFaulted || task.IsCanceled)
        {
            var area = city.World.WalkArea;
            var path = def.Path;
            task = Task.Run(() => WalkerRoute.Build(area, path));
            Routes[key] = task;
        }

        return task;
    }
}
