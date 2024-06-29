public class Program
{
    public static void Main(string[] args)
    {
        var embedder = new ScriptureEmbedder("./msmarco-distilbert-base-v3.onnx", "./vocab.txt");
        var processedDbPath = embedder.DestinateDbPath("./scriptures_with_embeddings.db");
        embedder.ProcessScriptures("./scriptures.db", "./scriptures_with_embeddings.db");
        Console.WriteLine("Embedding complete.");

        var scorer = new BM25Scorer(processedDbPath);
        scorer.CreateBM25ScoresTable();
        scorer.ComputeAndStoreBM25Scores();
        Console.WriteLine("BM25 scoring complete.");
    }
}
