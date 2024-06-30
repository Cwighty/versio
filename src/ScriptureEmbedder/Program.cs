using Versio.Shared;

public class Program
{
    public static void Main(string[] args)
    {
        var embeddingService = new AllMiniLmEmbedder("./all-MiniLM-L6-v2.onnx", "./all-MiniLM-L6-v2-vocab.txt");
        var embedder = new ScriptureEmbedder(embeddingService);
        var processedDbPath = embedder.DestinateDbPath("./");
        embedder.ProcessScriptures("./scriptures.db", "./");
        Console.WriteLine("Embedding complete.");

        var scorer = new BM25Scorer(processedDbPath);
        scorer.CreateBM25ScoresTable();
        scorer.ComputeAndStoreBM25Scores();
        Console.WriteLine("BM25 scoring complete.");
    }
}
