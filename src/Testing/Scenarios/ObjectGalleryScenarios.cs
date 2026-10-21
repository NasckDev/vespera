using System;
using System.Linq;
using System.Threading.Tasks;
using Vespera.Autoload;
using Vespera.Debug;

namespace Vespera.Testing;

/// <summary>Biblioteca de objetos (frente B5): galeria por tema, tamanho calibrado ao lado do jogador e da porta, com prints.</summary>
public partial class SmokeTest
{
    private const string WorldScenePath = "res://scenes/World/World.tscn";

    [SmokeScenario("objetos: galeria por tema no tamanho calibrado", order: 900)]
    private async Task ObjectGalleryByTheme()
    {
        await WaitForScene("World", timeoutSeconds: 10); // o boot (Main → World) precisa terminar antes de trocar de cena
        GameManager.Instance.ChangeScene(ObjectGallery.ScenePath);
        await WaitForScene("ObjectGallery", timeoutSeconds: 10);
        var gallery = Find<ObjectGallery>() ?? throw new SmokeFailure("sem ObjectGallery");
        var catalog = ObjectGallery.Catalog;
        Require(catalog.Count > 400, $"catálogo com {catalog.Count} objetos");

        var shown = 0;
        foreach (var theme in catalog.Themes.Keys)
        {
            gallery.ShowTheme(theme);
            Require(gallery.PageCount >= 1, $"{theme}: sem páginas");
            var inTheme = 0;
            for (var page = 0; page < gallery.PageCount; page++)
            {
                gallery.ShowPage(page);
                await Frames(2);
                Require(gallery.Sprites.Count > 0, $"{theme} p{page + 1}: página vazia");
                Require(gallery.Sprites.All(s => s.Texture is not null), $"{theme} p{page + 1}: textura não carregou");
                inTheme += gallery.Sprites.Count;

                // Escala real: o jogador de referência mede a altura de calibração e a porta, a altura de porta.
                var player = gallery.ReferencePlayer ?? throw new SmokeFailure("sem jogador de referência");
                var playerHeight = ObjectGallery.WorldHeightOf(player);
                Require(Math.Abs(playerHeight - catalog.PlayerHeight) < 1f, $"jogador mede {playerHeight:0.0} u, calibração {catalog.PlayerHeight} u");
                var door = gallery.ReferenceDoor ?? throw new SmokeFailure("sem porta de referência");
                var doorHeight = ObjectGallery.WorldHeightOf(door);
                Require(Math.Abs(doorHeight - catalog.DoorHeight) < 1f, $"porta mede {doorHeight:0.0} u, referência {catalog.DoorHeight} u");
                for (var i = 0; i < gallery.Sprites.Count; i++)
                {
                    var def = gallery.Shown[i];
                    var h = ObjectGallery.WorldHeightOf(gallery.Sprites[i]);
                    Require(Math.Abs(h - def.WorldHeight) < 1.5f, $"{def.Id}: desenhado com {h:0.0} u, catálogo {def.WorldHeight} u");
                }

                await Shot($"objetos-{theme}-{page + 1}");
            }

            Require(inTheme == catalog.InTheme(theme).Count, $"{theme}: {inTheme} de {catalog.InTheme(theme).Count} objetos nas páginas");
            shown += inTheme;
        }

        Require(shown == catalog.Count, $"galeria mostrou {shown} de {catalog.Count}");

        // Navegação por ação (teclado e gamepad usam as mesmas ações move_*).
        gallery.ShowTheme(catalog.Themes.Keys.First());
        await Tap(InputActions.MoveRight);
        Require(gallery.Theme == catalog.Themes.Keys.ElementAt(1), $"→ deveria ir ao 2º tema, está em {gallery.Theme}");
        await Tap(InputActions.MoveLeft);
        Require(gallery.Theme == catalog.Themes.Keys.First(), $"← deveria voltar ao 1º tema, está em {gallery.Theme}");
        if (gallery.PageCount > 1)
        {
            await Tap(InputActions.MoveDown);
            Require(gallery.Page == 1, $"↓ deveria ir à página 2, está na {gallery.Page + 1}");
        }

        GameManager.Instance.ChangeScene(WorldScenePath);
        await WaitForScene("World", timeoutSeconds: 10);
    }
}
