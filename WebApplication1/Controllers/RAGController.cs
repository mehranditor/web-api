using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models.DTOs; // Import DTOs
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RAGController : ControllerBase
    {
        private readonly IRAGService _ragService;
        private readonly ILogger<RAGController> _logger;

        public RAGController(IRAGService ragService, ILogger<RAGController> logger)
        {
            _ragService = ragService;
            _logger = logger;
        }

        [HttpPost("index")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> IndexDocument([FromBody] IndexDocumentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new { error = "Content cannot be empty" });
            }
            try
            {
                var documentId = await _ragService.IndexDocumentAsync(request.Content, request.Metadata);
                return Ok(new
                {
                    documentId = documentId,
                    message = "Document indexed successfully",
                    contentLength = request.Content.Length
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing document");
                return StatusCode(500, new { error = "Failed to index document" });
            }
        }

        [HttpPost("search")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<SearchResult>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SemanticSearch([FromBody] SemanticSearchRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                var results = await _ragService.SemanticSearchAsync(request);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing semantic search");
                return StatusCode(500, new { error = "Failed to perform search" });
            }
        }

        [HttpDelete("document/{documentId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteDocument(string documentId)
        {
            try
            {
                var deleted = await _ragService.DeleteDocumentAsync(documentId);
                if (!deleted)
                {
                    return NotFound(new { error = "Document not found" });
                }
                return Ok(new { message = "Document deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document {DocumentId}", documentId);
                return StatusCode(500, new { error = "Failed to delete document" });
            }
        }

        [HttpDelete("clear")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ClearIndex()
        {
            try
            {
                await _ragService.ClearIndexAsync();
                return Ok(new { message = "Index cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing index");
                return StatusCode(500, new { error = "Failed to clear index" });
            }
        }

        [HttpGet("health")]
        [AllowAnonymous]
        public IActionResult HealthCheck()
        {
            return Ok(new
            {
                status = "healthy",
                service = "RAG",
                timestamp = DateTime.UtcNow
            });
        }

        [HttpPost("summarize-logs")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SummarizeLogs([FromBody] LogSummarizationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.LogContent))
            {
                return BadRequest(new { error = "Log content cannot be empty" });
            }
            try
            {
                var response = await _ragService.SummarizeLogsAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error summarizing logs");
                return StatusCode(500, new { error = "Failed to summarize logs" });
            }
        }
    }

    // Keep only IndexDocumentRequest here, as others are in DTOs.cs
    public class IndexDocumentRequest
    {
        public string Content { get; set; } = string.Empty;
        public Dictionary<string, object>? Metadata { get; set; }
    }
}