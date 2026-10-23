using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Vespera.Core.Coop;
using Vespera.Core.Save;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Coop;

/// <summary>Um jogador do co-op local: número (0 = J1), dispositivo, classe e se a capa ganha outro tom (mesma classe de alguém antes).</summary>
public sealed record CoopMember(int Slot, InputDevice Device, HunterClass Hunter, bool CapeTint);

/// <summary>
/// Sessão do co-op local (fora das cenas, sobrevive à troca de sala): quem joga, com que dispositivo e classe, as regras
/// (<c>data/coop.json</c>) e a fábrica das fontes de input de cada um. O J1 é o dono do save (história e progressão); o J2
/// é o convidado (<see cref="GuestHunter"/>, no mesmo espaço). Sem co-op ativo tudo segue como no jogo solo.
/// </summary>
public static class CoopSession
{
    public const string RulesFile = "res://data/coop.json";

    private static readonly List<CoopMember> MembersList = [];
    private static CoopRules? _rules;

    public static CoopRules Rules => _rules ??= LoadRules();

    public static IReadOnlyList<CoopMember> Members => MembersList;

    /// <summary>Dois ou mais jogadores nesta sessão.</summary>
    public static bool Active => MembersList.Count >= 2;

    /// <summary>Jogadores para a escala dos monstros (1 sem co-op).</summary>
    public static int PlayerCount => Active ? MembersList.Count : 1;

    /// <summary>Alguém na metade direita e alguém no teclado: o J1 lê só a coluna Principal.</summary>
    public static bool KeyboardSplit =>
        MembersList.Any(m => m.Device.Kind == DeviceKind.KeyboardRight) && MembersList.Any(m => m.Device.Kind == DeviceKind.Keyboard);

    /// <summary>Grupo montado na tela de entrada, à espera do espaço de save (Título → Co-op local → espaço).</summary>
    public static CoopRoster? Pending { get; set; }

    /// <summary>Bots de teste: fonte de input crua de cada jogador no lugar do dispositivo (passa pelo comando do mesmo jeito).</summary>
    public static Func<CoopMember, IInputSource>? SourceOverride { get; set; }

    /// <summary>Entrou ou saiu alguém.</summary>
    public static event Action? Changed;

    /// <summary>Classes da tela de entrada, na ordem dos cartões (ids de arma do save).</summary>
    public static readonly string[] ClassIds = ["sword", "bow", "hammer"];

    public static CoopRoster NewRoster() => new(2, ClassIds);

    /// <summary>Começa (ou refaz) o grupo a partir da tela de entrada e prepara o convidado no save da sessão.</summary>
    public static void Begin(CoopRoster roster)
    {
        MembersList.Clear();
        for (var i = 0; i < roster.Count; i++)
        {
            var slot = roster.Slots[i];
            MembersList.Add(new CoopMember(i, slot.Device, RiftSession.HunterFor(slot.ClassId), roster.NeedsCapeTint(i)));
        }

        if (GameSession.Data is { } data && Active)
        {
            EnsureGuest(data, MembersList[1].Hunter);
        }

        Pending = null;
        Log.Info($"co-op: {string.Join(", ", MembersList.Select(m => $"{TagOf(m.Slot)} {m.Device} {m.Hunter}"))}");
        Changed?.Invoke();
    }

    public static void End()
    {
        if (MembersList.Count == 0)
        {
            return;
        }

        MembersList.Clear();
        Pending = null;
        PartyProgression.Reset();
        Changed?.Invoke();
    }

    public static CoopMember? Member(int slot) => slot >= 0 && slot < MembersList.Count ? MembersList[slot] : null;

    /// <summary>Convidado no save (cria na primeira vez) com a classe escolhida agora.</summary>
    public static GuestHunter EnsureGuest(SaveData data, HunterClass hunter)
    {
        data.Guest ??= new GuestHunter();
        data.Guest.ClassId = HunterSprites.WeaponId(hunter);
        return data.Guest;
    }

    /// <summary>Fonte de input do jogador: dispositivo dele → comando por passo (<see cref="CommandInputSource"/>).</summary>
    public static IInputSource CreateInput(CoopMember member)
    {
        var device = SourceOverride?.Invoke(member) ?? DeviceSource(member);
        return new CommandInputSource(new LocalCommandProvider(device, (byte)member.Slot));
    }

    /// <summary>Leitura crua do dispositivo com a tabela de teclas do jogador.</summary>
    public static IInputSource DeviceSource(CoopMember member) => member.Device.Kind switch
    {
        DeviceKind.KeyboardRight => new LocalInputSource(LocalInputSource.KeyboardMouse, () => InputSetup.Player2Table, [BindingSlot.Primary, BindingSlot.Alternate]),
        DeviceKind.Keyboard when KeyboardSplit => new LocalInputSource(LocalInputSource.KeyboardMouse, () => InputSetup.TableFor(member.Slot), [BindingSlot.Primary]),
        DeviceKind.Keyboard => new LocalInputSource(LocalInputSource.KeyboardMouse, () => InputSetup.TableFor(member.Slot), [BindingSlot.Primary, BindingSlot.Alternate]),
        _ => new LocalInputSource(member.Device.Pad, () => InputSetup.TableFor(member.Slot), [BindingSlot.Primary]),
    };

    /// <summary>Cor de cada jogador (contorno, etiqueta, painel da HUD, brilho do saque).</summary>
    public static Color ColorOf(int slot) => slot switch
    {
        0 => new Color("f2a541"),
        1 => new Color("62d6e8"),
        2 => new Color("9be07a"),
        _ => new Color("f08ac8"),
    };

    public static string TagOf(int slot) => $"J{slot + 1}";

    public static string DeviceName(InputDevice device, bool split) => device.Kind switch
    {
        DeviceKind.Keyboard => split ? "Teclado (esquerda)" : "Teclado",
        DeviceKind.KeyboardRight => "Teclado (direita)",
        _ => $"Controle {device.Pad + 1}",
    };

    private static CoopRules LoadRules()
    {
        var json = UserFile.ReadText(RulesFile);
        if (json is null)
        {
            Log.Error($"co-op: {RulesFile} ausente, usando padrões");
            return CoopRules.Defaults;
        }

        return CoopRules.Parse(json);
    }
}
