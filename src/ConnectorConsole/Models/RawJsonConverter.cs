// Copyright (c) Richasy. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeCliConnector.Console.Models;

/// <summary>
/// 将 JSON 对象值作为原始 JSON 字符串读写的转换器.
/// </summary>
internal sealed class RawJsonConverter : JsonConverter<string?>
{
    /// <inheritdoc/>
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return doc.RootElement.GetRawText();
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteRawValue(value);
        }
    }
}
