using Microsoft.Extensions.DependencyInjection;
using Versio.Shared;

public class CombinedSearch : ISearchService
{
    private ISearchService bm25Search;
    private ISearchService vectorSearch;

    // inject keyed services

    public CombinedSearch(IServiceProvider services)
    {
        bm25Search = services.GetRequiredKeyedService<ISearchService>(SearchAlgorithmOptions.BM25);
        vectorSearch = services.GetRequiredKeyedService<ISearchService>(SearchAlgorithmOptions.KNNVectorSimiliarity);
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

        var bm25Results = await bm25Search.SearchAsync(query);

        var vectorResults = await vectorSearch.SearchAsync(query);

        return bm25Results.Concat(vectorResults).ToList();
    }
}
