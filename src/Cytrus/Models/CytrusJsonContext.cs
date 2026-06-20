using System.Text.Json.Serialization;

namespace Cytrus.Models;

[JsonSerializable(typeof(CytrusIndex))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
public sealed partial class CytrusJsonContext : JsonSerializerContext;
