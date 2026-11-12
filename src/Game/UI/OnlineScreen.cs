using System.Collections.Generic;
using Godot;
using Vespera.Core.Net;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.Coop;
using Vespera.Game.Net;

namespace Vespera.Game.UI;

/// <summary>
/// Co-op online (título e pausa): "Hospedar" abre a porta e mostra o IP local e a porta para o amigo; "Entrar" pede IP,
/// porta e classe e conecta. Na pausa (já em jogo) só existe "Hospedar": o convidado nasce ao lado quando a sala deixar.
/// </summary>
public partial class OnlineScreen(bool inGame) : UiScreen
{
    private const int FieldWidth = 520;

    private Button _host = null!;
    private Button _stopHost = null!;
    private Button? _play;
    private Label _hostInfo = null!;
    private LineEdit? _address;
    private LineEdit? _port;
    private Button? _classButton;
    private Button? _join;
    private Label? _joinInfo;
    private int _classIndex = 1;

    public bool InGame => inGame;

    public string HostInfo => _hostInfo.Text;

    public string JoinInfo => _joinInfo?.Text ?? string.Empty;

    public string ClassId => CoopSession.ClassIds[_classIndex];

    protected override Control? DefaultFocus() => _host.Visible ? _host : _stopHost;

    protected override void Build()
    {
        var panel = BuildPage("Online", out _);
        var scroll = UiKit.MakeScroll();
        panel.AddChild(scroll);
        var column = UiKit.MakeColumn(14);
        column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(UiKit.PadForGlow(column));
        var buttons = new List<Control>();

        column.AddChild(UiKit.MakeLabel("Hospedar", UiTheme.TitleVariation));
        column.AddChild(UiKit.MakeWrapped(inGame
            ? "Seu jogo vira a partida: o amigo entra ao seu lado quando vocês estiverem fora da Brecha."
            : "Abre a porta para um amigo. Depois escolha o espaço de save; ele entra ao seu lado na cidade.", UiTheme.DimVariation));
        _host = Add(column, buttons, "Hospedar", StartHosting);
        _stopHost = Add(column, buttons, "Parar de hospedar", StopHosting);
        if (!inGame)
        {
            _play = Add(column, buttons, "Escolher o espaço de save", () =>
                SceneRouter.Instance.Push(SaveService.HasAnySave() ? ScreenKeys.SlotsContinue : ScreenKeys.SlotsNew));
        }

        _hostInfo = UiKit.MakeWrapped(string.Empty);
        column.AddChild(_hostInfo);

        if (!inGame)
        {
            BuildJoin(column, buttons);
        }

        Add(column, buttons, "Voltar", Back);
        UiKit.WrapFocus(buttons);
        Refresh();
        if (NetSession.Instance is { } session)
        {
            session.JoinResult += OnJoinResult;
        }
    }

    public override void _ExitTree()
    {
        if (NetSession.Instance is { } session)
        {
            session.JoinResult -= OnJoinResult;
        }
    }

    private void BuildJoin(VBoxContainer column, List<Control> buttons)
    {
        column.AddChild(UiKit.MakeLabel("Entrar", UiTheme.TitleVariation));
        column.AddChild(UiKit.MakeWrapped("Digite o IP e a porta que o host mostra em \"Hospedar\" e escolha a classe.", UiTheme.DimVariation));
        var row = UiKit.MakeRow(12);
        _address = new LineEdit { PlaceholderText = "IP do host (ex.: 192.168.0.10)", CustomMinimumSize = new Vector2(FieldWidth, 0), Text = "127.0.0.1" };
        _port = new LineEdit { PlaceholderText = "Porta", CustomMinimumSize = new Vector2(180, 0), Text = CoopSession.Rules.Online.Port.ToString() };
        row.AddChild(_address);
        row.AddChild(_port);
        column.AddChild(row);
        buttons.Add(_address);
        buttons.Add(_port);
        _classButton = Add(column, buttons, ClassText(), () =>
        {
            _classIndex = (_classIndex + 1) % CoopSession.ClassIds.Length;
            _classButton!.Text = ClassText();
        });
        _join = Add(column, buttons, "Entrar", StartJoin);
        _joinInfo = UiKit.MakeWrapped(string.Empty);
        column.AddChild(_joinInfo);
    }

    /// <summary>Bots e testes: preenche os campos como um jogador digitaria.</summary>
    public void Fill(string address, string port, string classId)
    {
        if (_address is null || _port is null)
        {
            return;
        }

        _address.Text = address;
        _port.Text = port;
        var index = System.Array.IndexOf(CoopSession.ClassIds, classId);
        _classIndex = index >= 0 ? index : _classIndex;
        _classButton!.Text = ClassText();
    }

    private string ClassText() => $"Classe: {HunterSprites.DisplayName(World.Rift.RiftSession.HunterFor(ClassId))}";

    private static Button Add(Container parent, List<Control> all, string text, System.Action onPressed)
    {
        var button = UiKit.MakeButton(text, onPressed, 0, UiTheme.MenuButtonVariation);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        parent.AddChild(button);
        all.Add(button);
        return button;
    }

    private void StartHosting()
    {
        if (NetSession.Instance is not { } session)
        {
            return;
        }

        session.Host(CoopSession.Rules.Online.Port);
        Refresh();
        _stopHost.GrabFocus();
    }

    private void StopHosting()
    {
        NetSession.Instance?.Stop();
        Refresh();
        _host.GrabFocus();
    }

    private void StartJoin()
    {
        if (NetSession.Instance is not { } session || _address is null || _port is null || _joinInfo is null)
        {
            return;
        }

        if (!NetProtocol.IsValidAddress(_address.Text))
        {
            _joinInfo.Text = "Endereço inválido. Use o IP que o host mostra (ex.: 192.168.0.10).";
            return;
        }

        if (!NetProtocol.TryParsePort(_port.Text, out var port))
        {
            _joinInfo.Text = "Porta inválida: use um número de 1024 a 65535.";
            return;
        }

        session.Join(_address.Text, port, ClassId);
        _joinInfo.Text = session.Status;
        Refresh();
    }

    private void OnJoinResult(bool ok, string text)
    {
        if (_joinInfo is not null && IsInstanceValid(_joinInfo))
        {
            _joinInfo.Text = text;
        }

        Refresh();
    }

    private void Refresh()
    {
        var session = NetSession.Instance;
        var hosting = session?.Role == NetRole.Host;
        var guest = session?.Role == NetRole.Guest;
        _host.Visible = !hosting;
        _host.Disabled = guest;
        _stopHost.Visible = hosting;
        if (_play is not null)
        {
            _play.Visible = hosting;
        }

        _hostInfo.Text = hosting
            ? $"{session!.Status}\nO amigo digita o IP e a porta {session.Port} em \"Entrar\". Pela internet, abra a porta {session.Port} (UDP) no roteador e no firewall e passe o seu IP público."
            : string.Empty;
        if (_join is not null)
        {
            _join.Disabled = hosting || guest;
        }
    }

    protected override void Back()
    {
        // Saindo da tela sem jogo e sem ter entrado: desfaz uma conexão em andamento.
        if (NetSession.Instance is { Role: NetRole.Guest, GuestState: not GuestState.Joined } session)
        {
            session.Stop();
        }

        SceneRouter.Instance.Pop();
    }
}
