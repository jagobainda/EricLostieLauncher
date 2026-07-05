using System.Windows;
using System.Windows.Documents;

namespace LostieLauncher.Views.Components;

public partial class NewsCardComponent : UserControl
{
    public NewsCardComponent() => InitializeComponent();

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(NewsCardComponent), new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(NewsCardComponent),
            new PropertyMetadata(string.Empty, OnDescriptionChanged));

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((NewsCardComponent)d).RenderDescription((string?)e.NewValue);

    private void RenderDescription(string? text)
    {
        DescriptionText.Inlines.Clear();

        foreach (var segment in LinkTextParser.Parse(text))
        {
            if (!segment.IsLink)
            {
                DescriptionText.Inlines.Add(new Run(segment.Text));
                continue;
            }

            var url = segment.Url;
            var hyperlink = new Hyperlink(new Run(segment.Text))
            {
                Style = (Style)FindResource("NewsLinkStyle"),
                ToolTip = url
            };
            hyperlink.Click += (_, _) => UrlLauncher.OpenHttps(url);
            DescriptionText.Inlines.Add(hyperlink);
        }
    }

    public static new readonly DependencyProperty TagProperty =
        DependencyProperty.Register(nameof(Tag), typeof(string), typeof(NewsCardComponent), new PropertyMetadata(string.Empty));

    public new string Tag
    {
        get => (string)GetValue(TagProperty);
        set => SetValue(TagProperty, value);
    }

    public static readonly DependencyProperty DateProperty =
        DependencyProperty.Register(nameof(Date), typeof(DateTime), typeof(NewsCardComponent));

    public DateTime Date
    {
        get => (DateTime)GetValue(DateProperty);
        set => SetValue(DateProperty, value);
    }
}
