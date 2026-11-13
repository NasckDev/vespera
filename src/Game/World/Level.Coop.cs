using System.Collections.Generic;
using System.Linq;
using Godot;
using Vespera.Core.Advancement;
using Vespera.Core.Coop;
using Vespera.Core.Story;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.Combat;
using Vespera.Game.Coop;
using Vespera.Game.Story;
using Vespera.Game.UI;
using Vespera.Game.World.Rift;

namespace Vespera.Game.World;

/// <summary>
/// Co-op local dentro da cena de jogo (PLANO-V2 camada 8): liga cada caçador às interações e à queda, reanima, aplica a
/// Lanterna quando o grupo inteiro cai (uma caçada só; os Fragmentos de cada um ficam na Lanterna dele e todos acordam na
/// Praça), deixa o J2 entrar e sair pelo menu de pausa fora da Brecha e lê a pausa do J2 pela fonte de input dele.
/// </summary>
public partial class Level
{
    private readonly Dictionary<int, int> _carriedHps = [];
    private CoopOverlay _coopOverlay = null!;
    private Player? _readingPlayer;

    /// <summary>Caçadores da sala (J1 primeiro).</summary>
    public IReadOnlyList<Player> Players => World.Players;

    /// <summary>Mais de um caçador nesta sala.</summary>
    public bool IsCoop => World.Players.Count > 1;

    /// <summary>Dá para chamar ou dispensar o J2 agora (fora da Brecha, sem troca de sala nem conversa).</summary>
    public bool CanChangeParty => !RiftSession.InRift && !_switching && !DialogueOpen && !_sceneBusy;

    /// <summary>
    /// Monta o grupo a partir da tela de entrada (J2 chegando no meio do jogo): o J1 passa a ler só o dispositivo dele e o
    /// J2 nasce ao lado, com o contorno e a etiqueta.
    /// </summary>
    public void ApplyParty(CoopRoster roster)
    {
        CoopSession.Begin(roster);
        var lead = CoopSession.Members[0];
        Player.Input = CoopSession.CreateInput(lead);
        Player.ApplyIdentity(CoopSession.ColorOf(0), false);
        foreach (var member in CoopSession.Members.Skip(1))
        {
            if (World.PlayerAt(member.Slot) is null)
            {
                World.AddPartyMember(member);
            }
        }

        World.Camera.Snap();
        CombatHud.Toast($"{CoopSession.TagOf(1)} entrou no co-op! ({CoopSession.DeviceName(CoopSession.Members[1].Device, CoopSession.KeyboardSplit)})");
        GameSession.Autosave();
    }

    /// <summary>
    /// Co-op online (este PC hospeda): o convidado aceito pela rede nasce ao lado do J1 com a classe que escolheu; o J1
    /// segue lendo todos os dispositivos locais e o J2 lê os comandos que chegam pela rede.
    /// </summary>
    public void ApplyOnlineGuest(int slot, string classId)
    {
        var roster = CoopSession.NewRoster();
        var lead = roster.Join(InputDevice.Keyboard, locked: true)!;
        lead.ClassId = HunterSprites.WeaponId(Player.Hunter);
        lead.Ready = true;
        var guest = roster.Join(InputDevice.Network(slot))!;
        guest.ClassId = classId;
        guest.Ready = true;
        ApplyParty(roster);
        Log.Info($"online: {CoopSession.TagOf(slot)} nasceu na sala '{World.Data.Id}' ({classId})");
    }

    /// <summary>O convidado online saiu ou caiu: o host segue sozinho, com aviso na HUD.</summary>
    public void LeaveOnline(string toast)
    {
        LeaveParty();
        CombatHud.Toast(toast);
    }

    /// <summary>J2 sai (menu de pausa): volta ao jogo solo, com o J1 em todos os dispositivos de novo.</summary>
    public void LeaveParty()
    {
        foreach (var p in World.Players.Where(p => p.Slot > 0).ToList())
        {
            World.RemovePartyMember(p);
        }

        CoopSession.End();
        Player.Input = MergedInput.LocalSolo();
        Player.ClearIdentity();
        World.Camera.Snap();
        GameSession.Autosave();
        CombatHud.Toast("O co-op terminou. Até a próxima caçada!");
    }

    private void BuildCoop()
    {
        _coopOverlay = new CoopOverlay(this) { Name = "CoopOverlay" };
        AddChild(_coopOverlay);
        PartyProgression.GuestRankChanged += OnGuestRankChanged;
    }

    private void UnbindCoop() => PartyProgression.GuestRankChanged -= OnGuestRankChanged;

    private void BindParty(LevelWorld world)
    {
        foreach (var p in world.Players)
        {
            BindPlayer(p);
        }

        world.PlayerAdded += BindPlayer;
        world.Party.Revived += OnRevived;
        world.Party.Wiped += OnPartyWiped;
    }

    private void BindPlayer(Player p)
    {
        p.InteractRequested += OnInteract;
        p.Combat.Died += OnPlayerDied;
        p.Combat.Downed += OnPlayerDowned;
        p.Combat.CanGoDown = () => RiftSession.InRift && IsInstanceValid(World) && World.Players.Count > 1;
    }

    private void SetPartyFrozen(bool frozen)
    {
        foreach (var p in World.Players)
        {
            if (IsInstanceValid(p))
            {
                p.Frozen = frozen;
            }
        }
    }

    /// <summary>Quem mostra o aviso de interação: o primeiro caçador de pé com algo ao alcance (J1 tem preferência).</summary>
    private Player? FocusedPlayer()
    {
        foreach (var p in World.Players)
        {
            if (IsInstanceValid(p) && p.Focus is not null && !p.Combat.IsDead)
            {
                return p;
            }
        }

        return null;
    }

