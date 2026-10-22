using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>
/// Prólogo de "Nova história": a praça, a grande lanterna e a Brecha abrindo (trecho final do vídeo do dono).
/// Pulável com qualquer tecla ou botão depois de meio segundo; ao fim segue para <see cref="ScreenKeys.AfterPrologue"/>.
/// </summary>
public partial class PrologueScreen : VideoCutscene
{
    protected override string VideoPath => ArtAssets.PrologueVideoPath;

    protected override bool ShowSkipHint => true;

    protected override void OnEnded(bool skipped)
    {
        if (!skipped)
        {
            FadeAudio();
        }

        SceneRouter.Instance.GoTo(ScreenKeys.AfterPrologue);
    }
}
