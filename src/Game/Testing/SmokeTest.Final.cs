using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Final;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Boot;
using Vespera.Game.Story;
using Vespera.Game.UI;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Testing;

/// <summary>
/// Roteiro do Final (<c>--final</c>): força as flags até o Ato VII (<see cref="FinalStory.UnlockFlag"/>), vê a primeira
/// Brecha na Praça e a fala do Benício e da Odete, fala antes de entrar e entra; na praça de baixo espera o Reflexo
/// avisar em âmbar e dizer a fala do minuto, vence com o bot (dano ×10, invulnerável), toca a queda, o sino (o caçador
/// toca), a ilustração, a cerimônia Alvor, a volta à Praça, o epílogo e o encerramento da Parte 1 até o título.
/// </summary>
public partial class SmokeTest
{
    public const string FinalFlag = "--final";
    private const int FightFrameLimit = 60 * 180;

    private async Task RunFinal(Level start)
    {
        var engine = StoryService.Engine;
        foreach (var flag in DoneBeforeAto4.Concat(["GuardianDefeated:farol_afogado", "act3_done", "GuardianDefeated:velora", "act4_done",
            "GuardianDefeated:sineiro_mudo", "act5_done", "GuardianDefeated:inquilino", "act6_done", FinalStory.UnlockFlag]))
        {
            engine.SetFlag(flag);
        }

        engine.Dawn();
        Check(engine.ObjectiveId == "final", $"depois do Ato VII o objetivo deveria ser 'final', é '{engine.ObjectiveId}'");

        // Praça: a primeira Brecha aberta e a fala do Benício e da Odete.
        await start.GoToRoom(FinalStory.HubLevel, withDirector: false);
        await PhysicsFrames(3);
        var level = start;
        Check(level.FinalPortal is not null, "a primeira Brecha deveria estar aberta na Praça");
        Check(level.PlayAutoplay(), "a fala do Benício e da Odete na Praça deveria tocar");
        await WaitForDialogue(level, "Benício na praça");
        await Shot("final_01_praca_benicio", 0.5);
        await SkipDialogue(level);
        Check(engine.ObjectiveId == "final_reflexo", $"depois da praça o objetivo deveria ser final_reflexo, é '{engine.ObjectiveId}'");

        // Antes de entrar: uma conversa e "Entrar.".
        await StandAt(level, level.FinalPortal!.GlobalPosition + new Vector2(0, 50 * level.World.Data.ActorScale));
        await Shot("final_02_brecha_aberta", 0.5);
        await PressInWorld(Key.E);
        Check(level.DialogueOpen, "interagir com a primeira Brecha deveria abrir 'antes de entrar'");
        await Choose(level, "Severo");
        await Choose(level, "Entrar.");
        await SkipDialogue(level);
        await WaitFor(() => RiftSession.InRift && !level.Switching && level.World.Data.Id == FinalStory.ArenaLevel, "entrada na praça de baixo");
        await PhysicsFrames(4);
        var reflexo = level.World.Director?.Guardian as Reflexo;
        Check(reflexo is not null, "o Reflexo deveria estar na praça de baixo");
        if (reflexo is null)
        {
            return;
        }

        await FightReflexo(level, reflexo);
        await AfterTheFall(level);
    }

    /// <summary>Espera o primeiro aviso âmbar (foto) e vence com o bot.</summary>
    private async Task FightReflexo(Level level, Reflexo reflexo)
    {
        var input = new ScriptedInput();
        var keyboard = level.Player.Input;
        level.Player.Input = input;
        RiftSession.Cheat = true;
        var sawWarning = false;
        for (var i = 0; i < 60 * 8 && !sawWarning; i++)
        {
            sawWarning = reflexo.State == EnemyState.Telegraph;
            await PhysicsFrames(1);
        }

        Check(sawWarning, "o Reflexo deveria avisar em âmbar antes do primeiro golpe");
        await Shot("final_03_reflexo_aviso", 0);
        Check(reflexo.Lines.Count >= 1, "o Reflexo deveria dizer a fala do primeiro minuto");
        Log.Info($"final: Reflexo com {reflexo.Profile.PreferredWeapon} na frente, {reflexo.Profile.Buff.Lamps} lanternas; fala: '{reflexo.Lines.FirstOrDefault()}'");

        var attacks = new System.Collections.Generic.HashSet<Core.Final.ReflexoAttack>();
        var maxPhase = 1;
        for (var frame = 0; frame < FightFrameLimit && !reflexo.Dead; frame++)
        {
            attacks.Add(reflexo.CurrentAttack);
            maxPhase = Mathf.Max(maxPhase, reflexo.Phase);
            var to = reflexo.GlobalPosition - level.Player.GlobalPosition;
            input.Move = to.Length() > 60f * level.World.Data.ActorScale ? to.Normalized() : to.Normalized() * 0.3f;
            if (frame % 10 == 0)
            {
                input.Tap(InputActions.Attack);
            }

            if (frame % 600 == 599)
            {
                level.Player.Teleport(level.World.ClampToFloor(reflexo.GlobalPosition + new Vector2(0, 40f * level.World.Data.ActorScale)));
            }

            await PhysicsFrames(1);
        }

        input.Move = Vector2.Zero;
        level.Player.Input = keyboard;
        RiftSession.Cheat = false;
        Check(reflexo.Dead, $"o bot deveria vencer o Reflexo em {FightFrameLimit / 60} s (vida {reflexo.Hp:0}/{reflexo.MaxHp:0})");
        Log.Info($"final: Reflexo caiu na fase {maxPhase}; golpes vistos [{string.Join(",", attacks)}]; esquivas {reflexo.Dodges}");
    }

