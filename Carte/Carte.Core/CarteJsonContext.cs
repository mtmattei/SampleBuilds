using System.Text.Json.Serialization;

namespace Carte.Core;

// Source-generated metadata so menu JSON works where reflection is disabled (trimmed WebAssembly).
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(MenuDocument))]
[JsonSerializable(typeof(string[]))]
internal sealed partial class CarteJsonContext : JsonSerializerContext;
