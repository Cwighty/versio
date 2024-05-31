
namespace Versio.Shared;
public interface ISearchService
{
    bool BookOfMormonEnabled { get; set; }
    bool DoctrineAndCovenantsEnabled { get; set; }
    int MaxResults { get; set; }
    bool NewTestamentEnabled { get; set; }
    bool OldTestamentEnabled { get; set; }
    double Threshold { get; set; }

    Task<List<ScriptureResult>> SearchAsync(string query);
}