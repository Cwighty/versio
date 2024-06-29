namespace Versio.Shared;

public class Tokenizer
{
    private Dictionary<string, int> vocab;

    public Tokenizer(string vocabPath = "./vocab.txt")
    {
        vocab = File.ReadAllLines(vocabPath)
            .Select((word, index) => new { word, index })
            .ToDictionary(x => x.word, x => x.index);
    }

    public (long[] inputIds, long[] attentionMask, long[] tokenTypeIds) Tokenize(string text, int inputSize)
    {
        var tokens = text.Split(' ')
            .Select(word => vocab.ContainsKey(word) ? vocab[word] : vocab["[UNK]"])
            .ToList();

        // Padding if the tokens are less than inputSize
        if (tokens.Count < inputSize)
        {
            tokens.AddRange(Enumerable.Repeat(vocab["[PAD]"], inputSize - tokens.Count));
        }
        else if (tokens.Count > inputSize)
        {
            tokens = tokens.Take(inputSize).ToList(); // Truncate if longer than inputSize
        }

        var attentionMask = tokens.Select(t => t != vocab["[PAD]"] ? 1L : 0L).ToArray(); // 1 for non-PAD tokens, 0 for PAD tokens
        var tokenTypeIds = new long[inputSize]; // Assuming single sequence input, thus all zeros.

        return (tokens.Select(t => (long)t).ToArray(), attentionMask, tokenTypeIds);
    }
}
