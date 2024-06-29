using Versio.Shared;

public class VectorSearch : ISearchService
{
    private readonly IEmbedderService embedder;

    public VectorSearch(IEmbedderService embedder)
    {
        this.embedder = embedder;
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

        var embedding = embedder.GetEmbeddings(query);

        throw new Exception(embedding.First().ToString());
    }
}
