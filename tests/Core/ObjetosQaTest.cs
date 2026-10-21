namespace Vespera.Tests.Core;

using System.Buffers.Binary;
using GdUnit4;
using Vespera.Core.World;
using static GdUnit4.Assertions;

/// <summary>
/// Testes do tester da frente B5 (biblioteca de objetos): o que o objects.json promete bate com os arquivos de verdade
/// (cabeçalho PNG, .import, orçamento de textura) e os tamanhos são críveis ao lado do jogador.
/// </summary>
[TestSuite]
public class ObjetosQaTest
{
    private static readonly Lazy<ObjectCatalog> Catalog = new(() =>
        ObjectCatalog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "objects.json"))));

    private static readonly Lazy<string> Root = new(() =>
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vespera.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Vespera.sln não encontrado acima da pasta do teste.");
    });

    [TestCase]
    public void PngHeaderMatchesCatalogPixels()
    {
        var wrong = new List<string>();
        foreach (var o in Catalog.Value.Objects)
        {
            var (w, h, depth, color) = ReadIhdr(PathOf(o.File));
            if (w != o.PixelWidth || h != o.PixelHeight)
            {
                wrong.Add($"{o.Id}: PNG {w}×{h}, objects.json {o.PixelWidth}×{o.PixelHeight}");
            }

            if (depth != 8 || color != 6)
            {
                wrong.Add($"{o.Id}: PNG com profundidade {depth} e tipo de cor {color} (esperado RGBA 8 bits)");
            }
        }

        AssertThat(wrong.Count).OverrideFailureMessage(string.Join("\n", wrong)).IsEqual(0);
    }

    [TestCase]
    public void EverySpriteIsImportedLosslessWithMipmaps()
    {
        // Os objetos são desenhados com escala 1/density (0,23 a 0,6) e filtro LinearWithMipmaps: sem mipmap serrilha, com perda borra.
        var wrong = new List<string>();
        foreach (var o in Catalog.Value.Objects)
        {
            var import = PathOf(o.File) + ".import";
            if (!File.Exists(import))
            {
                wrong.Add($"{o.Id}: sem {Path.GetFileName(import)}");
                continue;
            }

            var text = File.ReadAllText(import);
            if (!text.Contains("mipmaps/generate=true", StringComparison.Ordinal) || !text.Contains("compress/mode=0", StringComparison.Ordinal))
            {
                wrong.Add($"{o.Id}: .import sem mipmaps ou com compressão com perda");
            }
        }

        AssertThat(wrong.Count).OverrideFailureMessage(string.Join("\n", wrong)).IsEqual(0);
    }

    [TestCase]
    public void TextureBudgetIsSmall()
    {
        long disk = 0;
        long vram = 0;
        foreach (var o in Catalog.Value.Objects)
        {
            disk += new FileInfo(PathOf(o.File)).Length;
            vram += (long)o.PixelWidth * o.PixelHeight * 4 * 4 / 3; // RGBA8 + cadeia de mipmaps
            AssertThat(o.PixelWidth * o.PixelHeight <= 256 * 256).OverrideFailureMessage($"{o.Id}: {o.PixelWidth}×{o.PixelHeight} px").IsTrue();
        }

        AssertThat(disk < 10L * 1024 * 1024).OverrideFailureMessage($"{disk / 1048576.0:0.0} MB de PNG").IsTrue();
        AssertThat(vram < 40L * 1024 * 1024).OverrideFailureMessage($"{vram / 1048576.0:0.0} MB de textura").IsTrue();
    }

    [TestCase]
    public void IdsAndFilesAreUniqueAcrossThemes()
    {
        var objects = Catalog.Value.Objects;
        AssertThat(objects.Select(o => o.Id).Distinct().Count()).IsEqual(objects.Count);
        AssertThat(objects.Select(o => o.File).Distinct().Count()).IsEqual(objects.Count);
        AssertThat(objects.Select(o => o.Source).Distinct().Count()).OverrideFailureMessage("duas entradas do catálogo vêm da mesma peça da folha").IsEqual(objects.Count);
    }

    [TestCase]
    public void HandHeldThingsAreHandSized()
    {
        // Ao lado do jogador (32,4 u ≈ 1,8 m), uma chave ou um livro aberto no chão não passam de um palmo e pouco.
        var h = Catalog.Value.PlayerHeight;
        var wrong = Catalog.Value.Objects
            .Where(o => (o.Id.Contains("key", StringComparison.Ordinal) && o.HeightRatio(h) > 0.3f)
                        || (o.Id.StartsWith("book_", StringComparison.Ordinal) && o.HeightRatio(h) > 0.2f))
            .Select(o => $"{o.Id}: {o.HeightRatio(h):0.00}× o jogador ({o.WorldHeight * 1.8f / h:0.00} m)")
            .ToList();
        AssertThat(wrong.Count).OverrideFailureMessage(string.Join("\n", wrong)).IsEqual(0);
    }

    [TestCase]
    public void SameKindKeepsTheSameScale()
    {
        // Variações do mesmo tipo (barris, baús, caixotes, estátuas, velas) não podem variar mais que 1,75× entre si (pilhas ficam de fora).
        var h = Catalog.Value.PlayerHeight;
        var wrong = new List<string>();
        foreach (var kind in new[] { "barrel", "chest", "crate", "statue", "candle", "door", "stall", "mausoleum" })
        {
            var ratios = Catalog.Value.Objects.Where(o => o.Kind == kind && !o.Id.Contains("stack", StringComparison.Ordinal)).Select(o => o.HeightRatio(h)).ToList();
            if (ratios.Count > 1 && ratios.Max() / ratios.Min() > 1.75f)
            {
                wrong.Add($"{kind}: de {ratios.Min():0.00}× a {ratios.Max():0.00}×");
            }
        }

        AssertThat(wrong.Count).OverrideFailureMessage(string.Join("\n", wrong)).IsEqual(0);
    }

    [TestCase]
    public void ExtraMapPngsMatchTheRegistry()
    {
        var maps = MapCatalog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "maps.json")));
        var extra = ExtraMapCatalog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "maps_extra.json")), maps);
        foreach (var m in extra.Maps)
        {
            var (w, h, _, _) = ReadIhdr(PathOf(m.Image));
            AssertThat(w == m.ImageWidth && h == m.ImageHeight).OverrideFailureMessage($"{m.Id}: PNG {w}×{h}, registro {m.ImageWidth}×{m.ImageHeight}").IsTrue();
            AssertThat(maps.TryGet(m.Id, out _)).OverrideFailureMessage($"{m.Id} já existe no maps.json").IsFalse();
        }
    }

    private static string PathOf(string resPath) => Path.Combine(Root.Value, resPath["res://".Length..]);

    private static (int Width, int Height, int Depth, int ColorType) ReadIhdr(string path)
    {
        Span<byte> head = stackalloc byte[26];
        using (var f = File.OpenRead(path))
        {
            f.ReadExactly(head);
        }

        // Assinatura (8) + tamanho (4) + "IHDR" (4) + largura (4) + altura (4) + profundidade (1) + tipo de cor (1).
        AssertThat(head[12] == 'I' && head[13] == 'H' && head[14] == 'D' && head[15] == 'R').OverrideFailureMessage($"{path} não é PNG").IsTrue();
        return (BinaryPrimitives.ReadInt32BigEndian(head[16..]), BinaryPrimitives.ReadInt32BigEndian(head[20..]), head[24], head[25]);
    }
}
