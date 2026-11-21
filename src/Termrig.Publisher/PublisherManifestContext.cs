namespace Termrig.Publisher
{
    using System.Text.Json.Serialization;

    /// <summary>Source-generated JSON metadata so manifest loading works under trimming and native AOT.</summary>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
    [JsonSerializable(typeof(PublisherManifest))]
    public sealed partial class PublisherManifestContext : JsonSerializerContext
    {
    }
}
