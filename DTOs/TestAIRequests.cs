namespace AIShoppingAssistant.DTOs;

public sealed class TestAiChatRequest
{
    public string Message { get; set; } = string.Empty;
}

public sealed class TestAiParseRequest
{
    public string Query { get; set; } = string.Empty;
}

public sealed class TestAiPreferencesRequest
{
    public List<string> Colors { get; set; } = new();
    public List<string> Styles { get; set; } = new();
}

public sealed class TestAiRecommendRequest
{
    public decimal Budget { get; set; }
    public TestAiPreferencesRequest Preferences { get; set; } = new();
}