    /// <summary>Nome do botão de interagir para a dica da conversa: no co-op, o de cada jogador ("E / Enter").</summary>
    private string PartyInteractKeys()
    {
        if (!IsCoop)
        {
            return InteractionHud.InteractKeyName((Player.Input as MergedInput)?.DeviceId ?? Player.Input.DeviceId);
        }

        return string.Join(" / ", World.Players.Select(p => InputGlyphs.Describe(p.Input, InputActions.Interact)).Distinct());
    }

    /// <summary>
    /// Pausa pedida pelo J2 (a fonte dele não passa pela InputMap). Lida no passo de física, o mesmo em que o comando é
    /// amostrado: num quadro lento com dois passos, o aperto não se perde.
    /// </summary>
    public override void _PhysicsProcess(double delta) => PollGuestPause();

    private void PollGuestPause()
    {
        if (!IsCoop || !SceneRouter.Instance.IsActive(this) || DialogueOpen || _switching)
        {
            return;
        }

        foreach (var p in World.Players)
        {
            if (p.Slot > 0 && IsInstanceValid(p) && p.Input.IsJustPressed(InputActions.Pause))
            {
                OpenPause();
                return;
            }
        }
    }

    private void OpenPause()
    {
        if (_reading is not null)
        {
            CloseReading();
            return;
        }

        GetTree().Paused = true;
        SceneRouter.Instance.Push(ScreenKeys.Pause);
    }

    private void OnPlayerDowned(HunterCombat combat)
    {
        // Sopa da Véspera (Ato VI): no co-op a segunda vida da caçada levanta quem cair primeiro, ali mesmo.
        var rise = Hunter.HuntService.UseSecondLife(combat.MaxHp);
        if (rise > 0)
        {
            combat.ReviveFromDown(rise);
            CombatHud.Toast($"A sopa da Nilza levanta o {CoopSession.TagOf(combat.Player.Slot)}. Só uma vez.");
            return;
        }

        var tag = CoopSession.TagOf(combat.Player.Slot);
        var helper = World.Players.FirstOrDefault(p => p != combat.Player && !p.Combat.IsDead);
        CombatHud.Toast(helper is null
            ? $"{tag} caiu!"
            : $"{tag} caiu! {CoopSession.TagOf(helper.Slot)}: segure {InputGlyphs.Describe(helper.Input, InputActions.Interact)} perto para reanimar.");
    }

    private void OnRevived(Player downed, Player? helper) =>
        CombatHud.Toast($"{CoopSession.TagOf(downed.Slot)} de pé de novo{(helper is null ? string.Empty : $", graças ao {CoopSession.TagOf(helper.Slot)}")}!");

    /// <summary>Todos caíram: uma caçada só, regra da Lanterna para cada um; o grupo acorda na Praça.</summary>
    private void OnPartyWiped()
    {
        if (!RiftSession.InRift || _switching)
        {
            return;
        }

        var (end, summary) = RiftSession.EndRunWithSummary(survived: false, completed: false);
        var lead = end?.FragmentosLeft ?? 0;
        var guest = RiftSession.GuestEnd?.FragmentosLeft ?? 0;
        _carriedHp = null;
        _carriedHps.Clear();
        GameSession.Autosave();
        var lost = lead + guest > 0
            ? $"O grupo caiu. Na Lanterna, dentro da Brecha: {CoopSession.TagOf(0)} {lead}, {CoopSession.TagOf(1)} {guest} Fragmentos."
            : "O grupo caiu. A Brecha cuspiu vocês de volta na Praça.";
        _ = ReturnToHub(lost, "A Lanterna ficou para trás", summary, false);
    }

    private void OnGuestRankChanged(int slot, RankChange change) =>
        CombatHud.Toast($"{CoopSession.TagOf(slot)} subiu para o rank {change.To}!");

    /// <summary>Vida de cada caçador levada para a próxima sala (caído levanta com a vida da reanimação).</summary>
    private void CarryPartyHp()
    {
        _carriedHps.Clear();
        foreach (var p in World.Players)
        {
            _carriedHps[p.Slot] = p.Combat.IsDowned ? CoopSession.Rules.RevivedHp(p.Combat.MaxHp) : p.Combat.Hp;
        }
    }

    private void RestorePartyHp()
    {
        foreach (var p in World.Players)
        {
            if (_carriedHps.TryGetValue(p.Slot, out var hp))
            {
                p.Combat.SetHp(hp);
            }
        }
    }

    /// <summary>
    /// Entrar na Brecha <paramref name="brechaId"/> em grupo: o J1 com a arma equipada (como no solo) e o J2 com a classe
    /// que escolheu na entrada.
    /// </summary>
    private void EnterRiftAsParty(string brechaId)
    {
        // Os dois podem apertar no mesmo quadro (ou de novo durante o fade): só a primeira entrada começa a caçada.
        if (_switching || RiftSession.InRift)
        {
            return;
        }

        if (!StoryService.Engine.HasFlag(StoryFlags.StarterBlade))
        {
            ShowNotice("A fenda pulsa, mas vocês estão de mãos vazias. Fale com a Odete, na Forja: atravessem a ponte a oeste.");
            return;
        }

        RiftSession.Select(brechaId);
        _hubSpawn = World.Player.GlobalPosition + new Vector2(0, PortalStepBack * World.Data.ActorScale);
        EnterRift(EquippedHunter);
    }
}
