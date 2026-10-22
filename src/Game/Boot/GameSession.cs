using Vespera.Core.Save;

namespace Vespera.Game.Boot;

/// <summary>Sessão em andamento: espaço de save escolhido e o estado carregado dele.</summary>
public static class GameSession
{
    public static int Slot { get; private set; }

    public static SaveData? Data { get; private set; }

    public static bool IsActive => Data is not null;

    public static void Begin(int slot, SaveData data)
    {
        Slot = slot;
        Data = data;
    }

    public static void Autosave()
    {
        if (Data is not null)
        {
            SaveService.Save(Slot, Data);
        }
    }

    public static void End()
    {
        Slot = 0;
        Data = null;
    }
}
