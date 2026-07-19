using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace FetchDataFunctions.Models;

/// <summary>
/// Source-generated (compile-time, trim/AOT-friendly) serialization metadata for the provider payloads the
/// crawler deserializes. Generated with web defaults to match the previous <c>ReadFromJsonAsync</c> behaviour.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(HutInfoV2))]
[JsonSerializable(typeof(IEnumerable<AvailabilityData>))]
[JsonSerializable(typeof(List<NominatimSearchResult>))]
internal sealed partial class ScraperJsonContext : JsonSerializerContext;

/// <summary>
/// Shared options for deserializing provider payloads: uses the source-generated metadata for the declared
/// types and falls back to reflection for anything else, so an un-declared type can never fail at runtime.
/// </summary>
public static class ScraperJson
{
    public static readonly JsonSerializerOptions Options = new(ScraperJsonContext.Default.Options)
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine(ScraperJsonContext.Default, new DefaultJsonTypeInfoResolver())
    };
}
