using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using RagPlatform.Api.Contracts.Rag;
using RagPlatform.Api.Options;

namespace RagPlatform.Api.Services;

public sealed class FoundryInferenceClient
{
    private const string FoundryScope = "https://ai.azure.com/.default";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly TokenCredential _credential;
    private readonly FoundryOptions _options;

    public FoundryInferenceClient(
        IHttpClientFactory httpClientFactory,
        TokenCredential credential,
        Microsoft.Extensions.Options.IOptions<FoundryOptions> options)
    {
        _httpClient = httpClientFactory.CreateClient("FoundryInference");
        _credential = credential;
        _options = options.Value;
    }

    public async Task<(IReadOnlyList<float> Vector, int? InputTokenCount)> GenerateEmbeddingAsync(
        string deployment,
        string input,
        CancellationToken cancellationToken)
    {
        var response = await PostAsync<EmbeddingResponse>(
            "embeddings",
            new { model = deployment, input },
            cancellationToken);
        var vector = response.Data.FirstOrDefault()?.Embedding
            ?? throw new InvalidOperationException("The Foundry embedding deployment returned no vector.");
        return (vector, response.Usage?.PromptTokens);
    }

    public async Task<(string Answer, int? InputTokenCount, int? OutputTokenCount)> CompleteChatAsync(
        string deployment,
        IReadOnlyList<RagChatMessage> messages,
        int maxOutputTokens,
        CancellationToken cancellationToken)
    {
        var response = await PostAsync<ChatCompletionResponse>(
            "chat/completions",
            new
            {
                model = deployment,
                messages = messages.Select(message => new
                {
                    role = message.Role.ToLowerInvariant(),
                    content = message.Content
                }),
                max_tokens = maxOutputTokens
            },
            cancellationToken);
        var answer = response.Choices.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new InvalidOperationException("The Foundry chat deployment returned no text answer.");
        }

        return (answer, response.Usage?.PromptTokens, response.Usage?.CompletionTokens);
    }

    private async Task<TResponse> PostAsync<TResponse>(
        string operation,
        object request,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        if (!Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(endpoint.UserInfo)
            || !string.IsNullOrEmpty(endpoint.Query)
            || !string.IsNullOrEmpty(endpoint.Fragment)
            || !endpoint.AbsolutePath.TrimEnd('/').EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Foundry:Endpoint must be an absolute HTTPS OpenAI-compatible endpoint ending in /openai/v1.");
        }

        var accessToken = await _credential.GetTokenAsync(
            new TokenRequestContext([FoundryScope]),
            cancellationToken);
        var requestUri = new Uri($"{_options.Endpoint.TrimEnd('/')}/{operation}");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Foundry inference failed with HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                null,
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken)
            ?? throw new JsonException("The Foundry inference response was empty.");
    }

    private sealed class EmbeddingResponse
    {
        public List<EmbeddingDatum> Data { get; set; } = [];

        public InferenceUsage? Usage { get; set; }
    }

    private sealed class EmbeddingDatum
    {
        public float[]? Embedding { get; set; }
    }

    private sealed class ChatCompletionResponse
    {
        public List<ChatChoice> Choices { get; set; } = [];

        public InferenceUsage? Usage { get; set; }
    }

    private sealed class ChatChoice
    {
        public ChatMessageResult? Message { get; set; }
    }

    private sealed class ChatMessageResult
    {
        public string? Content { get; set; }
    }

    private sealed class InferenceUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int? PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int? CompletionTokens { get; set; }
    }
}
