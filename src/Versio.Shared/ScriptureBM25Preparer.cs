using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

public class BM25Scorer
{
    private readonly string connectionString;
    private readonly double k1;
    private readonly double b;
    private double avgDocLength;

    public BM25Scorer(string dbPath, double k1 = 1.5, double b = 0.75)
    {
        connectionString = $"Data Source={dbPath}";
        this.k1 = k1;
        this.b = b;
    }

    public void CreateBM25ScoresTable()
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS bm25_scores (
                verse_id INTEGER REFERENCES verses(id) ON DELETE CASCADE,
                term TEXT,
                score REAL,
                PRIMARY KEY (verse_id, term)
            )";
        command.ExecuteNonQuery();
    }

    public void ComputeAndStoreBM25Scores()
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        var totalDocs = GetTotalDocuments(connection);
        avgDocLength = GetAverageDocumentLength(connection);

        var verses = GetVerses(connection);
        var docFrequencies = new Dictionary<string, int>();
        var termFrequencies = new Dictionary<int, Dictionary<string, int>>();
        var docLengths = new Dictionary<int, int>();

        foreach (var (verseId, scriptureText) in verses)
        {
            var terms = Tokenize(scriptureText);
            docLengths[verseId] = terms.Count;
            termFrequencies[verseId] = terms.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());

            foreach (var term in terms.Distinct())
            {
                if (!docFrequencies.ContainsKey(term))
                    docFrequencies[term] = 0;
                docFrequencies[term]++;
            }
        }

        ClearExistingBM25Scores(connection);
        InsertBM25Scores(connection, docFrequencies, termFrequencies, docLengths, totalDocs);

        Console.WriteLine("BM25 scores have been successfully computed and stored.");
    }

    private int GetTotalDocuments(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM verses";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private double GetAverageDocumentLength(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT AVG(LENGTH(scripture_text)) FROM verses";
        return Convert.ToDouble(command.ExecuteScalar());
    }

    private List<(int VerseId, string ScriptureText)> GetVerses(SqliteConnection connection)
    {
        var verses = new List<(int, string)>();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT id, scripture_text FROM verses";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            verses.Add((reader.GetInt32(0), reader.GetString(1)));
        }

        return verses;
    }

    private List<string> Tokenize(string text)
    {
        return Regex.Replace(text.ToLower(), @"[^\w\s]", "").Split().ToList();
    }

    private void ClearExistingBM25Scores(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM bm25_scores";
        command.ExecuteNonQuery();
    }

    private void InsertBM25Scores(SqliteConnection connection, Dictionary<string, int> docFrequencies,
        Dictionary<int, Dictionary<string, int>> termFrequencies, Dictionary<int, int> docLengths, int totalDocs)
    {
        using var transaction = connection.BeginTransaction();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO bm25_scores (verse_id, term, score) VALUES (@verseId, @term, @score)";

        var verseIdParam = command.CreateParameter();
        verseIdParam.ParameterName = "@verseId";
        var termParam = command.CreateParameter();
        termParam.ParameterName = "@term";
        var scoreParam = command.CreateParameter();
        scoreParam.ParameterName = "@score"; 


        command.Parameters.Add(verseIdParam);
        command.Parameters.Add(termParam);
        command.Parameters.Add(scoreParam);

        foreach (var (verseId, terms) in termFrequencies)
        {
            var docLength = docLengths[verseId];
            foreach (var (term, termFrequency) in terms)
            {
                var docFrequency = docFrequencies[term];
                var score = CalculateScore(docLength, termFrequency, docFrequency, totalDocs);

                verseIdParam.Value = verseId;
                termParam.Value = term;
                scoreParam.Value = score;

                command.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    private double CalculateScore(int docLength, int termFrequency, int docFrequency, int totalDocs)
    {
        var idf = Math.Log((totalDocs - docFrequency + 0.5) / (docFrequency + 0.5) + 1);
        var tf = (termFrequency * (k1 + 1)) / (termFrequency + k1 * (1 - b + b * (docLength / avgDocLength)));
        return idf * tf;
    }
}
