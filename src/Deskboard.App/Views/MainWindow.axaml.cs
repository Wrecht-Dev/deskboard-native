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
        if (e.Source is not Control control || control.DataContext is not PostItemViewModel) return;
        OpenDetail(control);
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

    private async void OnSettings(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        await vm.EnsureIdentityAsync();
        var settings = new SettingsViewModel(vm.Client, vm.Identity, vm.Blogs, vm.Filters);
        var window = new SettingsWindow { DataContext = settings };
        await window.ShowDialog(this);
    }

    private async void OnSubmitPost(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || !vm.CanSubmitBlog) return;
        var composer = ComposerViewModel.ForSubmission(vm.Client, vm.Headline);
        var window = new ComposerWindow { DataContext = composer };
        composer.Completed += (_, label) => { vm.Status = label; vm.HasStatus = true; };
        await window.ShowDialog(this);
    }

    private async void OnEditPost(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if ((sender as Control)?.DataContext is not PostItemViewModel item) return;
        if (!item.IsMine) return;
        var composer = ComposerViewModel.ForEdit(vm.Client, vm.Blogs, vm.PrimaryBlog, item);
        var window = new ComposerWindow { DataContext = composer };
        composer.Completed += (_, label) => { vm.Status = label; vm.HasStatus = true; };
        await window.ShowDialog(this);
    }

    private void OnReplyToPost(object? sender, RoutedEventArgs e) => OpenDetail(sender);

    private void OnSharePost(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not PostItemViewModel item) return;
        if (string.IsNullOrEmpty(item.Post.PostUrl)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.Post.PostUrl)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception) { }
    }

    private void OpenDetail(object? sender)
    {
        if ((sender as Control)?.DataContext is not PostItemViewModel item) return;
        if (DataContext is not MainViewModel vm) return;
        var window = new PostDetailWindow
        {
            DataContext = new PostDetailViewModel(item, vm.Client, vm.PrimaryBlog),
        };
        window.Show(this);
    }

    private void OnPlayMedia(object? sender, RoutedEventArgs e)
    {
        // Inline video/audio decoding is not wired yet; hand the media URL to the
        // system player so the post is still usable.
        if ((sender as Control)?.DataContext is not MediaItemViewModel media) return;
        if (string.IsNullOrEmpty(media.Url)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(media.Url)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception) { /* no handler for this scheme */ }
    }

    private void OnPinnedTab(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if ((sender as Control)?.DataContext is Services.PinnedTab tab)
            _ = vm.OpenPinnedTabAsync(tab);
    }

    private async void OnAddPinned(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var dialog = new AddPinnedTabWindow();
        await dialog.ShowDialog(this);
        if (dialog.Result is { } tab) vm.AddPinnedTab(tab);
    }

    private void OnMediaPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not PostItemViewModel item) return;
        if (item.Media.Count == 0) return;
        var media = item.Media[0];
        _ = ViewerWindow.ShowAsync(this, media.Url, media.IsGif);
    }

    private void OnMoveUp(object? sender, RoutedEventArgs e) => Move(sender, -1);
    private void OnMoveDown(object? sender, RoutedEventArgs e) => Move(sender, 1);

    private void Move(object? sender, int direction)
    {
        if (DataContext is not MainViewModel vm) return;
        if ((sender as Control)?.DataContext is PostItemViewModel item)
            _ = vm.ReorderQueueAsync(item, direction);
    }

    private void OnToggleBookmark(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if ((sender as Control)?.DataContext is PostItemViewModel item) vm.ToggleBookmark(item);
    }

    private void OnOpenBookmark(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if ((sender as Control)?.DataContext is Services.Bookmark bookmark) vm.OpenBookmark(bookmark);
    }

    private void OnTrendingTag(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if ((sender as Control)?.DataContext is string tag) _ = vm.OpenTagAsync(tag);
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

    private void OnNotificationsScroll(object? sender, ScrollChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || sender is not ScrollViewer scroller) return;
        if (scroller.Extent.Height <= 0) return;
        if (scroller.Offset.Y + scroller.Viewport.Height > scroller.Extent.Height - 400)
            _ = vm.LoadMoreNotificationsAsync();
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
