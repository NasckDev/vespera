using System;
using System.Collections.Generic;
using Godot;

namespace Vespera.World.City;

/// <summary>
/// Textos sobre o mundo em espaço de tela (spec 01 §4.7, <c>drawOverlayWorld</c>): nome dos NPCs (âmbar), nome e Selo
/// dos caçadores, balões de fala/chat e bairro + timer dos portais. Ficam numa camada acima da escuridão, como na v2,
/// e usam um pool de <see cref="Label"/> por estilo (nada é criado por frame; quem aluga devolve em <c>_ExitTree</c>).
/// </summary>
public partial class WorldLabels : CanvasLayer
{
    public enum Style
    {
        NpcName,
        HunterName,
        HunterRank,
        Bubble,
        PortalTitle,
        PortalTimer,
    }

    private const int StyleCount = 6;

    /// <summary>Largura máxima de um balão antes de quebrar a linha (px da tela base).</summary>
    public const float BubbleMaxWidth = 150f;

    private static readonly Color NpcNameColor = new("#F2A541");
    private static readonly Color HunterNameColor = new("#d8cfe0");
    private static readonly Color Outline = new(0.04f, 0.03f, 0.08f, 0.9f);

    private readonly Stack<Label>[] _free = new Stack<Label>[StyleCount];
    private readonly LabelSettings[] _settings = new LabelSettings[StyleCount];
    private StyleBoxFlat _bubbleBox = null!;

    /// <summary>Quantos rótulos estão alugados agora (testes).</summary>
    public int InUse { get; private set; }

    /// <summary>
    /// C1-D7: rótulos do mundo nunca por cima do HUD. Depois que os donos posicionaram os rótulos no quadro
    /// (prioridade alta = roda por último), quem encosta numa zona do HUD (<see cref="UI.Common.HudZones"/>) some
    /// rápido e volta quando sai. Só percorre os filhos do pool; nada aloca.
    /// </summary>
    public override void _Process(double delta)
    {
        var step = (float)Math.Min(1.0, delta * 14.0);
        var count = GetChildCount();
        for (var i = 0; i < count; i++)
        {
            if (GetChild(i) is not Label { Visible: true } label)
            {
                continue;
            }

            var target = UI.Common.HudZones.Overlaps(label.GetGlobalRect()) ? 0f : 1f;
            var a = label.SelfModulate.A;
            if (!Mathf.IsEqualApprox(a, target))
            {
                label.SelfModulate = Colors.White with { A = MathF.Abs(target - a) < 0.02f ? target : Mathf.Lerp(a, target, step) };
            }
        }
    }

    /// <summary>O rótulo está escondido por encostar no HUD (smoke test).</summary>
    public static bool HiddenByHud(Label label) => label.SelfModulate.A < 0.05f;

    public override void _Ready()
    {
        ProcessPriority = 100; // depois de NPCs, caçadores e portais moverem os rótulos
        Layer = 6; // acima da escuridão (5), abaixo do HUD (10)
        // Em tamanho pequeno o espaço da Pixelify/Cinzel arredonda para quase nada: alarga um pouco.
        var pixel = Spaced(GD.Load<FontFile>("res://assets/fonts/PixelifySans.ttf"), 2);
        var cinzel = Spaced(GD.Load<FontFile>("res://assets/fonts/Cinzel.ttf"), 1);
        _settings[(int)Style.NpcName] = Settings(pixel, 8, NpcNameColor);
        _settings[(int)Style.HunterName] = Settings(pixel, 8, HunterNameColor);
        _settings[(int)Style.HunterRank] = Settings(pixel, 8, Colors.White);
        _settings[(int)Style.Bubble] = new LabelSettings { Font = pixel, FontSize = 8, FontColor = new Color("#F4E7C6") };
        _settings[(int)Style.PortalTitle] = Settings(cinzel, 8, new Color("#F4E7C6"));
        _settings[(int)Style.PortalTimer] = Settings(pixel, 8, Colors.White);
        for (var i = 0; i < StyleCount; i++)
        {
            _free[i] = new Stack<Label>();
        }

        // Balão rgba(14,11,24,.85) (render.js:222).
        _bubbleBox = new StyleBoxFlat
        {
            BgColor = new Color(14 / 255f, 11 / 255f, 24 / 255f, 0.85f),
            ContentMarginLeft = 3,
            ContentMarginRight = 3,
            ContentMarginTop = 1,
            ContentMarginBottom = 1,
        };
    }

    /// <summary>Aluga um rótulo visível do estilo pedido.</summary>
    public Label Rent(Style style)
    {
        var free = _free[(int)style];
        var label = free.Count > 0 ? free.Pop() : Create(style);
        label.Visible = true;
        label.Modulate = Colors.White;
        InUse++;
        return label;
    }

    /// <summary>Devolve um rótulo ao pool (null é ignorado).</summary>
    public void Return(Label? label)
    {
        if (label is null || !IsInstanceValid(label))
        {
            return;
        }

        label.Visible = false;
        label.Text = string.Empty;
        _free[(int)label.GetMeta(StyleMeta).AsInt32()].Push(label);
        InUse = Math.Max(0, InUse - 1);
    }

    /// <summary>Ponto do mundo → ponto da tela (mesma conta do <c>w2s</c> da v2).</summary>
    public static Vector2 ToScreen(CanvasItem anyWorldNode, Vector2 worldPoint) =>
        anyWorldNode.GetViewport().GetCanvasTransform() * worldPoint;

    /// <summary>Troca o texto e ajusta o tamanho (o rótulo só muda quando o texto muda).</summary>
    public static void SetText(Label label, string text)
    {
        if (label.Text == text)
        {
            return;
        }

        label.Text = text;
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        label.CustomMinimumSize = Vector2.Zero;
        label.ResetSize();
        if (label.GetMeta(StyleMeta).AsInt32() == (int)Style.Bubble && label.Size.X > BubbleMaxWidth)
        {
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(BubbleMaxWidth, 0);
            label.ResetSize();
        }
    }

    /// <summary>
    /// Posiciona o rótulo com o centro da base em <paramref name="screenPoint"/> (arredondado ao px). Balões são mantidos
    /// dentro da tela para não cortar a fala na borda.
    /// </summary>
    public static void PlaceCentered(Label label, Vector2 screenPoint)
    {
        var size = label.Size;
        var x = screenPoint.X - (size.X / 2f);
        if (label.GetMeta(StyleMeta).AsInt32() == (int)Style.Bubble)
        {
            var view = label.GetViewportRect().Size;
            x = Math.Clamp(x, 2f, MathF.Max(2f, view.X - size.X - 2f));
        }

        label.Position = new Vector2(MathF.Round(x), MathF.Round(screenPoint.Y - size.Y));
    }

    private static readonly StringName StyleMeta = "label_style";

    private Label Create(Style style)
    {
        var label = new Label
        {
            LabelSettings = _settings[(int)style],
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false,
        };
        label.SetMeta(StyleMeta, (int)style);
        if (style == Style.Bubble)
        {
            label.AddThemeStyleboxOverride("normal", _bubbleBox);
        }

        AddChild(label);
        return label;
    }

    private static FontVariation Spaced(Font font, int space)
    {
        var variation = new FontVariation { BaseFont = font };
        variation.SetSpacing(TextServer.SpacingType.Space, space);
        return variation;
    }

    private static LabelSettings Settings(Font font, int size, Color color) => new()
    {
        Font = font,
        FontSize = size,
        FontColor = color,
        OutlineSize = 2,
        OutlineColor = Outline,
    };
}
