namespace AIShoppingAssistant.Services;

public sealed class AIServiceFactory
{
    private readonly LocalAIService _localAiService;
    private readonly MockAIService _mockAiService;
    private readonly ILogger<AIServiceFactory> _logger;

    public AIServiceFactory(
        LocalAIService localAiService,
        MockAIService mockAiService,
        ILogger<AIServiceFactory> logger)
    {
        _localAiService = localAiService;
        _mockAiService = mockAiService;
        _logger = logger;
    }

    public IAIService Create()
    {
        if (_localAiService.IsAvailable)
            return _localAiService;

        _logger.LogWarning("Local AI model is unavailable. Using mock AI mode.");
        return _mockAiService;
    }
}
