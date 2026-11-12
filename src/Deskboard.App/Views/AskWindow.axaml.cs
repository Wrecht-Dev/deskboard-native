using Avalonia.Controls;
using Deskboard.App.ViewModels;

namespace Deskboard.App.Views;

public partial class AskWindow : Window
{
    public AskWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is AskViewModel vm)
                vm.Completed += (_, ok) => { if (ok) Close(); };
        };
    }
}
