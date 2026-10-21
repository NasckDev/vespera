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
/// balão sobre um caçador do mapa (e emitido em <c>ChatMessage</c> para quem quiser um log). D41: além da entrada
/// no mapa (v2), a presença dos NPCs é revista a cada hora do relógio; quem chega aparece, quem sai some.
/// </summary>
public sealed class TownsfolkDirector(CityLife city)
{
    /// <summary>Percursos já calculados (ou em cálculo) por mapa e NPC; valem a sessão inteira.</summary>
    private static readonly Dictionary<string, Task<WalkerRoute>> Routes = [];

    private readonly List<TownNpc> _npcs = [];
    private readonly List<TownHunter> _hunters = [];
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
        var count = AmbientHunter.CountFor(map, state.Save.BrechasClosed, Random);
        var player = world.Player.GlobalPosition;
        foreach (var def in AmbientHunter.Pick(GameManager.Db.Social, state.Save.ClassId, count, Random))
        {
            var at = WalkMover.RandomPoint(world.WalkArea, map.ImageWidth, map.ImageHeight, Random, player.X, player.Y, minDistance: 70f);
            var hunter = new TownHunter
            {
                Name = $"Hunter_{def.Name}",
                Brain = new AmbientHunter(def, at.X, at.Y, GameManager.Db.Social, Random),
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
            var npc = _npcs[i];
            if (!GodotObject.IsInstanceValid(npc) || npc.IsQueuedForDeletion())
            {
                _npcs.RemoveAt(i);
                continue;
            }

            npc.Step(area, dt, Random);
        }

        for (var i = _hunters.Count - 1; i >= 0; i--)
        {
            var hunter = _hunters[i];
            if (!GodotObject.IsInstanceValid(hunter))
            {
                _hunters.RemoveAt(i);
                continue;
            }

            hunter.Step(area, _map.ImageWidth, _map.ImageHeight, dt);
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
    /// Uma mensagem do chat de Lumeira: balão sobre o caçador com esse nome (ou outro caçador do mapa) e o sinal
    /// <c>ChatMessage</c>.
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

        if (speaker is null && _hunters.Count > 0)
        {
            speaker = _hunters[Random.RangeInt(0, _hunters.Count - 1)];
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

    /// <summary>Percurso do NPC neste mapa: calculado uma vez, numa thread (BFS da máscara), e reaproveitado.</summary>
    private Task<WalkerRoute> RouteFor(MapDef map, WalkerDef def)
    {
        var key = map.Id + ":" + def.Id;
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
