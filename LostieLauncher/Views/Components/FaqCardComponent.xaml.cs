using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace LostieLauncher.Views.Components;

public partial class FaqCardComponent : UserControl
{
    public FaqCardComponent() => InitializeComponent();

    public static readonly DependencyProperty QuestionProperty =
        DependencyProperty.Register(nameof(Question), typeof(string), typeof(FaqCardComponent),
            new PropertyMetadata(string.Empty, OnContentChanged));

    public string Question
    {
        get => (string)GetValue(QuestionProperty);
        set => SetValue(QuestionProperty, value);
    }

    public static readonly DependencyProperty AnswerProperty =
        DependencyProperty.Register(nameof(Answer), typeof(string), typeof(FaqCardComponent),
            new PropertyMetadata(string.Empty, OnContentChanged));

    public string Answer
    {
        get => (string)GetValue(AnswerProperty);
        set => SetValue(AnswerProperty, value);
    }

    public static readonly DependencyProperty HighlightProperty =
        DependencyProperty.Register(nameof(Highlight), typeof(string), typeof(FaqCardComponent),
            new PropertyMetadata(string.Empty, OnContentChanged));

    public string Highlight
    {
        get => (string)GetValue(HighlightProperty);
        set => SetValue(HighlightProperty, value);
    }

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(FaqCardComponent),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => IsExpanded = !IsExpanded;

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FaqCardComponent)d).RenderTexts();

    private void RenderTexts()
    {
        var term = Highlight?.Trim() ?? string.Empty;
        RenderQuestion(term);
        RenderAnswer(term);
    }

    private void RenderQuestion(string term)
    {
        QuestionText.Inlines.Clear();
        foreach (var run in BuildHighlightedRuns(Question, term)) QuestionText.Inlines.Add(run);
    }

    private void RenderAnswer(string term)
    {
        AnswerText.Inlines.Clear();

        foreach (var segment in LinkTextParser.Parse(Answer))
        {
            if (!segment.IsLink)
            {
                foreach (var run in BuildHighlightedRuns(segment.Text, term)) AnswerText.Inlines.Add(run);
                continue;
            }

            var url = segment.Url;
            var hyperlink = new Hyperlink
            {
                Style = (Style)FindResource("FaqLinkStyle"),
                ToolTip = url
            };
            foreach (var run in BuildHighlightedRuns(segment.Text, term, isLink: true)) hyperlink.Inlines.Add(run);
            hyperlink.Click += (_, _) => UrlLauncher.OpenHttps(url);
            AnswerText.Inlines.Add(hyperlink);
        }
    }

    private static IEnumerable<Run> BuildHighlightedRuns(string text, string term, bool isLink = false)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        var index = 0;

        foreach (var (start, length) in SearchMatcher.FindMatches(text, term))
        {
            if (start > index) yield return new Run(text[index..start]);

            var highlighted = new Run(text[start..(start + length)]) { FontWeight = FontWeights.Bold };
            if (!isLink)
            {
                // Hyperlinks already come underlined and accent-colored; plain text gets both here.
                highlighted.TextDecorations = TextDecorations.Underline;
                highlighted.SetResourceReference(TextElement.ForegroundProperty, "PrimaryFgBrush");
            }
            yield return highlighted;

            index = start + length;
        }

        if (index < text.Length) yield return new Run(text[index..]);
    }
}
