namespace AIShoppingAssistant.Services;

public sealed class LocalAIUnavailableException : InvalidOperationException
{
    public LocalAIUnavailableException(Exception innerException)
        : base("Ollama is unavailable. Start Ollama and ensure model 'llama3.2:3b' is installed (ollama pull llama3.2:3b).", innerException)
    {
    }
}
