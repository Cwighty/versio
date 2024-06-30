using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Linq;
using System.Text.RegularExpressions;

namespace Versio.Shared;
public class DistilbertEmbeddingGenerator : IEmbedderService
{
    private InferenceSession session;
    private const int MaxSequenceLength = 128;
    private Dictionary<string, int> tokenToId;

    public DistilbertEmbeddingGenerator(string modelPath, string vocabPath)
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
}

public class SimpleTokenizer
{
    private const int MaxLength = 128;
    private readonly Dictionary<string, int> _vocab;

    public SimpleTokenizer(string vocabPath)
    {
        _vocab = LoadVocabulary(vocabPath);
    }

    public int PadTokenId => _vocab["[PAD]"];

    private Dictionary<string, int> LoadVocabulary(string path)
    {
        return File.ReadAllLines(path)
            .Select((token, index) => new { Token = token, Index = index })
            .ToDictionary(item => item.Token, item => item.Index);
    }

    public List<int> Tokenize(string text)
    {
        var tokens = new List<int> { _vocab["[CLS]"] };

        var words = Regex.Split(text.ToLower(), @"\W+")
            .Where(word => !string.IsNullOrEmpty(word));

        foreach (var word in words)
        {
            if (_vocab.TryGetValue(word, out int id))
            {
                tokens.Add(id);
            }
            else
            {
                tokens.Add(_vocab["[UNK]"]);
            }

            if (tokens.Count >= MaxLength - 1)
                break;
        }

        tokens.Add(_vocab["[SEP]"]);

        while (tokens.Count < MaxLength)
        {
            tokens.Add(_vocab["[PAD]"]);
        }

        return tokens;
    }
}

public class AllMiniLmEmbedder : IEmbedderService
{
    private readonly InferenceSession _session;
    private readonly SimpleTokenizer _tokenizer;

    public AllMiniLmEmbedder(string modelPath, string vocabPath)
    {
        // set the current directory to the directory of the model
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
        var mPath = Path.GetFullPath(modelPath);
        var vPath = Path.GetFullPath(vocabPath);
        _session = new InferenceSession(mPath);
        _tokenizer = new SimpleTokenizer(vPath);
    }

    public float[] GenerateEmbedding(string text)
    {
        var tokenIds = _tokenizer.Tokenize(text);

        var inputData = new List<NamedOnnxValue>();

        // Add input_ids
        inputData.Add(NamedOnnxValue.CreateFromTensor("input_ids",
            new DenseTensor<long>(tokenIds.Select(id => (long)id).ToArray(), new int[] { 1, tokenIds.Count })));

        // Add attention_mask
        var attentionMask = tokenIds.Select(id => id != _tokenizer.PadTokenId ? 1L : 0L).ToArray();
        inputData.Add(NamedOnnxValue.CreateFromTensor("attention_mask",
            new DenseTensor<long>(attentionMask, new int[] { 1, tokenIds.Count })));

        // Add token_type_ids
        var tokenTypeIds = new long[tokenIds.Count]; // All zeros for single sentence
        inputData.Add(NamedOnnxValue.CreateFromTensor("token_type_ids",
            new DenseTensor<long>(tokenTypeIds, new int[] { 1, tokenIds.Count })));

        using (var results = _session.Run(inputData))
        {
            var output = results.First().AsTensor<float>();

            var embeddingDim = 384;
            var embedding = new float[embeddingDim];
            int nonPaddingTokens = Math.Min(attentionMask.Count(mask => mask == 1), 128);

            for (int i = 0; i < nonPaddingTokens; i++)
            {
                for (int j = 0; j < embeddingDim; j++)
                {
                    embedding[j] += output[0, i, j];
                }
            }

            for (int j = 0; j < embeddingDim; j++)
            {
                embedding[j] /= nonPaddingTokens;
            }

            // Normalize the embedding
            float magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] /= magnitude;
            }

            return embedding;
        }
    }
}