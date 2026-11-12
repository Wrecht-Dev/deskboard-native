using Avalonia.Controls;
using Avalonia.Interactivity;
using Deskboard.App.Services;

namespace Deskboard.App.Views;

public partial class AddPinnedTabWindow : Window
{
    public PinnedTab? Result { get; private set; }

    public AddPinnedTabWindow() => InitializeComponent();

    private void OnAdd(object? sender, RoutedEventArgs e)
    {
        var kind = KindBox.SelectedIndex switch { 1 => "search", 2 => "your-tags", _ => "tag" };
        var title = (TitleBox.Text ?? "").Trim();
        var value = (ValueBox.Text ?? "").Trim().TrimStart('#');

        if (kind != "your-tags" && value.Length == 0)
        {
            ErrorText.Text = kind == "tag" ? "Enter a tag name." : "Enter a search query.";
            ErrorText.IsVisible = true;
            return;
        }
        if (title.Length == 0) title = kind == "your-tags" ? "Your tags" : (kind == "tag" ? "#" + value : value);

        Result = new PinnedTab
        {
            Title = title,
            Kind = kind,
            Value = value,
            Sort = SortBox.SelectedIndex == 1 ? "top" : "latest",
        };
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
