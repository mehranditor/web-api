using WebApplication1.Models.DTOs;
using WebApplication1.Services;

namespace WebApplication1.Services
{
    public class InMemoryVectorStore : IVectorStore
    {
        private readonly Dictionary<string, DocumentChunk> _documents = new();
        private readonly ILogger<InMemoryVectorStore> _logger;

        public InMemoryVectorStore(ILogger<InMemoryVectorStore> logger)
        {
            _logger = logger;
        }

        public Task<string> UpsertAsync(DocumentChunk chunk)
        {
            _documents[chunk.Id] = chunk;
            _logger.LogDebug("Upserted document chunk {Id}", chunk.Id);
            return Task.FromResult(chunk.Id);
        }

        public Task<List<SearchResult>> SearchAsync(float[] queryEmbedding, int topK = 5, float minScore = 0.0f)
        {
            if (queryEmbedding?.Length == 0)
            {
                return Task.FromResult(new List<SearchResult>());
            }

            var results = new List<SearchResult>();

            foreach (var doc in _documents.Values)
            {
                if (doc.Embedding?.Length == 0 || doc.Embedding.Length != queryEmbedding.Length)
                    continue;

                var similarity = CosineSimilarity(queryEmbedding, doc.Embedding);

                if (similarity >= minScore)
                {
                    results.Add(new SearchResult
                    {
                        Id = doc.Id,
                        Content = doc.Content,
                        Score = similarity,
                        Metadata = doc.Metadata
                    });
                }
            }

            var topResults = results
                .OrderByDescending(r => r.Score)
                .Take(topK)
                .ToList();

            _logger.LogDebug("Found {Count} results for similarity search (topK: {TopK})", topResults.Count, topK);

            return Task.FromResult(topResults);
        }

        public Task<bool> DeleteAsync(string id)
        {
            var removed = _documents.Remove(id);
            if (removed)
            {
                _logger.LogDebug("Deleted document chunk {Id}", id);
            }
            return Task.FromResult(removed);
        }

        public Task<List<DocumentChunk>> GetAllAsync()
        {
            return Task.FromResult(_documents.Values.ToList());
        }

        public Task ClearAsync()
        {
            var count = _documents.Count;
            _documents.Clear();
            _logger.LogInformation("Cleared {Count} document chunks from vector store", count);
            return Task.CompletedTask;
        }

        private static float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length)
                return 0f;

            float dotProduct = 0f;
            float normA = 0f;
            float normB = 0f;

            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            if (normA == 0f || normB == 0f)
                return 0f;

            return dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
        }
    }
}