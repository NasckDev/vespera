namespace Vespera.Game.UI;

/// <summary>Chaves das telas registradas no SceneRouter.</summary>
public static class ScreenKeys
{
    public const string Title = "title";
    public const string Options = "options";
    public const string Controls = "controls";
    public const string Help = "help";
    public const string Credits = "credits";
    public const string Pause = "pause";
    public const string Journal = "journal";
    public const string SlotsNew = "slots_new";
    public const string SlotsContinue = "slots_continue";
    public const string Game = "game";
    public const string Intro = "intro";
    public const string Prologue = "prologue";

    /// <summary>Cena que começa o jogo depois do prólogo (e ao continuar um save). Troque aqui para plugar o nível jogável.</summary>
    public const string AfterPrologue = Game;
}
