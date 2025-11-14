using WebApplication1.Controllers;
using WebApplication1.Models.DTOs;

namespace WebApplication1.Services
{
    public interface IRAGService
    {
        Task<string> IndexDocumentAsync(string content, Dictionary<string, object>? metadata = null);
        Task<List<SearchResult>> SemanticSearchAsync(SemanticSearchRequest request);
        Task<bool> DeleteDocumentAsync(string documentId);
        Task ClearIndexAsync();
        Task<LogSummarizationResponse> SummarizeLogsAsync(LogSummarizationRequest request);
    }
}