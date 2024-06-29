using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

public class EmbeddingGenerator
{
    private InferenceSession session;
    private const int MaxSequenceLength = 128;
    private Dictionary<string, int> tokenToId;

    public EmbeddingGenerator(string modelPath, string vocabPath)
    {
        session = new InferenceSession(modelPath);
        tokenToId = LoadVocabulary(vocabPath);
    }

    private Dictionary<string, int> LoadVocabulary(string path)
    {
        var vocab = new Dictionary<string, int>();
        var lines = System.IO.File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            vocab[lines[i]] = i;
        }
        return vocab;
    }

    private List<long> Tokenize(string text)
    {
        var tokens = new List<long> { tokenToId["[CLS]"] };
        foreach (var word in text.ToLower().Split())
        {
            if (tokenToId.TryGetValue(word, out int id))
            {
                tokens.Add(id);
            }
            else
            {
                tokens.Add(tokenToId["[UNK]"]);
            }
        }
        tokens.Add(tokenToId["[SEP]"]);
        return tokens.Take(MaxSequenceLength).ToList();
    }

    public float[] GenerateEmbedding(string text)
    {
        var tokens = Tokenize(text);
        var inputIds = new long[MaxSequenceLength];
        var attentionMask = new long[MaxSequenceLength];

        for (int i = 0; i < MaxSequenceLength; i++)
        {
            if (i < tokens.Count)
            {
                inputIds[i] = tokens[i];
                attentionMask[i] = 1;
            }
            else
            {
                inputIds[i] = 0;
                attentionMask[i] = 0;
            }
        }

        var inputTensor = new DenseTensor<long>(inputIds, new[] { 1, MaxSequenceLength });
        var maskTensor = new DenseTensor<long>(attentionMask, new[] { 1, MaxSequenceLength });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", maskTensor)
        };

        using (var results = session.Run(inputs))
        {
            var output = results.First().AsTensor<float>();
            return output.ToArray();
        }
    }

    //static void Main(string[] args)
    //{
    //    // print current directory
    //    Console.WriteLine(Directory.GetCurrentDirectory());

    //    var generator = new EmbeddingGenerator("./msmarco-distilbert-base-v3.onnx", "./vocab.txt");
    //    string text = "Hello, world!";
    //    var embedding = generator.GenerateEmbedding(text);
    //    Console.WriteLine($"Embedding dimension: {embedding.Length}");
    //    Console.WriteLine($"First few values: {string.Join(", ", embedding.Take(5))}");
    //}
}