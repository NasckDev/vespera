namespace Vespera.Core.World;

/// <summary>
/// Escolhe quais sons ambiente do layout tocam agora (DECISIONS D47): os mais próximos do ouvinte dentro do próprio raio,
/// até o tamanho do pool de vozes. Sem alocação: escreve índices num buffer do chamador.
/// </summary>
public static class AmbientPlanner
{
    /// <summary>
    /// Preenche <paramref name="result"/> com os índices dos emissores audíveis em (<paramref name="x"/>, <paramref name="y"/>),
    /// do mais forte (menor distância/raio) para o mais fraco. Devolve quantos foram escritos.
    /// <paramref name="margin"/> (u) mantém um emissor logo fora do raio, para o fade de entrada começar antes.
    /// </summary>
    public static int SelectAudible(IReadOnlyList<LayoutAmbient> emitters, float x, float y, int[] result, float margin = 48f)
    {
        ArgumentNullException.ThrowIfNull(emitters);
        ArgumentNullException.ThrowIfNull(result);
        var count = 0;
        Span<float> keys = stackalloc float[Math.Min(result.Length, 64)];
        var capacity = keys.Length;
        for (var i = 0; i < emitters.Count; i++)
        {
            var e = emitters[i];
            var dx = e.X - x;
            var dy = e.Y - y;
            var d = MathF.Sqrt((dx * dx) + (dy * dy));
            var reach = e.Radius + margin;
            if (d > reach || reach <= 0f)
            {
                continue;
            }

            var key = d / reach;
            if (count == capacity && key >= keys[count - 1])
            {
                continue;
            }

            var pos = count < capacity ? count : capacity - 1;
            while (pos > 0 && keys[pos - 1] > key)
            {
                keys[pos] = keys[pos - 1];
                result[pos] = result[pos - 1];
                pos--;
            }

            keys[pos] = key;
            result[pos] = i;
            if (count < capacity)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Volume linear (0–1) de um emissor a <paramref name="distance"/> u com alcance <paramref name="radius"/> (queda suave).</summary>
    public static float Gain(float distance, float radius)
    {
        if (radius <= 0f || distance >= radius)
        {
            return 0f;
        }

        var t = 1f - (distance / radius);
        return t * t * (3f - (2f * t)); // smoothstep
    }
}
