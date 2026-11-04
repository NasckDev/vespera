using Godot;

namespace Vespera.Game.Audio;

/// <summary>
/// Silêncio do Ato V (ROTEIRO-V2 §13, CAMPANHA Ato V): a Sala do Silêncio e a fase CULPA do Sineiro Mudo tiram todo o
/// som (música, golpes, passos) e deixam só os avisos visuais. Quem pede silêncio chama <see cref="Begin"/> e, ao sair,
/// <see cref="End"/>; contado, para dois pedidos juntos não devolverem o som antes da hora. O estado de mudo do Master
/// de antes (o jogador pode ter desligado o som nas opções) volta quando o último pedido termina.
/// </summary>
public static class Silence
{
    private static int _requests;
    private static bool _wasMuted;

    /// <summary>Há silêncio pedido agora.</summary>
    public static bool Active => _requests > 0;

    public static void Begin()
    {
        if (_requests++ == 0)
        {
            _wasMuted = AudioServer.IsBusMute(0);
            AudioServer.SetBusMute(0, true);
        }
    }

    public static void End()
    {
        if (_requests == 0)
        {
            return;
        }

        if (--_requests == 0)
        {
            AudioServer.SetBusMute(0, _wasMuted);
        }
    }
}
