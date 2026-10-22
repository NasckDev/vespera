using System;
using Godot;
using Vespera.Autoload;

namespace Vespera.UI.Common;

/// <summary>
/// Escala da interface e tamanho do texto (frente C1, DECISIONS C1-D4). A escala vale para um <see cref="Control"/>
/// raiz sem âncoras: ele recebe <c>Scale = s</c> e o tamanho da área visível dividido por <c>s</c>, então tudo que está
/// ancorado dentro dele continua nos cantos certos, só maior. O texto é rasterizado no tamanho final
/// (<see cref="Viewport.OversamplingOverride"/>), para não borrar em 125/150%. O tamanho do texto vale só para os
/// rótulos de leitura marcados com <see cref="Reading"/> (diálogo, missão, notas, legendas, dicas): eles guardam o
/// tamanho base e são recalculados ao mudar a opção (nunca por quadro).
/// </summary>
public static class UiScaling
{
    private static readonly StringName BaseMeta = "c1_text_base";
    private static readonly StringName FontSize = "font_size";

    /// <summary>
    /// Fator inteiro da janela (px de tela por u; stretch <c>canvas_items</c> com escala inteira): janela ÷ área visível,
    /// que acompanha a base que o <see cref="WindowFit"/> pede ao engine (POLI-11).
    /// </summary>
    public static float WindowFactor(Node node)
    {
        var window = node.GetTree().Root.Size;
        var visible = node.GetViewport().GetVisibleRect().Size;
        if (visible.X <= 0f || visible.Y <= 0f)
        {
            return 1f;
        }

        return MathF.Max(1f, MathF.Round(MathF.Min(window.X / visible.X, window.Y / visible.Y)));
    }

    /// <summary>Ajusta <paramref name="root"/> à área visível na escala <paramref name="scale"/>.</summary>
    public static void Fit(Control root, float scale)
    {
        if (!root.IsInsideTree())
        {
            return;
        }

        var visible = root.GetViewport().GetVisibleRect();
        root.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        root.Scale = new Vector2(scale, scale);
        root.Position = visible.Position;
        root.Size = visible.Size / scale;
    }

    /// <summary>Escala que cabe: até <paramref name="wanted"/>, sem deixar um conteúdo de <paramref name="design"/> u sair da tela.</summary>
    public static float Fitting(Control any, float wanted, Vector2 design)
    {
        if (design.X <= 0 || design.Y <= 0 || !any.IsInsideTree())
        {
            return wanted;
        }

        var visible = any.GetViewport().GetVisibleRect().Size;
        return Math.Clamp(MathF.Min(wanted, MathF.Min(visible.X / design.X, visible.Y / design.Y)), 1f, wanted);
    }

    /// <summary>Texto nítido na escala maior (0 = o padrão do Godot, igual à escala da janela).</summary>
    public static void ApplyOversampling(Node any)
    {
        if (!any.IsInsideTree())
        {
            return;
        }

        var scale = GameSettings.UiScale;
        any.GetTree().Root.OversamplingOverride = scale > 1.001f ? WindowFactor(any) * scale : 0f;
    }

    /// <summary>Marca o rótulo como texto de leitura (segue "Tamanho do texto") e aplica o tamanho atual.</summary>
    public static T Reading<T>(T label)
        where T : Control
    {
        if (!label.HasMeta(BaseMeta))
        {
            label.SetMeta(BaseMeta, label.GetThemeFontSize(FontSize));
        }

        ApplyOne(label, GameSettings.TextScale);
        return label;
    }

    /// <summary>Tamanho de leitura para quem desenha texto sozinho (legendas, dicas desenhadas).</summary>
    public static int ReadingSize(int baseSize) => Math.Max(baseSize, (int)MathF.Round(baseSize * GameSettings.TextScale));

    /// <summary>Recalcula todos os rótulos de leitura debaixo de <paramref name="root"/> (opção mudou).</summary>
    public static void ApplyText(Node root)
    {
        var scale = GameSettings.TextScale;
        Walk(root, scale);
    }

    private static void Walk(Node node, float scale)
    {
        if (node is Control c && c.HasMeta(BaseMeta))
        {
            ApplyOne(c, scale);
        }

        var count = node.GetChildCount();
        for (var i = 0; i < count; i++)
        {
            Walk(node.GetChild(i), scale);
        }
    }

    private static void ApplyOne(Control label, float scale)
    {
        var size = label.GetMeta(BaseMeta).AsInt32();
        var scaled = Math.Max(size, (int)MathF.Round(size * scale));
        if (label is RichTextLabel)
        {
            label.AddThemeFontSizeOverride("normal_font_size", scaled);
        }
        else
        {
            label.AddThemeFontSizeOverride(FontSize, scaled);
        }
    }
}

/// <summary>
/// Retângulos da tela ocupados pelo HUD (frente C1, DECISIONS C1-D7), em coordenadas da tela base. O HUD publica a
/// cada quadro (sem alocar); quem desenha no mundo em espaço de tela (nomes, balões, títulos de portal) some onde
/// encostar neles, e o minimapa do canto usa <see cref="MinimapSlot"/>.
/// </summary>
public static class HudZones
{
    public const int Capacity = 16; // POLI-12: 7 blocos fixos + chefe + onda + até 4 linhas de loot = 13

    private static readonly Rect2[] Rects = new Rect2[Capacity];

    public static int Count { get; private set; }

    /// <summary>Onde o minimapa do canto deve ficar (vazio = posição padrão do motor).</summary>
    public static Rect2 MinimapSlot { get; set; }

    /// <summary>
    /// Altura (u da tela) que a hotbar ocupa no pé da tela, 0 sem HUD. A câmera deixa esta folga além da borda de baixo do
    /// mapa para o jogador que anda até a borda não ficar escondido atrás dela (POLI-9).
    /// </summary>
    public static float BottomInset { get; set; }

    public static Rect2 Get(int i) => Rects[i];

    public static void Clear() => Count = 0;

    public static void Add(Rect2 rect)
    {
        if (Count < Capacity && rect.Size.X > 0 && rect.Size.Y > 0)
        {
            Rects[Count++] = rect;
        }
    }

    /// <summary>O retângulo encosta em alguma zona do HUD.</summary>
    public static bool Overlaps(Rect2 rect)
    {
        for (var i = 0; i < Count; i++)
        {
            if (Rects[i].Intersects(rect))
            {
                return true;
            }
        }

        return false;
    }
}
