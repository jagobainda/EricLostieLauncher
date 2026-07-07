using LostieLauncher.Content;
using LostieLauncher.Models;
using LostieLauncher.Services;
using LostieLauncher.Utils;
using LostieLauncher.ViewModels;

namespace LostieLauncher.Tests.ViewModels;

[Collection(WpfCollection.Name)]
public class FaqsViewModelTests
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IWindowsStartupService _startupService = Substitute.For<IWindowsStartupService>();

    public FaqsViewModelTests(WpfApplicationFixture _) => _settingsService.Load().Returns(new AppSettings());

    private FaqsViewModel CreateSut(out SettingsViewModel settings)
    {
        settings = new SettingsViewModel(_settingsService, _startupService, new GlobalViewModel(), Substitute.For<IUpdateService>());
        return new FaqsViewModel(settings);
    }

    [Fact]
    public void Constructor_LoadsAllFaqsForCurrentLanguage()
    {
        var vm = CreateSut(out var settings);

        vm.FilteredFaqs.Count.ShouldBe(Faqs.For(settings.Language).Entries.Count);
        vm.HasNoResults.ShouldBeFalse();
    }

    [Fact]
    public void SearchText_FiltersByQuestionAndAnswer_AndExpandsMatches()
    {
        var vm = CreateSut(out _);

        vm.SearchText = "clave";

        vm.FilteredFaqs.ShouldNotBeEmpty();
        vm.FilteredFaqs.ShouldAllBe(f => f.IsExpanded);
        vm.FilteredFaqs.ShouldAllBe(f =>
            SearchMatcher.Contains(f.Question, "clave") ||
            SearchMatcher.Contains(f.Answer, "clave"));
    }

    [Fact]
    public void SearchText_WithNoMatches_SetsHasNoResults()
    {
        var vm = CreateSut(out _);

        vm.SearchText = "zzzzzzzz";

        vm.FilteredFaqs.ShouldBeEmpty();
        vm.HasNoResults.ShouldBeTrue();
    }

    [Fact]
    public void ClearSearchCommand_RestoresFullListCollapsed()
    {
        var vm = CreateSut(out var settings);
        vm.SearchText = "clave";

        vm.ClearSearchCommand.Execute(null);

        vm.SearchText.ShouldBe(string.Empty);
        vm.FilteredFaqs.Count.ShouldBe(Faqs.For(settings.Language).Entries.Count);
        vm.FilteredFaqs.ShouldAllBe(f => !f.IsExpanded);
    }

    [Fact]
    public void LanguageChange_ReloadsFaqsInNewLanguage()
    {
        var vm = CreateSut(out var settings);

        settings.Language = AppLanguage.Eng;

        vm.FilteredFaqs.Select(f => f.Question).ShouldBe(new EngFaqs().Entries.Select(e => e.Question));
    }

    [Fact]
    public void AllLanguages_HaveSameNumberOfFaqEntries()
    {
        var expected = new EspFaqs().Entries.Count;

        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            Faqs.For(language).Entries.Count.ShouldBe(expected, $"FAQ count mismatch for language {language}");
        }
    }
}
