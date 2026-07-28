using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AIShoppingAssistant.Services;

public sealed class ModelHealthCheck : IHealthCheck
{
    private const string ModelName = "llama3.2:3b";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ModelHealthCheck> _logger;

    public ModelHealthCheck(
        IHttpClientFactory httpClientFactory,
        ILogger<ModelHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("LocalAI");

        try
        {
            var tagsResponse = await client.GetAsync("/api/tags", cancellationToken);
            if (!tagsResponse.IsSuccessStatusCode)
            {
                return Unhealthy(
                    $"Ollama tags endpoint returned {(int)tagsResponse.StatusCode} {tagsResponse.ReasonPhrase}.");
            }

            await using var tagsStream = await tagsResponse.Content.ReadAsStreamAsync(cancellationToken);
            var tags = await JsonSerializer.DeserializeAsync<OllamaTagsResponse>(
                tagsStream,
                JsonOptions,
                cancellationToken);

            var modelAvailable = tags?.Models?.Any(model =>
                string.Equals(model.Name, ModelName, StringComparison.OrdinalIgnoreCase)) == true;

            if (!modelAvailable)
            {
                return Unhealthy(
                    $"Ollama is running, but model '{ModelName}' is not available. Run 'ollama pull {ModelName}'.");
            }

            var promptResponse = await SendTestPromptAsync(client, cancellationToken);
            if (string.IsNullOrWhiteSpace(promptResponse))
            {
                return Unhealthy($"Model '{ModelName}' responded with an empty message.");
            }

            _logger.LogInformation(
                "Local AI health check succeeded. Model {ModelName} responded to the test prompt.",
                ModelName);

            return HealthCheckResult.Healthy(
                $"Ollama is running and model '{ModelName}' responded successfully.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return Unhealthy(
                $"Ollama endpoint was not found while checking model '{ModelName}'.",
                ex);
        }
        catch (HttpRequestException ex)
        {
            return Unhealthy(
                $"Could not connect to Ollama at '{client.BaseAddress}'. Ensure Ollama is running and listening on port 11434.",
                ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return Unhealthy(
                $"Timed out while checking Ollama model '{ModelName}'.",
                ex);
        }
        catch (JsonException ex)
        {
            return Unhealthy(
                $"Ollama returned invalid JSON while checking model '{ModelName}'.",
                ex);
        }
        catch (Exception ex)
        {
            return Unhealthy(
                $"Unexpected error while checking Ollama model '{ModelName}'.",
                ex);
        }
    }

    private static async Task<string?> SendTestPromptAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var request = new OllamaGenerateRequest(ModelName, "Hello", Stream: false);
        using var content = new StringContent(
            JsonSerializer.Serialize(request, JsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/api/generate", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Ollama generate endpoint returned {(int)response.StatusCode} {response.ReasonPhrase}.",
                null,
                response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var generateResponse = await JsonSerializer.DeserializeAsync<OllamaGenerateResponse>(
            stream,
            JsonOptions,
            cancellationToken);

        return generateResponse?.Response;
    }

    private HealthCheckResult Unhealthy(string message, Exception? exception = null)
    {
        _logger.LogError(
            exception,
            "Local AI health check failed for model {ModelName}: {Message}",
            ModelName,
            message);

        return HealthCheckResult.Unhealthy(message, exception);
    }

    private sealed record OllamaTagsResponse(List<OllamaModel>? Models);
    private sealed record OllamaModel(string Name);
    private sealed record OllamaGenerateRequest(string Model, string Prompt, bool Stream);
    private sealed record OllamaGenerateResponse(string? Response);
}
