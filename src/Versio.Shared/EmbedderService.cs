using Microsoft.ML;
using Microsoft.ML.Data;
using BERTTokenizers;

namespace Versio.Shared;

public class EmbedderService : IEmbedderService
{
    private readonly PredictionEngine<InputData, EmbeddingData> _predictionEngine;
    public const int InputSize = 256;

    public EmbedderService(string modelPath)
    {
        var mlContext = new MLContext();

        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var fullPath = Path.Combine(appDirectory, modelPath);

        var pipeline = mlContext.Transforms.ApplyOnnxModel(
            modelFile: fullPath,
            shapeDictionary: new Dictionary<string, int[]>
            {
                { "input_ids", new [] { 1, InputSize } },
                { "attention_mask", new [] { 1, InputSize } },
                { "token_type_ids", new [] { 1, InputSize } },
                { "last_hidden_state", new [] { 1, InputSize, 384 } },
            },
            outputColumnNames: new[] { "last_hidden_state" },
            inputColumnNames: new[] { "input_ids", "attention_mask", "token_type_ids" },
            gpuDeviceId: null,
            fallbackToCpu: true
        );

        var model = pipeline.Fit(mlContext.Data.LoadFromEnumerable(new List<InputData>()));

        _predictionEngine = mlContext.Model.CreatePredictionEngine<InputData, EmbeddingData>(model);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        DirectoryInfo dir = new DirectoryInfo(sourceDir);
        DirectoryInfo[] dirs = dir.GetDirectories();

        if (!Directory.Exists(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        FileInfo[] files = dir.GetFiles();
        foreach (FileInfo file in files)
        {
            string tempPath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(tempPath, false);
        }

        foreach (DirectoryInfo subdir in dirs)
        {
            string tempPath = Path.Combine(destinationDir, subdir.Name);
            CopyDirectory(subdir.FullName, tempPath);
        }
    }

    public float[] GetEmbeddings(string text)
    {
        var tokenizedInput = Tokenize(text);
        var result = _predictionEngine.Predict(tokenizedInput);
        return result.Embedding;
    }

    private InputData Tokenize(string text)
    {
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
        var tokenizer = new BertBaseTokenizer();

        var encoded = tokenizer.Encode(InputSize, text);

        return new InputData()
        {
            InputIds = encoded.Select(t => t.InputIds).ToArray(),
            AttentionMask = encoded.Select(t => t.AttentionMask).ToArray(),
            TokenTypeIds = encoded.Select(t => t.TokenTypeIds).ToArray()
        };
    }

    public class InputData
    {
        [VectorType(InputSize)]
        [ColumnName("input_ids")]
        public long[] InputIds { get; set; }

        [VectorType(InputSize)]
        [ColumnName("attention_mask")]
        public long[] AttentionMask { get; set; }

        [VectorType(InputSize)]
        [ColumnName("token_type_ids")]
        public long[] TokenTypeIds { get; set; }
    }

    public class EmbeddingData
    {
        [VectorType(InputSize, 384)]
        [ColumnName("last_hidden_state")]
        public float[] Embedding { get; set; }
    }
}

public interface IEmbedderService
{
    float[] GetEmbeddings(string text);
}
