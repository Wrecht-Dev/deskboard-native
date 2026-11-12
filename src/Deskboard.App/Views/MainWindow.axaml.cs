using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Deskboard.App.ViewModels;

namespace Deskboard.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged);
    }

    private async void OnReblog(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not PostItemViewModel item) return;
        if (DataContext is not MainViewModel vm) return;
        var reblog = new ReblogViewModel(item, vm.Client, vm.Blogs);
        var window = new ReblogWindow { DataContext = reblog };
        reblog.Completed += (_, ok) =>
        {
            if (ok)
            {
                vm.Status = $"Reblogged to {reblog.SelectedBlog}.";
                vm.HasStatus = true;
            }
            window.Close();
        };
        await window.ShowDialog(this);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var scroller = FeedList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (scroller is null) return;
        var offset = scroller.Offset.Y;
        var extent = scroller.Extent.Height;
        var viewport = scroller.Viewport.Height;
        if (extent > 0 && offset + viewport > extent - 600)
            _ = vm.LoadMoreAsync();
    }
}
