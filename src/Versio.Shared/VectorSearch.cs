using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System.Data.Common;

namespace Versio.Shared;

public class VectorSearch : ISearchService
{
    private readonly IEmbedderService embedder;
    private readonly string connectionString;

    public VectorSearch(IEmbedderService embedder, string connectionString = "Data Source=scriptures.db")
    {
        this.embedder = embedder;
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var scripturePath = Path.Combine(appDirectory, "scriptures.db");
        this.connectionString = $"Data Source={scripturePath}";
    }

    public double Threshold { get; set; } = 0.7;
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

        var queryEmbedding = embedder.GenerateEmbedding(query);

        var scriptureResults = new List<ScriptureResult>();

        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    verses.id AS verse_id,
                    volumes.id AS volume_id,
                    books.id AS book_id,
                    chapters.id AS chapter_id,
                    verses.verse_number,
                    chapters.chapter_number,
                    volumes.volume_title,
                    volumes.volume_long_title,
                    volumes.volume_subtitle,
                    volumes.volume_short_title,
                    volumes.volume_lds_url,
                    books.book_title,
                    books.book_long_title,
                    books.book_subtitle,
                    books.book_short_title,
                    books.book_lds_url,
                    verses.scripture_text,
                    verse_chunks.id AS chunk_id,
                    verse_chunks.verse_id AS chunk_verse_id,
                    verse_chunks.chunk_text AS chunk_text,
                    verse_chunks.embedding AS chunk_embedding
                FROM verse_chunks
                JOIN verses ON verse_chunks.verse_id = verses.id
                JOIN chapters ON verses.chapter_id = chapters.id
                JOIN books ON chapters.book_id = books.id
                JOIN volumes ON books.volume_id = volumes.id
                WHERE (@bom = 1 OR volumes.volume_title != 'Book of Mormon')
                    AND (@dc = 1 OR volumes.volume_title != 'Doctrine and Covenants')
                    AND (@nt = 1 OR volumes.volume_title != 'New Testament')
                    AND (@ot = 1 OR volumes.volume_title != 'Old Testament')
            ";

            command.Parameters.AddWithValue("@bom", BookOfMormonEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@dc", DoctrineAndCovenantsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@nt", NewTestamentEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@ot", OldTestamentEnabled ? 1 : 0);

            using (var reader = command.ExecuteReader())
            {
                var scriptureResultsDict = new Dictionary<int, ScriptureResult>();

                while (reader.Read())
                {
                    var verseId = reader.GetInt32(reader.GetOrdinal("verse_id"));

                    if (!scriptureResultsDict.TryGetValue(verseId, out var scriptureResult))
                    {
                        scriptureResult = new ScriptureResult
                        {
                            VerseId = verseId,
                            VolumeId = reader.GetInt32(reader.GetOrdinal("volume_id")),
                            BookId = reader.GetInt32(reader.GetOrdinal("book_id")),
                            ChapterId = reader.GetInt32(reader.GetOrdinal("chapter_id")),
                            VerseNumber = reader.GetInt32(reader.GetOrdinal("verse_number")),
                            ChapterNumber = reader.GetInt32(reader.GetOrdinal("chapter_number")),
                            VolumeTitle = reader.GetString(reader.GetOrdinal("volume_title")),
                            VolumeLongTitle = reader.GetString(reader.GetOrdinal("volume_long_title")),
                            VolumeSubtitle = reader.GetString(reader.GetOrdinal("volume_subtitle")),
                            VolumeShortTitle = reader.GetString(reader.GetOrdinal("volume_short_title")),
                            VolumeLdsUrl = reader.GetString(reader.GetOrdinal("volume_lds_url")),
                            BookTitle = reader.GetString(reader.GetOrdinal("book_title")),
                            BookLongTitle = reader.GetString(reader.GetOrdinal("book_long_title")),
                            BookSubtitle = reader.GetString(reader.GetOrdinal("book_subtitle")),
                            BookShortTitle = reader.GetString(reader.GetOrdinal("book_short_title")),
                            BookLdsUrl = reader.GetString(reader.GetOrdinal("book_lds_url")),
                            ScriptureText = reader.GetString(reader.GetOrdinal("scripture_text")),
                            Chunks = new List<VerseChunk>()
                        };
                        scriptureResultsDict[verseId] = scriptureResult;
                    }

                    var chunk = new VerseChunk
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("chunk_id")),
                        VerseId = reader.GetInt32(reader.GetOrdinal("chunk_verse_id")),
                        Text = reader.GetString(reader.GetOrdinal("chunk_text")),
                        Embedding = GetFloatArrayFromBlob(reader, "chunk_embedding")
                    };

                    double cosineSimilarity = CosineSimilarity(queryEmbedding, chunk.Embedding);
                    //chunk.CosineSimilarity = cosineSimilarity;

                    if (cosineSimilarity > Threshold)
                    {
                        scriptureResult.Chunks.Add(chunk);
                        scriptureResult.Distance = Math.Max(scriptureResult.Distance, cosineSimilarity);
                    }
                }

                scriptureResults.AddRange(scriptureResultsDict.Values.Where(r => r.Chunks.Any()));
            }
        }

        return scriptureResults
            .OrderByDescending(r => r.Distance)
            .Take(MaxResults)
            .ToList();
    }

    private float[] GetFloatArrayFromBlob(DbDataReader reader, string columnName)
    {
        int columnIndex = reader.GetOrdinal(columnName);

        if (reader.IsDBNull(columnIndex))
            return null;

        long blobLength = reader.GetBytes(columnIndex, 0, null, 0, 0);
        byte[] blobData = new byte[blobLength];
        reader.GetBytes(columnIndex, 0, blobData, 0, (int)blobLength);

        float[] floatArray = new float[blobLength / sizeof(float)];
        Buffer.BlockCopy(blobData, 0, floatArray, 0, (int)blobLength);

        return floatArray;
    }

    private double CosineSimilarity(float[] v1, float[] v2)
    {
        double dotProduct = 0;
        double magnitude1 = 0;
        double magnitude2 = 0;

        for (int i = 0; i < v1.Length; i++)
        {
            dotProduct += v1[i] * v2[i];
            magnitude1 += v1[i] * v1[i];
            magnitude2 += v2[i] * v2[i];
        }

        magnitude1 = Math.Sqrt(magnitude1);
        magnitude2 = Math.Sqrt(magnitude2);

        if (magnitude1 == 0 || magnitude2 == 0)
            return 0;

        return dotProduct / (magnitude1 * magnitude2);
    }
}
