using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Deskboard.App.ViewModels;

namespace Deskboard.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged);
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
