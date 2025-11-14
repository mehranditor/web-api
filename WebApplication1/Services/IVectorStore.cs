using WebApplication1.Models.DTOs;

namespace WebApplication1.Services
{
    public interface IVectorStore
    {
        Task<string> UpsertAsync(DocumentChunk chunk);
        Task<List<SearchResult>> SearchAsync(float[] queryEmbedding, int topK = 5, float minScore = 0.0f);
        Task<bool> DeleteAsync(string id);
        Task<List<DocumentChunk>> GetAllAsync();
        Task ClearAsync();
    }
}