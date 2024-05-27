using System.Text.Json.Serialization;

public class SearchService
{
    private readonly HttpClient _httpClient;

    public SearchService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public double Threshold { get; set; } = 1.5;
    public int MaxResults { get; set; } = 30;

    public bool BookOfMormonEnabled { get; set; } = true;
    public bool DoctrineAndCovenantsEnabled { get; set; } = true;
    public bool NewTestamentEnabled { get; set; } = true;
    public bool OldTestamentEnabled { get; set; } = true;

    public async Task<List<ScriptureResult>> SearchAsync(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            throw new ArgumentException("Query cannot be empty", nameof(query));
        }

        var paramObject = new Dictionary<string, string>
        {
            { "threshold", Threshold.ToString() },
            { "max_results", MaxResults.ToString() },
            { "bom", BookOfMormonEnabled.ToString() },
            { "dc", DoctrineAndCovenantsEnabled.ToString() },
            { "nt", NewTestamentEnabled.ToString() },
            { "ot", OldTestamentEnabled.ToString() }
        };


        string url = $"http://searchapi:5000/search?query={query}";

        foreach (var param in paramObject)
        {
            url += $"&{param.Key}={param.Value}";
        }

        var response = await _httpClient.GetFromJsonAsync<List<ScriptureResult>>(url);

        if (response == null)
        {
            throw new InvalidOperationException("Failed to retrieve search results");
        }

        return response;
    }
}

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
}

