using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models.DTOs
{
    public class SemanticSearchRequest
    {
        [Required]
        [StringLength(1000, ErrorMessage = "Query cannot exceed 1000 characters")]
        public string Query { get; set; } = string.Empty;

        [Range(1, 50, ErrorMessage = "TopK must be between 1 and 50")]
        public int TopK { get; set; } = 5;

        public float MinScore { get; set; } = 0.7f;
    }




    public class DocumentChunk
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class SearchResult
    {
        public string Id { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public float Score { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class LogSummarizationRequest
    {
        public string LogContent { get; set; } = string.Empty;
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class LogSummarizationResponse
    {
        public string Summary { get; set; } = string.Empty;
        public List<LogEntry> Entries { get; set; } = new List<LogEntry>();
        public string Model { get; set; } = string.Empty;
    }

    public class LogEntry
    {
        public string Timestamp { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
    }

    public class RAGRequest
    {
        [Required]
        [StringLength(1000)]
        public string Question { get; set; } = string.Empty;
        public bool IncludeSources { get; set; } = true;
        public int MaxSources { get; set; } = 3;
        public string Model { get; set; } = "llama3.1:8b";
    }

}