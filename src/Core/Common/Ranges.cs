using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vespera.Core.Common;

/// <summary>Intervalo real [Min, Max). Nos JSON é escrito como <c>[min, max]</c>.</summary>
[JsonConverter(typeof(NumRangeJsonConverter))]
public readonly record struct NumRange(double Min, double Max);

/// <summary>Intervalo inteiro [Min, Max] inclusivo. Nos JSON é escrito como <c>[min, max]</c>.</summary>
[JsonConverter(typeof(IntRangeJsonConverter))]
public readonly record struct IntRange(int Min, int Max);

/// <summary>Ponto 2D em unidades do mundo (1 u = 1 px do mapa). Nos JSON é escrito como <c>[x, y]</c>.</summary>
[JsonConverter(typeof(Point2JsonConverter))]
public readonly record struct Point2(double X, double Y);

internal static class PairReader
{
    public static (double A, double B) Read(ref Utf8JsonReader reader, string typeName)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"{typeName} precisa ser um array [a, b].");
        }

        reader.Read();
        var a = reader.GetDouble();
        reader.Read();
        var b = reader.GetDouble();
        reader.Read();
        if (reader.TokenType != JsonTokenType.EndArray)
        {
            throw new JsonException($"{typeName} precisa ter exatamente 2 números.");
        }

        return (a, b);
    }

    public static void Write(Utf8JsonWriter writer, double a, double b)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(a);
        writer.WriteNumberValue(b);
        writer.WriteEndArray();
    }
}

internal sealed class NumRangeJsonConverter : JsonConverter<NumRange>
{
    public override NumRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var (a, b) = PairReader.Read(ref reader, nameof(NumRange));
        return new NumRange(a, b);
    }

    public override void Write(Utf8JsonWriter writer, NumRange value, JsonSerializerOptions options) => PairReader.Write(writer, value.Min, value.Max);
}

internal sealed class IntRangeJsonConverter : JsonConverter<IntRange>
{
    public override IntRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var (a, b) = PairReader.Read(ref reader, nameof(IntRange));
        if (a != Math.Floor(a) || b != Math.Floor(b))
        {
            throw new JsonException("IntRange só aceita inteiros.");
        }

        return new IntRange((int)a, (int)b);
    }

    public override void Write(Utf8JsonWriter writer, IntRange value, JsonSerializerOptions options) => PairReader.Write(writer, value.Min, value.Max);
}

internal sealed class Point2JsonConverter : JsonConverter<Point2>
{
    public override Point2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var (a, b) = PairReader.Read(ref reader, nameof(Point2));
        return new Point2(a, b);
    }

    public override void Write(Utf8JsonWriter writer, Point2 value, JsonSerializerOptions options) => PairReader.Write(writer, value.X, value.Y);
}
