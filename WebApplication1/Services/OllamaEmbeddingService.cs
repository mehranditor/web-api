using System.Text;
using System.Text.Json;
using WebApplication1.Services;

namespace WebApplication1.Services
{
    public class OllamaEmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OllamaEmbeddingService> _logger;

        public OllamaEmbeddingService(HttpClient httpClient, IConfiguration configuration, ILogger<OllamaEmbeddingService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            try
            {
                var ollamaUrl = _configuration["Ollama:Url"] ?? "http://127.0.0.1:11434";
                var model = _configuration["Ollama:EmbeddingModel"] ?? "nomic-embed-text";

                var requestBody = new
                {
                    model = model,
                    prompt = text
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{ollamaUrl}/api/embeddings", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Ollama embedding request failed: {StatusCode}", response.StatusCode);
                    return Array.Empty<float>();
                }

                var responseText = await response.Content.ReadAsStringAsync();
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseText);

                if (jsonResponse.TryGetProperty("embedding", out var embeddingProp))
                {
                    var embedding = embeddingProp.EnumerateArray()
                        .Select(x => (float)x.GetDouble())
                        .ToArray();

                    _logger.LogDebug("Generated embedding of dimension {Dimension} for text length {Length}",
                        embedding.Length, text.Length);

                    return embedding;
                }

                _logger.LogError("No embedding property in Ollama response");
                return Array.Empty<float>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding for text: {Text}", text[..Math.Min(100, text.Length)]);
                return Array.Empty<float>();
            }
        }

        public async Task<List<float[]>> GetEmbeddingsAsync(IEnumerable<string> texts)
        {
            var embeddings = new List<float[]>();

            foreach (var text in texts)
            {
                var embedding = await GetEmbeddingAsync(text);
                embeddings.Add(embedding);

                // Small delay to avoid overwhelming Ollama
                await Task.Delay(100);
            }

            return embeddings;
        }
    }
}