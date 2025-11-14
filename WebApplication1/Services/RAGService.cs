using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebApplication1.Controllers;
using WebApplication1.Models.DTOs;

namespace WebApplication1.Services
{
    public class RAGService : IRAGService
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly IVectorStore _vectorStore;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RAGService> _logger;

        public RAGService(
            IEmbeddingService embeddingService,
            IVectorStore vectorStore,
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<RAGService> logger)
        {
            _embeddingService = embeddingService;
            _vectorStore = vectorStore;
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            // Set a longer timeout for Ollama requests
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        public async Task<string> IndexDocumentAsync(string content, Dictionary<string, object>? metadata = null)
        {
            try
            {
                var chunks = SplitIntoChunks(content, 500, 50);
                var documentId = Guid.NewGuid().ToString();
                _logger.LogInformation("Indexing document {DocumentId} with {ChunkCount} chunks", documentId, chunks.Count);

                var tasks = chunks.Select(async (chunk, index) =>
                {
                    var embedding = await _embeddingService.GetEmbeddingAsync(chunk);
                    var documentChunk = new DocumentChunk
                    {
                        Id = $"{documentId}_chunk_{index}",
                        Content = chunk,
                        Embedding = embedding,
                        Metadata = new Dictionary<string, object>(metadata ?? new Dictionary<string, object>())
                        {
                            ["document_id"] = documentId,
                            ["chunk_index"] = index,
                            ["chunk_count"] = chunks.Count
                        }
                    };
                    await _vectorStore.UpsertAsync(documentChunk);
                    return documentChunk.Id;
                });

                await Task.WhenAll(tasks);
                _logger.LogInformation("Successfully indexed document {DocumentId}", documentId);
                return documentId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing document");
                throw;
            }
        }

        public async Task<List<SearchResult>> SemanticSearchAsync(SemanticSearchRequest request)
        {
            try
            {
                _logger.LogInformation("Processing semantic search: {Query}", request.Query);
                var queryEmbedding = await _embeddingService.GetEmbeddingAsync(request.Query);
                if (queryEmbedding.Length == 0)
                {
                    _logger.LogWarning("Empty query embedding for query: {Query}", request.Query);
                    return new List<SearchResult>();
                }

                var results = await _vectorStore.SearchAsync(queryEmbedding, request.TopK, request.MinScore);
                _logger.LogInformation("Semantic search returned {Count} results", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing semantic search: {Query}", request.Query);
                throw;
            }
        }

        public async Task<bool> DeleteDocumentAsync(string documentId)
        {
            try
            {
                var allChunks = await _vectorStore.GetAllAsync();
                var documentChunks = allChunks.Where(c =>
                    c.Metadata.ContainsKey("document_id") &&
                    c.Metadata["document_id"].ToString() == documentId).ToList();

                var deleteTasks = documentChunks.Select(chunk => _vectorStore.DeleteAsync(chunk.Id));
                await Task.WhenAll(deleteTasks);

                _logger.LogInformation("Deleted document {DocumentId} with {ChunkCount} chunks",
                    documentId, documentChunks.Count);
                return documentChunks.Count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document {DocumentId}", documentId);
                throw;
            }
        }

        public async Task ClearIndexAsync()
        {
            await _vectorStore.ClearAsync();
        }

        public async Task<LogSummarizationResponse> SummarizeLogsAsync(LogSummarizationRequest request)
        {
            try
            {
                _logger.LogInformation("Starting log summarization, content length: {Length}", request.LogContent.Length);
                var stopwatch = Stopwatch.StartNew();

                // Parse log entries first
                _logger.LogDebug("Parsing log entries");
                var logEntries = ParseLogEntries(request.LogContent);
                _logger.LogInformation("Parsed {EntryCount} log entries in {ElapsedMs}ms", logEntries.Count, stopwatch.ElapsedMilliseconds);

                // Assign colors to entries
                foreach (var entry in logEntries)
                {
                    entry.Color = GetSeverityColor(entry.Severity);
                }

                string summary;
                var model = _configuration.GetValue<string>("Ollama:Model", "llama3.1:8b");

                try
                {
                    // Try to get AI summary with timeout protection
                    _logger.LogInformation("Calling Ollama with model {Model}", model);

                    var prompt = BuildSummarizationPrompt(request.LogContent, logEntries);
                    summary = await CallOllamaWithTimeoutAsync(prompt, model);

                    _logger.LogInformation("Generated AI summary in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get AI summary, generating fallback summary");
                    summary = GenerateFallbackSummary(logEntries);
                }

                // Index the summary
                var metadata = new Dictionary<string, object>(request.Metadata ?? new Dictionary<string, object>())
                {
                    { "topic", "Log Summary" },
                    { "summary_timestamp", DateTime.UtcNow.ToString("o") },
                    { "entry_count", logEntries.Count },
                    { "critical_count", logEntries.Count(e => e.Severity == "CRITICAL") },
                    { "error_count", logEntries.Count(e => e.Severity == "ERROR") },
                    { "warning_count", logEntries.Count(e => e.Severity == "WARNING") }
                };

                try
                {
                    _logger.LogInformation("Indexing summary");
                    await IndexDocumentAsync(summary, metadata);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to index summary, but continuing");
                }

                _logger.LogInformation("Completed summarization in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

                return new LogSummarizationResponse
                {
                    Summary = summary,
                    Entries = logEntries,
                    Model = model
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error summarizing logs: {Message}", ex.Message);
                throw;
            }
        }

        private string BuildSummarizationPrompt(string logContent, List<LogEntry> entries)
        {
            var criticalCount = entries.Count(e => e.Severity == "CRITICAL");
            var errorCount = entries.Count(e => e.Severity == "ERROR");
            var warningCount = entries.Count(e => e.Severity == "WARNING");

            return $@"Analyze these log entries and provide a concise 2-3 sentence summary focusing on the most critical issues:

STATISTICS:
- CRITICAL: {criticalCount} entries
- ERROR: {errorCount} entries  
- WARNING: {warningCount} entries
- TOTAL: {entries.Count} entries

LOG ENTRIES:
{logContent}

Provide a brief technical summary highlighting:
1. Most severe issues first (CRITICAL → ERROR → WARNING)
2. Main problem categories (database, server, memory, etc.)
3. Any patterns or trends

Keep response under 150 words and focus on actionable insights.";
        }

        private async Task<string> CallOllamaWithTimeoutAsync(string prompt, string model)
        {
            var requestBody = new
            {
                model = model,
                prompt = prompt,
                stream = false,
                options = new
                {
                    temperature = 0.3f,
                    top_p = 0.9f,
                    max_tokens = 200
                }
            };

            var ollamaUrl = _configuration.GetValue<string>("Ollama:Url", "http://127.0.0.1:11434");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)); // 60 second timeout
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{ollamaUrl}/api/generate", content, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Ollama API error: {response.StatusCode} - {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(responseContent);

                if (jsonDoc.RootElement.TryGetProperty("response", out var responseProp))
                {
                    var responseText = responseProp.GetString();
                    if (!string.IsNullOrWhiteSpace(responseText))
                    {
                        return responseText.Trim();
                    }
                }

                throw new InvalidOperationException("Empty or invalid response from Ollama");
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("Ollama request timed out after 60 seconds");
            }
        }

        private string GenerateFallbackSummary(List<LogEntry> entries)
        {
            var criticalCount = entries.Count(e => e.Severity == "CRITICAL");
            var errorCount = entries.Count(e => e.Severity == "ERROR");
            var warningCount = entries.Count(e => e.Severity == "WARNING");

            var summary = new StringBuilder();
            summary.Append($"Log analysis: {entries.Count} total entries processed. ");

            if (criticalCount > 0)
            {
                summary.Append($"🔴 {criticalCount} CRITICAL issues detected requiring immediate attention. ");
                var criticalMessages = entries.Where(e => e.Severity == "CRITICAL").Take(2);
                summary.Append($"Key issues: {string.Join(", ", criticalMessages.Select(e => e.Message.Split('.')[0]))}. ");
            }

            if (errorCount > 0)
            {
                summary.Append($"🟠 {errorCount} ERROR(s) found. ");
            }

            if (warningCount > 0)
            {
                summary.Append($"🟡 {warningCount} WARNING(s) detected. ");
            }

            summary.Append("Review recommended for system stability.");

            return summary.ToString();
        }

        private List<LogEntry> ParseLogEntries(string logContent)
        {
            var entries = new List<LogEntry>();

            // Enhanced regex to catch more log formats
            var patterns = new[]
            {
                @"\[(.*?)\]\s*(CRITICAL|ERROR|WARNING|INFO|DEBUG):\s*(.+)",  // [timestamp] LEVEL: message
                @"(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2})\s+(CRITICAL|ERROR|WARNING|INFO|DEBUG)\s+(.+)", // timestamp LEVEL message
                @"(.*?)\s+(CRITICAL|ERROR|WARNING|INFO|DEBUG):\s*(.+)" // flexible format
            };

            foreach (var pattern in patterns)
            {
                var regex = new Regex(pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
                var matches = regex.Matches(logContent);

                foreach (Match match in matches)
                {
                    if (match.Groups.Count >= 4)
                    {
                        var severity = match.Groups[2].Value.ToUpper();

                        // Only process ERROR, CRITICAL, WARNING levels for summarization
                        if (severity == "CRITICAL" || severity == "ERROR" || severity == "WARNING")
                        {
                            entries.Add(new LogEntry
                            {
                                Timestamp = match.Groups[1].Value.Trim(),
                                Severity = severity,
                                Message = match.Groups[3].Value.Trim()
                            });
                        }
                    }
                }

                if (entries.Count > 0) break; // Use first successful pattern
            }

            return entries.OrderBy(e => e.Timestamp).ToList();
        }

        private static string GetSeverityColor(string severity)
        {
            return severity?.ToUpper() switch
            {
                "CRITICAL" => "#FF0000", // Red
                "ERROR" => "#FF8C00",     // Dark Orange
                "WARNING" => "#FFD700",   // Gold
                "INFO" => "#0000FF",      // Blue
                "DEBUG" => "#808080",     // Gray
                _ => "#000000"            // Black (default)
            };
        }

        private static List<string> SplitIntoChunks(string text, int maxChunkSize, int overlapSize)
        {
            var chunks = new List<string>();
            var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            var currentChunk = new StringBuilder();

            foreach (var sentence in sentences)
            {
                var trimmedSentence = sentence.Trim();
                if (string.IsNullOrEmpty(trimmedSentence)) continue;

                if (currentChunk.Length + trimmedSentence.Length > maxChunkSize && currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString().Trim());
                    var overlapText = GetLastWords(currentChunk.ToString(), overlapSize);
                    currentChunk.Clear();
                    currentChunk.Append(overlapText);
                }

                currentChunk.Append(trimmedSentence + ". ");
            }

            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
            }

            return chunks.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        }

        private static string GetLastWords(string text, int maxLength)
        {
            if (text.Length <= maxLength) return text;
            var lastPart = text.Substring(text.Length - maxLength);
            var firstSpaceIndex = lastPart.IndexOf(' ');
            return firstSpaceIndex > 0 ? lastPart.Substring(firstSpaceIndex + 1) : lastPart;
        }
    }
}