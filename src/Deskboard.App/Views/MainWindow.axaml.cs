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

    private void OnOpenPost(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (e.Source is not Control control || control.DataContext is not PostItemViewModel item) return;
        if (DataContext is not MainViewModel vm) return;
        var window = new PostDetailWindow
        {
            DataContext = new PostDetailViewModel(item, vm.Client, vm.PrimaryBlog),
        };
        window.Show(this);
    }

    private static PostItemViewModel? ItemFrom(object? sender)
        => (sender as Control)?.DataContext as PostItemViewModel;

    private void OnMutePost(object? sender, RoutedEventArgs e)
    {
        if (ItemFrom(sender) is { } item && DataContext is MainViewModel vm) _ = vm.MutePostAsync(item);
    }

    private async void OnBlockBlog(object? sender, RoutedEventArgs e)
    {
        if (ItemFrom(sender) is not { } item || DataContext is not MainViewModel vm) return;
        if (await ConfirmWindow.AskAsync(this, $"Block {item.Post.BlogName}? Their posts will disappear from your feed.", "Block"))
            await vm.BlockBlogAsync(item);
    }

    private void OnReportPost(object? sender, RoutedEventArgs e)
    {
        if (ItemFrom(sender) is { } item && DataContext is MainViewModel vm) _ = vm.ReportPostAsync(item);
    }

    private async void OnDeletePost(object? sender, RoutedEventArgs e)
    {
        if (ItemFrom(sender) is not { } item || DataContext is not MainViewModel vm) return;
        if (await ConfirmWindow.AskAsync(this, "Delete this post permanently?", "Delete"))
            await vm.DeletePostAsync(item);
    }

    private void OnSearchKey(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key != Avalonia.Input.Key.Enter) return;
        if (DataContext is MainViewModel vm) _ = vm.SearchAsync(SearchBox.Text ?? "");
    }

    private async void OnCreate(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var composer = new ComposerViewModel(vm.Client, vm.Blogs);
        var window = new ComposerWindow { DataContext = composer };
        composer.Completed += (_, label) =>
        {
            vm.Status = label;
            vm.HasStatus = true;
        };
        await window.ShowDialog(this);
    }

    private void OnFollow(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) _ = vm.ToggleFollowAsync();
    }

    private async void OnAsk(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || !vm.CanAsk) return;
        var window = new AskWindow
        {
            DataContext = new AskViewModel(vm.Client, vm.Headline, vm.CanAskAnon),
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
