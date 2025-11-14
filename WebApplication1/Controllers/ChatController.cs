using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WebApplication1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly ILogger<ChatController> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public ChatController(ILogger<ChatController> logger, IConfiguration configuration, HttpClient httpClient)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(30); // Reduced timeout for faster feedback
        }

        [HttpGet("test-ollama")]
        [AllowAnonymous]
        public async Task<IActionResult> TestOllama()
        {
            try
            {
                var ollamaUrl = _configuration["Ollama:Url"] ?? "http://127.0.0.1:11434";
                _logger.LogInformation("Attempting to connect to Ollama at {Url}/api/tags", ollamaUrl);
                var response = await _httpClient.GetAsync($"{ollamaUrl}/api/tags");
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Ollama /api/tags response: {Content}", content);
                return Ok(new { response = content });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error connecting to Ollama");
                return StatusCode(500, new { error = "Failed to connect to Ollama", details = ex.Message });
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request to Ollama timed out");
                return StatusCode(500, new { error = "Ollama request timed out", details = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error connecting to Ollama");
                return StatusCode(500, new { error = "Unexpected error", details = ex.Message });
            }
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ChatResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            if (string.IsNullOrEmpty(request.Message) || request.Message.Length > 1000)
            {
                _logger.LogWarning("Invalid input message: {Message}", request.Message);
                return BadRequest(new { error = "Message is empty or exceeds 1000 characters" });
            }

            try
            {
                var ollamaUrl = _configuration["Ollama:Url"] ?? "http://127.0.0.1:11434";
                var model = _configuration["Ollama:Model"] ?? "llama3.1:8b";
                _logger.LogInformation("Using Ollama URL: {Url}, Model: {Model}", ollamaUrl, model);

                var prompt = @"You are a friendly chatbot for a weather app. Respond to the user's message in a concise, helpful manner, using JSON format with a single 'response' key containing a string summary of the weather. Example: { ""response"": ""Sunny, 25°C, 60% humidity"" }. If the query is not weather-related, respond with { ""response"": ""I can only assist with weather queries. Please ask about the weather!"" }. Ensure the response is valid JSON and contains only the JSON object.";

                var requestBody = new 
                {
                    model,
                    prompt = $"{prompt}\nUser: {request.Message}",
                    max_tokens = 1000,
                    temperature = 0.7f,
                    stream = false // Disable streaming to simplify response handling
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                int retries = 3;
                for (int i = 0; i < retries; i++)
                {
                    try
                    {
                        _logger.LogInformation("Attempt {Retry}/{Retries} - Sending message to Ollama: {Message}", i + 1, retries, request.Message);
                        var response = await _httpClient.PostAsync($"{ollamaUrl}/api/generate", content);

                        if (!response.IsSuccessStatusCode)
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            _logger.LogWarning("Ollama request failed with status {StatusCode}: {Error}", response.StatusCode, errorContent);
                            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                                throw new HttpRequestException("429", null, System.Net.HttpStatusCode.TooManyRequests);
                            return StatusCode((int)response.StatusCode, new { error = "Ollama API error", details = errorContent });
                        }

                        var responseText = await response.Content.ReadAsStringAsync();
                        _logger.LogInformation("Received raw response from Ollama: {Response}", responseText);

                        try
                        {
                            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseText);
                            if (jsonResponse.TryGetProperty("response", out var responseProp))
                            {
                                var responseContent = responseProp.GetString();
                                if (string.IsNullOrEmpty(responseContent))
                                {
                                    _logger.LogError("Empty response content from Ollama");
                                    return StatusCode(500, new { error = "Empty response content from Ollama" });
                                }

                                try
                                {
                                    // Parse response content as JSON
                                    var parsedResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                                    if (parsedResponse.TryGetProperty("response", out var responseField))
                                    {
                                        return Ok(new ChatResponse { Response = responseContent });
                                    }
                                    else
                                    {
                                        // Handle multi-field JSON
                                        string summary = responseContent;
                                        if (parsedResponse.TryGetProperty("weather", out var weather) &&
                                            parsedResponse.TryGetProperty("temperature", out var temp))
                                        {
                                            summary = $"{weather.GetString()}, {temp.GetString()}";
                                            if (parsedResponse.TryGetProperty("humidity", out var humidity))
                                            {
                                                summary += $", {humidity.GetString()} humidity";
                                            }
                                            if (parsedResponse.TryGetProperty("city", out var city))
                                            {
                                                summary = $"{city.GetString()}: {summary}";
                                            }
                                            responseContent = JsonSerializer.Serialize(new { response = summary });
                                        }
                                        else
                                        {
                                            _logger.LogWarning("Unexpected JSON structure: {Content}", responseContent);
                                            responseContent = JsonSerializer.Serialize(new { response = responseContent });
                                        }
                                        return Ok(new ChatResponse { Response = responseContent });
                                    }
                                }
                                catch (JsonException ex)
                                {
                                    _logger.LogWarning("Failed to parse response content as JSON: {Content}, Error: {Error}", responseContent, ex.Message);
                                    responseContent = JsonSerializer.Serialize(new { response = responseContent });
                                    return Ok(new ChatResponse { Response = responseContent });
                                }
                            }
                            else
                            {
                                _logger.LogError("No 'response' property in Ollama response");
                                return StatusCode(500, new { error = "Invalid response format from Ollama" });
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogError(ex, "Failed to parse Ollama response: {Response}", responseText);
                            return StatusCode(500, new { error = "Failed to parse Ollama response", details = ex.Message });
                        }
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogWarning("Rate limit hit, retrying {Retry}/{Retries}", i + 1, retries);
                        if (i == retries - 1) throw;
                        await Task.Delay(1000 * (i + 1));
                    }
                    catch (TaskCanceledException ex)
                    {
                        _logger.LogError(ex, "Request to Ollama timed out: {Message}", request.Message);
                        return StatusCode(500, new { error = "Ollama request timed out", details = ex.Message });
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogError(ex, "HTTP error connecting to Ollama: {Message}", ex.Message);
                        return StatusCode(500, new { error = "Failed to connect to Ollama", details = ex.Message });
                    }
                }

                return StatusCode(429, new { error = "Rate limit exceeded after retries" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat request: {Message}", request.Message);
                return StatusCode(500, new { error = "Error processing chat request", details = ex.Message });
            }
        }
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
    }

    public class ChatResponse
    {
        public string Response { get; set; } = string.Empty;
    }
}