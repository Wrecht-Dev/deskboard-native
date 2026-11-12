using Avalonia.Controls;
using Deskboard.App.ViewModels;

namespace Deskboard.App.Views;

public partial class PostDetailWindow : Window
{
    public PostDetailWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is PostDetailViewModel vm)
                vm.CloseRequested += (_, _) => Close();
        };
    }
}
