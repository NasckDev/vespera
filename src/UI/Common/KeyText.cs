using System;
using System.Text;
using Godot;
using Vespera.Autoload;

namespace Vespera.UI.Common;

/// <summary>
/// Teclas dentro de textos corridos (POLI-4): falas, missões, "Como jogar" e telas de ajuda trazem marcadores que viram a
/// tecla ou o botão de <b>agora</b> (dispositivo em uso e remapeamento, via <see cref="InputBindings"/>), em vez de um "F" ou
/// "Esc" escrito no texto. Marcadores: <c>{key:ação}</c> (a tecla principal: "F" / "RT"), <c>{keys:ação}</c> (todas, "J ou
/// Clique" / "X") e <c>{keyPhrase:ação}</c> ("a tecla F" / "o botão RT"). A ação é o nome do Input Map (<c>ultimate</c>,
/// <c>pause</c>, <c>attack</c>…). Texto sem marcador passa direto, sem alocar. Usado ao exibir (não por quadro).
/// </summary>
public static class KeyText
{
    private const string Open = "{key";

    /// <summary>Troca os marcadores pelas teclas do dispositivo em uso.</summary>
    public static string Expand(string text) => Expand(text, InputHints.UsingGamepad);

    /// <summary>Troca os marcadores pelas teclas de um dispositivo específico (testes).</summary>
    public static string Expand(string text, bool gamepad)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains(Open, StringComparison.Ordinal))
        {
            return text;
        }

        var sb = new StringBuilder(text.Length + 16);
        var i = 0;
        while (i < text.Length)
        {
            var start = text.IndexOf(Open, i, StringComparison.Ordinal);
            if (start < 0)
            {
                sb.Append(text, i, text.Length - i);
                break;
            }

            var end = text.IndexOf('}', start);
            if (end < 0)
            {
                sb.Append(text, i, text.Length - i);
                break;
            }

            sb.Append(text, i, start - i);
            var token = text.AsSpan(start + 1, end - start - 1); // key:ultimate
            var colon = token.IndexOf(':');
            if (colon < 0)
            {
                sb.Append(text, start, end - start + 1);
            }
            else
            {
                var kind = token[..colon].ToString();
                var action = token[(colon + 1)..].ToString();
                sb.Append(kind switch
                {
                    "key" => InputHints.LabelFor(action, gamepad),
                    "keys" => Keys(action, gamepad),
                    "keyPhrase" => (gamepad ? "o botão " : "a tecla ") + InputHints.LabelFor(action, gamepad),
                    _ => text.Substring(start, end - start + 1),
                });
            }

            i = end + 1;
        }

        return sb.ToString();
    }

    /// <summary>Todas as teclas (ou botões) da ação no dispositivo, "J ou Clique".</summary>
    public static string Keys(string action, bool gamepad)
    {
        var labels = InputBindings.Labels(action, gamepad);
        return labels.Count == 0 ? InputHints.LabelFor(action, gamepad) : string.Join(" ou ", labels);
    }
}
