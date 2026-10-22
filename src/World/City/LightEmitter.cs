using System.Collections.Generic;
using Godot;

namespace Vespera.World.City;

/// <summary>
/// Luz dinâmica da v2 (spec 01 §4.5, <c>lightsList</c>): portal aberto, portal de saída, porta liberada, projéteis,
/// glow de monstros, picks… Ponha um <see cref="LightEmitter"/> como filho do nó que brilha (ele segue o pai) e a
/// camada de iluminação da cidade fura a escuridão e soma o glow colorido. Registrar e sair do registro acontecem
/// em <c>_EnterTree</c>/<c>_ExitTree</c>; a iluminação lê a lista sem alocar.
/// </summary>
[GlobalClass]
public partial class LightEmitter : Node2D
{
    private static readonly List<LightEmitter> ActiveList = [];

    /// <summary>Raio em u (o furo na escuridão tem 1,6× isso; o glow, 0,7×).</summary>
    [Export]
    public float Radius { get; set; } = 46f;

    /// <summary>Tipo de cor de <c>LIGHTC</c>: <c>a</c> âmbar, <c>f</c> fogo, <c>m</c> magenta, <c>c</c> ciano, <c>g</c> verde.</summary>
    [Export]
    public string Kind { get; set; } = "a";

    /// <summary>Multiplicador de intensidade (0 apaga sem tirar do registro). Bom para luzes que acendem/somem.</summary>
    [Export]
    public float Intensity { get; set; } = 1f;

    /// <summary>Multiplica só o glow colorido (não o furo): para luzes que têm que dominar a cena, como o portal (B2-D51).</summary>
    [Export]
    public float GlowBoost { get; set; } = 1f;

    /// <summary>Emissores dentro da árvore, na ordem em que entraram.</summary>
    public static IReadOnlyList<LightEmitter> Active => ActiveList;

    public override void _EnterTree() => ActiveList.Add(this);

    public override void _ExitTree() => ActiveList.Remove(this);
}
