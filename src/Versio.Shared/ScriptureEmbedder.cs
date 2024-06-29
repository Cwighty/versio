using Microsoft.Data.Sqlite;

public class ScriptureEmbedder
{
    private const int MaxChunkLength = 32;
    private const int ChunkOverlap = 16;
    private EmbeddingGenerator embedder;

    public ScriptureEmbedder(string modelPath, string vocabPath)
    {
        embedder = new EmbeddingGenerator(modelPath, vocabPath);
    }

    private List<string> ChunkText(string text)
    {
        const int maxTokens = 128;
        var sentences = text.Split(new[] { '.', '!', '?', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        var currentChunk = new List<string>();
        int currentLength = 0;

        foreach (var sentence in sentences)
        {
            var trimmedSentence = sentence.Trim();
            var sentenceTokens = trimmedSentence.Split(' ').Length;

            if (currentLength + sentenceTokens > maxTokens)
            {
                // Create a chunk from current sentences and reset
                if (currentChunk.Count > 0)
                {
                    chunks.Add(string.Join(" ", currentChunk) + '.');
                    currentChunk.Clear();
                    currentLength = 0;
                }
            }

            currentChunk.Add(trimmedSentence);
            currentLength += sentenceTokens;

        }

        // Add the remaining sentences as the last chunk
        if (currentChunk.Count > 0)
        {
            chunks.Add(string.Join(" ", currentChunk) + '.');
        }

        return chunks;
    }


    public string DestinateDbPath(string destinationPath)
    {
        string destDbFileName = $"scriptures_chunk{MaxChunkLength}_overlap{ChunkOverlap}.db";
        return Path.Combine(Path.GetDirectoryName(destinationPath), destDbFileName);
    }

    public string ProcessScriptures(string sourceDbPath, string destDbPath)
    {
        destDbPath = DestinateDbPath(destDbPath);

        // delete dest if exists
        if (File.Exists(destDbPath))
        {
            File.Delete(destDbPath);
        }

        // copy sourceDbPath to destDbPath
        File.Copy(sourceDbPath, destDbPath, true);
  
        using (var destConn = new SqliteConnection($"Data Source={destDbPath}"))
        {
            destConn.Open();

            // Create verse_chunks table in the destination database
            using (var command = destConn.CreateCommand())
            {
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS verse_chunks (
                        id INTEGER PRIMARY KEY,
                        verse_id INTEGER REFERENCES verses(id) ON DELETE CASCADE,
                        chunk_text TEXT,
                        embedding BLOB
                    )";
                command.ExecuteNonQuery();
            }

            // Process verses and generate embeddings
            using (var command = destConn.CreateCommand())
            {
                command.CommandText = "SELECT id, scripture_text FROM verses";
                using (var reader = command.ExecuteReader())
                {
                    int count = 0;
                    while (reader.Read())
                    {
                        if (++count % 100 == 0)
                        {
                            Console.Clear();
                            Console.WriteLine($"Processing verse {count}...");
                            Console.WriteLine(reader.GetString(1));
                        }

                        var verseId = reader.GetInt32(0);
                        var scriptureText = reader.GetString(1);
                        var chunks = ChunkText(scriptureText);

                        foreach (var chunk in chunks)
                        {
                            var embedding = embedder.GenerateEmbedding(chunk);
                            var embeddingBlob = embedding.SelectMany(BitConverter.GetBytes).ToArray();

                            using (var insertCommand = destConn.CreateCommand())
                            {
                                insertCommand.CommandText = "INSERT INTO verse_chunks (verse_id, chunk_text, embedding) VALUES (@verseId, @chunkText, @embedding)";
                                insertCommand.Parameters.AddWithValue("@verseId", verseId);
                                insertCommand.Parameters.AddWithValue("@chunkText", chunk);
                                insertCommand.Parameters.AddWithValue("@embedding", embeddingBlob);
                                insertCommand.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
        }

        return destDbPath;
    }
}
