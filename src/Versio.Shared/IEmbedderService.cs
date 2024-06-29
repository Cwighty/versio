namespace Versio.Shared;

public interface IEmbedderService
{
    double CalculateSimilarity(float[] embedding1, float[] embedding2);
    float[] DecodeEmbedding(byte[] embedding);
    float[] GetEmbeddings(string text);
}
