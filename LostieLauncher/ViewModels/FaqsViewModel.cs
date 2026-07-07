using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LostieLauncher.Content;
using LostieLauncher.Models;
using System.Collections.ObjectModel;

namespace LostieLauncher.ViewModels;

public partial class FaqsViewModel : ObservableObject
{
    private readonly SettingsViewModel _settingsViewModel;
    private List<FaqItem> _allFaqs = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasNoResults { get; set; }

    public ObservableCollection<FaqItem> FilteredFaqs { get; } = [];

    public FaqsViewModel(SettingsViewModel settingsViewModel)
    {
        _settingsViewModel = settingsViewModel;
        LoadFaqs();

        settingsViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.Language)) LoadFaqs();
        };
    }

    private void LoadFaqs()
    {
        _allFaqs = [.. Faqs.For(_settingsViewModel.Language).Entries.Select(entry => new FaqItem(entry.Question, entry.Answer))];
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        var isSearching = term.Length > 0;

        FilteredFaqs.Clear();
        foreach (var faq in _allFaqs)
        {
            if (isSearching && !SearchMatcher.Contains(faq.Question, term) && !SearchMatcher.Contains(faq.Answer, term)) continue;

            faq.IsExpanded = isSearching;
            FilteredFaqs.Add(faq);
        }

        HasNoResults = FilteredFaqs.Count == 0;
    }
}
