using System.Text.Json.Serialization;

public record ScriptureResult
{
    [JsonPropertyName("volume_id")]
    public int VolumeId { get; set; }
    [JsonPropertyName("book_id")]
    public int BookId { get; set; }
    [JsonPropertyName("chapter_id")]
    public int ChapterId { get; set; }
    [JsonPropertyName("verse_id")]
    public int VerseId { get; set; }
    [JsonPropertyName("volume_title")]
    public string? VolumeTitle { get; set; }
    [JsonPropertyName("book_title")]
    public string? BookTitle { get; set; }
    [JsonPropertyName("volume_long_title")]
    public string? VolumeLongTitle { get; set; }
    [JsonPropertyName("book_long_title")]
    public string? BookLongTitle { get; set; }
    [JsonPropertyName("volume_subtitle")]
    public string? VolumeSubtitle { get; set; }
    [JsonPropertyName("book_subtitle")]
    public string? BookSubtitle { get; set; }
    [JsonPropertyName("volume_short_title")]
    public string? VolumeShortTitle { get; set; }
    [JsonPropertyName("book_short_title")]
    public string? BookShortTitle { get; set; }
    [JsonPropertyName("volume_lds_url")]
    public string? VolumeLdsUrl { get; set; }
    [JsonPropertyName("book_lds_url")]
    public string? BookLdsUrl { get; set; }
    [JsonPropertyName("chapter_number")]
    public int ChapterNumber { get; set; }
    [JsonPropertyName("verse_number")]
    public int VerseNumber { get; set; }
    [JsonPropertyName("scripture_text")]
    public string? ScriptureText { get; set; }
    [JsonPropertyName("verse_title")]
    public string? VerseTitle { get; set; }
    [JsonPropertyName("verse_short_title")]
    public string? VerseShortTitle { get; set; }

    [JsonPropertyName("distance")]
    public double Distance { get; set; }
}

