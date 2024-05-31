using Microsoft.Data.Sqlite;

namespace Versio.Shared;

public class BM25Search : ISearchService
{
    private readonly IEmbedderService embedder;
    private readonly string connectionString;

    public BM25Search(IEmbedderService embedder, string connectionString = "Data Source=scriptures.db")
    {
        this.embedder = embedder;

        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var scripturePath = Path.Combine(appDirectory, "scriptures.db");
        connectionString = $"Data Source={scripturePath}";
        this.connectionString = connectionString;
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

        var tokenizedQuery = Tokenize(query);

        var scriptureResults = new List<ScriptureResult>();

        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();

            foreach (var term in tokenizedQuery)
            {
                var command = connection.CreateCommand();
                command.CommandText =
                @"
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
                        bm25_scores.score
                    FROM bm25_scores
                    JOIN verses ON bm25_scores.verse_id = verses.id
                    JOIN chapters ON verses.chapter_id = chapters.id
                    JOIN books ON chapters.book_id = books.id
                    JOIN volumes ON books.volume_id = volumes.id
                    WHERE bm25_scores.term = @term
                    ORDER BY bm25_scores.score DESC
                    LIMIT @maxResults
                ";
                command.Parameters.AddWithValue("@term", term);
                command.Parameters.AddWithValue("@maxResults", MaxResults);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var scriptureResult = new ScriptureResult
                        {
                            VerseId = reader.GetInt32(reader.GetOrdinal("verse_id")),
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
                            Distance = reader.GetDouble(reader.GetOrdinal("score"))
                        };

                        scriptureResults.Add(scriptureResult);
                    }
                }
            }
        }

        var topResults = scriptureResults
            .GroupBy(result => result.VerseId)
            .Select(group => new ScriptureResult
            {
                VerseId = group.Key,
                VolumeId = group.First().VolumeId,
                BookId = group.First().BookId,
                ChapterId = group.First().ChapterId,
                VerseNumber = group.First().VerseNumber,
                ChapterNumber = group.First().ChapterNumber,
                VolumeTitle = group.First().VolumeTitle,
                VolumeLongTitle = group.First().VolumeLongTitle,
                VolumeSubtitle = group.First().VolumeSubtitle,
                VolumeShortTitle = group.First().VolumeShortTitle,
                VolumeLdsUrl = group.First().VolumeLdsUrl,
                BookTitle = group.First().BookTitle,
                BookLongTitle = group.First().BookLongTitle,
                BookSubtitle = group.First().BookSubtitle,
                BookShortTitle = group.First().BookShortTitle,
                BookLdsUrl = group.First().BookLdsUrl,
                ScriptureText = group.First().ScriptureText,
                Distance = group.Sum(result => result.Distance)
            })
            .OrderByDescending(result => result.Distance)
            .Take(MaxResults)
            .ToList();

        return topResults;
    }

    private List<string> Tokenize(string text)
    {
        char[] delimiters = new char[] { ' ', '.', ',', ';', '!', '?' };
        return text.ToLower().Split(delimiters, StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}


