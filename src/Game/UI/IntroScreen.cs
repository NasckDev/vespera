using Godot;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>
/// Abertura do jogo: cidade e logo (trecho inicial do vídeo do dono). Ao acabar, congela no último quadro e faz
/// um fundido suave para o título, cujo fundo começa exatamente nessa mesma imagem.
/// </summary>
public partial class IntroScreen : VideoCutscene
{
    private const float CrossfadeSeconds = 1.1f;

    private TextureRect? _still;

    protected override string VideoPath => ArtAssets.IntroVideoPath;

    protected override bool ShowSkipHint => false;

    protected override void AddExtras()
    {
        _still = new TextureRect
        {
            Texture = ArtAssets.IntroStill,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        _still.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_still);
    }

    protected override void OnEnded(bool skipped)
    {
        if (_still is not null && !skipped)
        {
            _still.Visible = true;
            StopPlayer();
        }

        SceneRouter.Instance.CrossfadeTo(ScreenKeys.Title, CrossfadeSeconds);
    }
}