    /// <summary>Queda, sino, ilustração, selo Alvor, volta, epílogo e fim da Parte 1.</summary>
    private async Task AfterTheFall(Level level)
    {
        var engine = StoryService.Engine;
        await WaitForDialogue(level, "fala da queda do Reflexo");
        await Shot("final_04_queda", 0.5);
        await SkipDialogue(level);
        Check(engine.HasFlag(FinalStory.ReflexoFellFlag), "a queda deveria ligar reflexo_caiu");
        Check(engine.ObjectiveId == "final_sino", $"depois da queda o objetivo deveria ser final_sino, é '{engine.ObjectiveId}'");

        var bell = level.World.Entities.GetNodeOrNull<Node2D>("zone_sino");
        Check(bell is not null, "o sino de baixo deveria virar interação");
        if (bell is null)
        {
            return;
        }

        await StandAt(level, bell.GlobalPosition + new Vector2(0, 30 * level.World.Data.ActorScale));
        await Seconds(0.4);
        await PressInWorld(Key.E);
        Check(level.DialogueOpen, "E no sino deveria abrir a fala do sino");
        await Choose(level, "Tocar você.");
        await SkipDialogue(level);
        Check(engine.HasFlag("final_jogador") && engine.HasFlag(FinalStory.AlvorFlag), "tocar o sino deveria ligar final_jogador e alvor");

        await WaitFor(() => BellScene.Active is not null, "ilustração do sino");
        Check(BellScene.Active?.TexturePath == FinalStory.SceneFor(Core.Final.BellRinger.Jogador), "a ilustração deveria ser a do caçador");
        await Shot("final_05_cena_sino", 1.2);
        BellScene.Active?.Leave();

        for (var i = 0; i < 600 && RankUpScreen.Active is not { Revealed: true }; i++)
        {
            await Frames(1);
        }

        Check(RankUpScreen.Active?.Change.To == Core.Ranks.Rank.Alvor, "a cerimônia deveria mostrar o selo Alvor");
        await Shot("final_06_selo_alvor", 0.3);
        RankUpScreen.Active?.Close();

        for (var i = 0; i < 1200 && (RiftSession.InRift || level.Switching || level.World.Data.Id != FinalStory.HubLevel); i++)
        {
            await Frames(1);
        }

        Check(!RiftSession.InRift && level.World.Data.Id == FinalStory.HubLevel, "depois do sino a caçada deveria fechar na Praça");
        for (var i = 0; i < 240 && Hunt.RunResultScreen.Active is null; i++)
        {
            await Frames(1);
        }

        Hunt.RunResultScreen.Active?.Close();
        await Frames(6);
        Check(engine.AutoplayFor(FinalStory.HubLevel)?.Dialogue == "final/epilogo", $"na Praça deveria tocar o epílogo, toca '{engine.AutoplayFor(FinalStory.HubLevel)?.Dialogue}'");
        Check(level.FinalPortal is null, "depois do sino a primeira Brecha não deveria mais estar na Praça");
        Check(level.PlayAutoplay(), "o epílogo deveria tocar");
        await WaitForDialogue(level, "epílogo");
        await SkipDialogue(level);
        Check(engine.HasFlag(FinalStory.PartOneDoneFlag), "o epílogo deveria marcar a Parte 1 como concluída");
        await WaitFor(() => PartOneEnd.Active is not null, "encerramento da Parte 1");
        await Shot("final_07_fim_parte1", 2.0);
        var slot = GameSession.Slot;
        PartOneEnd.Active?.Finish();
        await WaitFor(() => _router.Current is TitleScreen && !_router.IsBusy && FocusText() is not null, "título depois da Parte 1");
        Check(SaveService.Summarize(slot).PartOneDone, $"o save do espaço {slot} deveria dizer 'Parte 1 concluída'");

        // O save do fim continua jogável (o resto do smoke segue dentro do jogo).
        await OpenFromTitle("Continuar");
        await Shot("final_08_espaco_parte1", 0.3);
        await Tap(Key.Enter);
        await WaitFor(() => _router.Current is Level && !_router.IsBusy, "jogo (continuar depois da Parte 1)");
        Check(_router.Current is Level { FinalPortal: null }, "continuar depois da Parte 1 não deveria reabrir a primeira Brecha");
        Log.Info("final OK: Praça, antes de entrar, Reflexo, queda, sino, selo Alvor, epílogo e fim da Parte 1");
    }
}
