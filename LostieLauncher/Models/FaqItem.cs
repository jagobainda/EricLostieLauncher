using CommunityToolkit.Mvvm.ComponentModel;

namespace LostieLauncher.Models;

public partial class FaqItem(string question, string answer) : ObservableObject
{
    public string Question { get; } = question;
    public string Answer { get; } = answer;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }
}
