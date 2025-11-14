namespace WebApplication1.Services
{
    public interface IEmbeddingService
    {
        Task<float[]> GetEmbeddingAsync(string text);
        Task<List<float[]>> GetEmbeddingsAsync(IEnumerable<string> texts);
    }
}