using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace API.Models.WordPress
{
    public class WordPress
    {
        public int Id { get; set; }
        public string? Date { get; set; }
        public string? Modified { get; set; }
        public string? Slug { get; set; }
        public string? Link { get; set; }
        public string? Status { get; set; }

        public RenderField? Title { get; set; }
        public RenderField? Excerpt { get; set; }
        public RenderField? Content { get; set; }

        public List<int>? Categories { get; set; }
        [JsonPropertyName("_embedded")]
        public WpEmbedded? Embedded { get; set; }
    }
    public class WPCategory
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
        public string Description { get; set; }
        public int Parent { get; set; }
        public int Count { get; set; }
    }
    public class WPMedia
    {
        public int Id { get; set; }
        public string? Source_Url { get; set; }
    }
    public class RenderField
    {
        public string? Rendered { get; set; }
    }
    public class WpEmbedded
    {
        [JsonPropertyName("wp:featuredmedia")]
        public List<WpFeaturedMedia>? FeaturedMedia { get; set; }
    }
    public class WpFeaturedMedia
    {
        [JsonPropertyName("source_url")]
        public string? SourceUrl { get; set; }
    }
}
