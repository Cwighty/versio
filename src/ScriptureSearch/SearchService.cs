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
