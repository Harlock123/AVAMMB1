using System.Text.Json;
using System.Text.Json.Serialization;
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Session;

namespace AVAMMB1.Core.Persistence;

/// <summary>Reads dice expressions from strings ("2d6+1") or plain numbers.</summary>
public sealed class DiceJsonConverter : JsonConverter<DiceExpression>
{
    /// <inheritdoc />
    public override DiceExpression Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => new DiceExpression(0, 0, reader.GetInt32()),
            JsonTokenType.String => DiceExpression.TryParse(reader.GetString(), out var d)
                ? d
                : throw new JsonException($"Invalid dice expression '{reader.GetString()}'."),
            _ => throw new JsonException("Expected a dice expression string."),
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DiceExpression value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

/// <summary>Source-generated JSON metadata for content, saves and settings (trim/AOT friendly).</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    WriteIndented = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = [typeof(DiceJsonConverter)])]
[JsonSerializable(typeof(List<RaceDef>))]
[JsonSerializable(typeof(List<ClassDef>))]
[JsonSerializable(typeof(List<ItemDef>))]
[JsonSerializable(typeof(List<MonsterDef>))]
[JsonSerializable(typeof(List<SpellDef>))]
[JsonSerializable(typeof(List<ShopDef>))]
[JsonSerializable(typeof(GameConfigDef))]
[JsonSerializable(typeof(MapDef))]
[JsonSerializable(typeof(SaveFile))]
[JsonSerializable(typeof(GameState))]
[JsonSerializable(typeof(GameSettings))]
public sealed partial class GameJsonContext : JsonSerializerContext;
