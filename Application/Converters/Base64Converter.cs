using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LibraryManagement.Application.Converters
{
    public class Base64Converter : JsonConverter<byte[]?>
    {
        public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;

                case JsonTokenType.String:
                    var stringValue = reader.GetString();

                    // Boş veya null string için null döndür
                    if (string.IsNullOrWhiteSpace(stringValue))
                        return null;

                    try
                    {
                        // Base64 string'i byte array'e çevirir
                        return Convert.FromBase64String(stringValue);
                    }
                    catch (FormatException ex)
                    {
                        // Daha açıklayıcı hata mesajı
                        throw new JsonException(
                            $"RowVersion değeri geçerli bir Base64 string değil. " +
                            $"Gönderilen değer: '{stringValue}'. " +
                            $"Örnek geçerli format: 'AAAAAAAANso='", ex);
                    }

                case JsonTokenType.StartArray:
                    // Byte array olarak gönderilmişse [0, 0, 0, 0, 7, 209]
                    var bytes = new List<byte>();
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndArray)
                            break;

                        if (reader.TokenType == JsonTokenType.Number)
                        {
                            bytes.Add(reader.GetByte());
                        }
                    }
                    return bytes.Count > 0 ? bytes.ToArray() : null;

                default:
                    throw new JsonException($"RowVersion için beklenmeyen token tipi: {reader.TokenType}");
            }
        }

        public override void Write(Utf8JsonWriter writer, byte[]? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue(Convert.ToBase64String(value));
            }
        }
    }
}