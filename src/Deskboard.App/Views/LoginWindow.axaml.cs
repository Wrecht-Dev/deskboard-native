using Avalonia.Controls;
using Deskboard.App.ViewModels;

namespace Deskboard.App.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is LoginViewModel vm)
                vm.Completed += (_, ok) => Close();
        };
    }
}
