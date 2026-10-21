namespace Vespera.Tests.Core;

using System.IO.Compression;
using System.Text.Json;
using GdUnit4;
using Vespera.Core.World;
using static GdUnit4.Assertions;

/// <summary>
/// QA da frente B4d (interiores: Guilda, Estalagem e as 4 lojas), sem o Godot: lê a máscara walk_&lt;id&gt;.png de
/// verdade (PNG cinza de 8 bits gerado por tools/maps/layout.py) e o layout_&lt;id&gt;.json e confere, com as regras de
/// pé do jogo (<see cref="WalkAreaExtensions.CanStand"/>), que o jogador alcança tudo o que a arte sugere e que a
/// colisão bate com móveis e piso olhados na arte.
/// </summary>
[TestSuite]
public class InterioresMapaQaTest
{
    private static readonly string[] Interiors = ["guild", "inn", "shop_arm", "shop_mag", "shop_alq", "shop_rel"];

    private static readonly Lazy<MapCatalog> Real = new(() =>
        MapCatalog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "maps.json"))));

    private static readonly Lazy<string> RepoRoot = new(() =>
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vespera.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("raiz do repositório (Vespera.sln) não encontrada");
    });

    // Pontos olhados na arte (tools/maps/layout.py render <id>): 'B' = móvel/parede (bloqueado), 'W' = piso (andável).
    private static readonly (string Map, char Kind, float X, float Y, string What)[] ArtProbes =
    [
        ("guild", 'B', 155, 470, "base do boneco de treino 1"),
        ("guild", 'B', 245, 200, "mesa da Odete"),
        ("guild", 'B', 400, 150, "mural de contratos"),
        ("guild", 'B', 80, 520, "armário de lanças"),
        ("guild", 'W', 250, 430, "tapete do salão"),
        ("guild", 'W', 250, 500, "piso entre os bonecos"),
        ("guild", 'W', 400, 228, "frente do mural"),
        ("guild", 'W', 250, 700, "corredor da entrada"),
        ("inn", 'B', 105, 385, "mesa redonda noroeste"),
        ("inn", 'B', 115, 505, "mesa redonda sudoeste"),
        ("inn", 'B', 352, 510, "mesa redonda sudeste"),
        ("inn", 'B', 370, 400, "balcão da Lúcia"),
        ("inn", 'B', 100, 140, "cama oeste"),
        ("inn", 'B', 190, 140, "segunda cama"),
        ("inn", 'W', 235, 430, "tapete do salão"),
        ("inn", 'W', 90, 445, "piso entre as mesas do oeste"),
        ("inn", 'W', 250, 600, "escada da entrada"),
        ("inn", 'W', 147, 142, "vão entre as camas"),
        ("shop_arm", 'B', 296, 185, "bigorna"),
        ("shop_arm", 'B', 286, 275, "vitrine de elmos"),
        ("shop_arm", 'B', 500, 160, "balcão do Gaspar"),
        ("shop_arm", 'B', 525, 265, "base do boneco de testes"),
        ("shop_arm", 'B', 588, 270, "armadura de exposição"),
        ("shop_arm", 'W', 290, 230, "piso entre bigorna e vitrine"),
        ("shop_arm", 'W', 440, 250, "tapete"),
        ("shop_arm", 'W', 480, 240, "piso ao lado do boneco"),
        ("shop_arm", 'W', 352, 370, "escada da entrada"),
        ("shop_mag", 'B', 330, 190, "balcão da Vesna"),
        ("shop_mag", 'B', 556, 230, "mesa da bola de cristal"),
        ("shop_mag", 'B', 60, 230, "cristal grande"),
        ("shop_mag", 'B', 140, 265, "mesa de mapas"),
        ("shop_mag", 'W', 330, 260, "tapete do pentagrama"),
        ("shop_mag", 'W', 470, 170, "piso atrás da mesa da bola"),
        ("shop_mag", 'W', 210, 180, "piso entre astrolábio e balcão"),
        ("shop_alq", 'B', 355, 200, "balcão do Ivo"),
        ("shop_alq", 'B', 110, 170, "alambique"),
        ("shop_alq", 'B', 570, 250, "bancada do caldeirão azul"),
        ("shop_alq", 'W', 360, 280, "tapete"),
        ("shop_alq", 'W', 450, 250, "piso leste"),
        ("shop_rel", 'B', 335, 205, "balcão do Abel"),
        ("shop_rel", 'B', 540, 260, "bancada do orbe"),
        ("shop_rel", 'W', 335, 265, "tapete"),
        ("shop_rel", 'W', 184, 190, "vão do baú"),
        ("shop_rel", 'W', 420, 240, "piso leste"),
    ];

    private static MapCatalog Catalog => Real.Value;

    [TestCase]
    public void TestaMascarasTemOTamanhoDaArteEmCelulasDe4()
    {
        foreach (var id in Interiors)
        {
            var map = Catalog[id];
            var grid = LoadGrid(map);
            AssertThat(grid.Width).OverrideFailureMessage($"{id}: largura da máscara").IsEqual((map.ImageWidth + 3) / 4);
            AssertThat(grid.Height).OverrideFailureMessage($"{id}: altura da máscara").IsEqual((map.ImageHeight + 3) / 4);
        }
    }

    [TestCase]
    public void TestaColisaoBateComMoveisEPisoDaArte()
    {
        var failures = new List<string>();
        foreach (var (mapId, kind, x, y, what) in ArtProbes)
        {
            var area = new MaskWalkArea(LoadGrid(Catalog[mapId]), 4);
            var walkable = area.IsWalkable(x, y);
            if (walkable != (kind == 'W'))
            {
                failures.Add($"{mapId} ({x}, {y}) {what}: {(walkable ? "andável" : "bloqueado")}");
            }
        }

        AssertThat(string.Join("; ", failures)).IsEqual(string.Empty);
    }

    [TestCase]
    public void TestaTodoPisoOndeOJogadorCabeEAlcancavelDoSpawn()
    {
        // O check do layout.py conta células de 4 u; aqui vale o corpo do jogador (círculo de 3,6 u contra as células):
        // bolsões de piso ligados só por frestas ou quinas diagonais mais finas que o pé ficam visíveis na arte, o
        // caminho do Core (CanStand) acha que dá, e o jogador enrosca na entrada.
        var failures = new List<string>();
        foreach (var id in Interiors)
        {
            var map = Catalog[id];
            var area = new MaskWalkArea(LoadGrid(map), 4);
            var reach = Reach(area, map.Spawn);
            var pockets = new List<string>();
            for (var y = 0f; y < area.Height; y += 2f)
            {
                for (var x = 0f; x < area.Width; x += 2f)
                {
                    if (BodyFits(area, x, y) && !reach.Contains(((int)(x / 2), (int)(y / 2))))
                    {
                        pockets.Add($"({x}, {y})");
                    }
                }
            }

            if (pockets.Count > 0)
            {
                failures.Add($"{id}: {pockets.Count} pontos de pé inalcançáveis, ex. {string.Join(" ", pockets.Take(6))}");
            }
        }

        AssertThat(string.Join("; ", failures)).IsEqual(string.Empty);
    }

    [TestCase]
    public void TestaNpcsDecoracaoBausESaidaAlcancaveisDeTodaChegada()
    {
        var failures = new List<string>();
        foreach (var id in Interiors)
        {
            var map = Catalog[id];
            var area = new MaskWalkArea(LoadGrid(map), 4);
            var starts = new List<(string Name, MapPoint At)> { ("spawn", map.Spawn) };
            starts.AddRange(map.Arrivals.Select(a => (a.Key, a.Value)));
            starts.AddRange(Catalog.Maps.SelectMany(m => m.Exits.Where(e => e.To == id).Select(e => ($"chegada de {m.Id}", e.At))));

            var targets = new List<(string Label, MapPoint At, float Reach)>();
            targets.AddRange(map.Interactables.Select(i => (i.Label, new MapPoint(i.X, i.Y), i.Radius - 2f)));
            var layout = LoadLayout(map);
            targets.AddRange(Array(layout, "decor").Select(d => (d.GetProperty("label").GetString()!, Point(d), d.GetProperty("r").GetSingle() - 2f)));
            targets.AddRange(Array(layout, "chests").Select(c => ("baú", Point(c), 18f))); // v2: "Abrir baú" r 20

            foreach (var (name, at) in starts)
            {
                if (!BodyFits(area, MathF.Round(at.X / 2f) * 2f, MathF.Round(at.Y / 2f) * 2f))
                {
                    failures.Add($"{id} {name} ({at.X}, {at.Y}): o pé do jogador não cabe");
                    continue;
                }

                var reach = Reach(area, at);
                foreach (var (label, point, radius) in targets)
                {
                    if (!reach.Any(c => Distance(c, point) < radius))
                    {
                        failures.Add($"{id} de {name}: '{label}' ({point.X}, {point.Y}) fora de alcance (r {radius})");
                    }
                }

                foreach (var exit in map.Exits)
                {
                    if (!reach.Any(c => exit.Area.Contains(c.Item1 * 2f, c.Item2 * 2f)))
                    {
                        failures.Add($"{id} de {name}: saída para {exit.To} inalcançável");
                    }
                }
            }
        }

        AssertThat(string.Join("; ", failures)).IsEqual(string.Empty);
    }

    [TestCase]
    public void TestaDecoracaoEBauNaoInvademORaioDosDonos()
    {
        // O foco do jogador é o interagível mais próximo do pé entre os que o contêm no raio. Uma decoração colada no raio
        // de um dono (a lareira da Estalagem a 46 u da Lúcia, r 38; o boneco da Armaria a 16 u do Gaspar) disputa o foco
        // com ele: chegando pela frente, o prompt mostra "Falar com …" (INTE-2/INTE-8). Os raios podem se tocar, não se
        // sobrepor mais que 4 u.
        var failures = new List<string>();
        foreach (var id in Interiors)
        {
            var map = Catalog[id];
            var layout = LoadLayout(map);
            var spots = Array(layout, "decor").Select(d => (d.GetProperty("label").GetString()!, Point(d), d.GetProperty("r").GetSingle()))
                .Concat(Array(layout, "chests").Select(c => ("baú", Point(c), 20f))); // Chest.InteractRadius (world.js:197)
            foreach (var (label, at, radius) in spots)
            {
                foreach (var it in map.Interactables)
                {
                    var distance = MathF.Sqrt(((it.X - at.X) * (it.X - at.X)) + ((it.Y - at.Y) * (it.Y - at.Y)));
                    var overlap = radius + it.Radius - distance;
                    if (overlap > 4f)
                    {
                        failures.Add($"{id} '{label}' ({at.X}, {at.Y}): raio invade o de '{it.Label}' em {overlap:0.0} u");
                    }
                }
            }
        }

        AssertThat(string.Join("; ", failures)).IsEqual(string.Empty);
    }

    [TestCase]
    public void TestaBauTemOPeBloqueadoNaMascara()
    {
        // No motor (frente mundo-motor, Chest.cs) o baú é sólido: círculo de 8 × propScale 1,25 × 0,8 = 8 u. Se a máscara
        // deixa o piso dele andável, o caminho do Core passa por dentro do baú e o jogador trava nele (a Guilda travou o
        // bot em (55, 228), atrás do baú no canto do mezanino). O pé do baú tem que estar bloqueado na máscara.
        const float solid = 8f;
        var failures = new List<string>();
        foreach (var id in Interiors)
        {
            var map = Catalog[id];
            var area = new MaskWalkArea(LoadGrid(map), 4);
            foreach (var c in Array(LoadLayout(map), "chests"))
            {
                var at = Point(c);
                for (var y = at.Y - solid + 2f; y <= at.Y + solid - 2f; y += 2f)
                {
                    for (var x = at.X - solid + 2f; x <= at.X + solid - 2f; x += 2f)
                    {
                        if (((x - at.X) * (x - at.X)) + ((y - at.Y) * (y - at.Y)) < (solid - 2f) * (solid - 2f) && area.IsWalkable(x, y))
                        {
                            failures.Add($"{id} baú ({at.X}, {at.Y}): piso andável em ({x}, {y}), dentro do corpo do baú");
                            y = float.MaxValue - 1f;
                            break;
                        }
                    }
                }
            }
        }

        AssertThat(string.Join("; ", failures)).IsEqual(string.Empty);
    }

    [TestCase]
    public void TestaLoreNaoNegaOSomAmbienteAoLado()
    {
        // INTE-4: a lore do alambique dizia "sem fogo embaixo" com um som de fogareiro no mesmo ponto.
        string[] denials = ["sem fogo", "nunca acende", "apagad"];
        var failures = new List<string>();
        foreach (var id in Interiors)
        {
            var layout = LoadLayout(Catalog[id]);
            var fires = Array(layout, "ambient").Where(a => a.GetProperty("kind").GetString() is "fire" or "forge").Select(Point).ToList();
            foreach (var d in Array(layout, "decor"))
            {
                var text = d.GetProperty("text").GetString()!.ToLowerInvariant();
                var at = Point(d);
                var nearFire = fires.Any(f => MathF.Sqrt(((f.X - at.X) * (f.X - at.X)) + ((f.Y - at.Y) * (f.Y - at.Y))) < 140f);
                if (nearFire && denials.Any(text.Contains))
                {
                    failures.Add($"{id} '{d.GetProperty("label").GetString()}': o texto nega fogo ao lado de um som de fogo");
                }
            }
        }

        AssertThat(string.Join("; ", failures)).IsEqual(string.Empty);
    }

    [TestCase]
    public void TestaNoMaximoUmBauPorInteriorEDecoracaoComLoreCurta()
    {
        var failures = new List<string>();
        foreach (var id in Interiors)
        {
            var layout = LoadLayout(Catalog[id]);
            var chests = Array(layout, "chests").Count();
            if (chests > 1)
            {
                failures.Add($"{id}: {chests} baús (máximo 1 escondido por interior)");
            }

            foreach (var d in Array(layout, "decor"))
            {
                var label = d.TryGetProperty("label", out var l) ? l.GetString() : null;
                var text = d.TryGetProperty("text", out var t) ? t.GetString() : null;
                if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(text))
                {
                    failures.Add($"{id}: decoração em ({Point(d).X}, {Point(d).Y}) sem label ou texto");
                }
                else if (text.Length > 180)
                {
                    failures.Add($"{id} '{label}': lore com {text.Length} caracteres (curta = até 180)");
                }
            }
        }

        AssertThat(string.Join("; ", failures)).IsEqual(string.Empty);
    }

    [TestCase]
    public void TestaOclusoresTemBaseSolidaNoPeDoRecorte()
    {
        // Um oclusor é um recorte da arte desenhado na frente do jogador quando o pé dele está acima de baseY: a base
        // precisa estar no pé do recorte e o objeto precisa ser sólido ali (senão o jogador anda "dentro" dele).
        var failures = new List<string>();
        foreach (var id in Interiors)
        {
            var map = Catalog[id];
            var area = new MaskWalkArea(LoadGrid(map), 4);
            foreach (var o in Array(LoadLayout(map), "occluders"))
            {
                var note = o.TryGetProperty("note", out var n) ? n.GetString() : "?";
                var points = o.GetProperty("poly").EnumerateArray().Select(p => (X: p[0].GetSingle(), Y: p[1].GetSingle())).ToList();
                var baseY = o.GetProperty("baseY").GetSingle();
                var (minX, maxX, minY, maxY) = (points.Min(p => p.X), points.Max(p => p.X), points.Min(p => p.Y), points.Max(p => p.Y));
                if (points.Count < 3 || baseY < maxY - 8 || baseY > maxY + 2 || baseY <= minY)
                {
                    failures.Add($"{id} '{note}': baseY {baseY} fora do pé do recorte (y {minY}..{maxY})");
                    continue;
                }

                var solid = false;
                for (var y = baseY - 12; y <= baseY && !solid; y += 2)
                {
                    for (var x = minX + 2; x <= maxX - 2 && !solid; x += 2)
                    {
                        solid = !area.IsWalkable(x, y);
                    }
                }

                if (!solid)
                {
                    failures.Add($"{id} '{note}': nada bloqueado nos 12 u acima de baseY {baseY}");
                }
            }
        }

        AssertThat(string.Join("; ", failures)).IsEqual(string.Empty);
    }

    [TestCase]
    public void TestaSonsAmbienteDosInterioresSaoDoFormato()
    {
        string[] kinds = ["water", "fire", "forge", "crowd", "wind", "bells", "birds"];
        var expected = new Dictionary<string, string[]>
        {
            ["guild"] = ["crowd"],
            ["inn"] = ["crowd", "fire"],
            ["shop_arm"] = ["forge", "fire"],
        };
        var failures = new List<string>();
        foreach (var id in Interiors)
        {
            var map = Catalog[id];
            var ambient = Array(LoadLayout(map), "ambient").ToList();
            foreach (var a in ambient)
            {
                var kind = a.GetProperty("kind").GetString()!;
                var p = Point(a);
                if (!kinds.Contains(kind) || p.X < 0 || p.Y < 0 || p.X > map.ImageWidth || p.Y > map.ImageHeight || a.GetProperty("r").GetSingle() <= 0)
                {
                    failures.Add($"{id}: som '{kind}' inválido em ({p.X}, {p.Y})");
                }
            }

            foreach (var kind in expected.GetValueOrDefault(id, []))
            {
                if (!ambient.Any(a => a.GetProperty("kind").GetString() == kind))
                {
                    failures.Add($"{id}: falta som ambiente '{kind}'");
                }
            }
        }

        AssertThat(string.Join("; ", failures)).IsEqual(string.Empty);
    }

    /// <summary>
    /// Raio do pé no Godot (CircleShape2D 3,5 do Player.tscn) mais a margem de segurança do MoveAndSlide (0,08):
    /// o corpo de verdade é um círculo contra retângulos de 4 u, mais exigente que as 4 sondas do CanStand.
    /// </summary>
    private const float BodyRadius = 3.6f;

    /// <summary>O círculo do pé em (x, y) não encosta em nenhuma célula bloqueada (nem fora da grade).</summary>
    private static bool BodyFits(MaskWalkArea area, float x, float y)
    {
        for (var cy = (int)MathF.Floor((y - BodyRadius) / 4f); cy <= (int)MathF.Floor((y + BodyRadius) / 4f); cy++)
        {
            for (var cx = (int)MathF.Floor((x - BodyRadius) / 4f); cx <= (int)MathF.Floor((x + BodyRadius) / 4f); cx++)
            {
                if (area.Grid.IsWalkable(cx, cy))
                {
                    continue;
                }

                var nx = Math.Clamp(x, cx * 4f, (cx * 4f) + 4f);
                var ny = Math.Clamp(y, cy * 4f, (cy * 4f) + 4f);
                if (((nx - x) * (nx - x)) + ((ny - y) * (ny - y)) < BodyRadius * BodyRadius)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Busca em largura em passos de 2 u por onde o corpo (círculo) cabe, no ponto e no meio do passo.</summary>
    private static HashSet<(int, int)> Reach(MaskWalkArea area, MapPoint start)
    {
        var sx = (int)MathF.Round(start.X / 2f);
        var sy = (int)MathF.Round(start.Y / 2f);
        var seen = new HashSet<(int, int)>();
        if (!BodyFits(area, sx * 2f, sy * 2f))
        {
            return seen;
        }

        var queue = new Queue<(int X, int Y)>();
        seen.Add((sx, sy));
        queue.Enqueue((sx, sy));
        (int, int)[] steps = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var (dx, dy) in steps)
            {
                var next = (x + dx, y + dy);
                if (seen.Contains(next)
                    || !BodyFits(area, next.Item1 * 2f, next.Item2 * 2f)
                    || !BodyFits(area, (x * 2f) + dx, (y * 2f) + dy))
                {
                    continue;
                }

                seen.Add(next);
                queue.Enqueue(next);
            }
        }

        return seen;
    }

    private static float Distance((int X, int Y) cell, MapPoint p) =>
        MathF.Sqrt(((cell.X * 2f) - p.X) * ((cell.X * 2f) - p.X) + (((cell.Y * 2f) - p.Y) * ((cell.Y * 2f) - p.Y)));

    private static MapPoint Point(JsonElement e) => new(e.GetProperty("x").GetSingle(), e.GetProperty("y").GetSingle());

    private static IEnumerable<JsonElement> Array(JsonElement root, string name) =>
        root.TryGetProperty(name, out var a) ? a.EnumerateArray().ToList() : [];

    private static string ResPath(string res) => Path.Combine(RepoRoot.Value, res["res://".Length..].Replace('/', Path.DirectorySeparatorChar));

    private static JsonElement LoadLayout(MapDef map)
    {
        var dir = map.Image[..map.Image.LastIndexOf('/')];
        using var doc = JsonDocument.Parse(File.ReadAllText(ResPath($"{dir}/layout_{map.Id}.json")));
        return doc.RootElement.Clone();
    }

    private static WalkGrid LoadGrid(MapDef map)
    {
        var (width, height, pixels) = ReadGrayPng(ResPath(map.Mask ?? throw new InvalidOperationException($"{map.Id} sem máscara")));
        // Mesmo limiar do MaskCollision (brilho >= 0,5 = andável).
        return new WalkGrid(width, height, pixels.Select(v => v >= 128).ToArray());
    }

    /// <summary>Decodifica PNG cinza de 8 bits sem entrelaçamento (o formato que o layout.py grava).</summary>
    private static (int Width, int Height, byte[] Pixels) ReadGrayPng(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var idat = new MemoryStream();
        int width = 0, height = 0;
        for (var i = 8; i < bytes.Length;)
        {
            var length = (bytes[i] << 24) | (bytes[i + 1] << 16) | (bytes[i + 2] << 8) | bytes[i + 3];
            var type = System.Text.Encoding.ASCII.GetString(bytes, i + 4, 4);
            if (type == "IHDR")
            {
                width = (bytes[i + 8] << 24) | (bytes[i + 9] << 16) | (bytes[i + 10] << 8) | bytes[i + 11];
                height = (bytes[i + 12] << 24) | (bytes[i + 13] << 16) | (bytes[i + 14] << 8) | bytes[i + 15];
                if (bytes[i + 16] != 8 || bytes[i + 17] != 0 || bytes[i + 20] != 0)
                {
                    throw new InvalidDataException($"{path}: esperado PNG cinza de 8 bits sem entrelaçamento");
                }
            }
            else if (type == "IDAT")
            {
                idat.Write(bytes, i + 8, length);
            }

            i += 12 + length;
        }

        idat.Position = 0;
        using var z = new ZLibStream(idat, CompressionMode.Decompress);
        var raw = new MemoryStream();
        z.CopyTo(raw);
        var data = raw.ToArray();
        var pixels = new byte[width * height];
        var previous = new byte[width];
        for (var y = 0; y < height; y++)
        {
            var filter = data[y * (width + 1)];
            var row = new byte[width];
            for (var x = 0; x < width; x++)
            {
                int a = x > 0 ? row[x - 1] : 0, b = previous[x], c = x > 0 ? previous[x - 1] : 0;
                var v = data[(y * (width + 1)) + 1 + x];
                row[x] = (byte)(filter switch
                {
                    0 => v,
                    1 => v + a,
                    2 => v + b,
                    3 => v + ((a + b) / 2),
                    4 => v + Paeth(a, b, c),
                    _ => throw new InvalidDataException($"{path}: filtro {filter}"),
                });
            }

            System.Array.Copy(row, 0, pixels, y * width, width);
            previous = row;
        }

        return (width, height, pixels);

        static int Paeth(int a, int b, int c)
        {
            var p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }
    }
}
