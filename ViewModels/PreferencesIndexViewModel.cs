using AIShoppingAssistant.DTOs;
using AIShoppingAssistant.Models;

namespace AIShoppingAssistant.ViewModels;

public class PreferencesIndexViewModel
{
    public UserPreferenceDto Preferences { get; set; } = new();

    public List<SearchHistory> RecentSearchHistory { get; set; } = new();

    public List<string> SmartSuggestions { get; set; } = new();
}
