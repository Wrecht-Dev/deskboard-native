using System.Collections.Generic;
using Deskboard.App.ViewModels;
using Deskboard.Core.Api;
using Deskboard.Core.Models;
using Xunit;

namespace Deskboard.Tests;

public class ReblogModeTests
{
    private static PostItemViewModel Item()
    {
        var post = new Post { BlogName = "artspotting", Content = new List<Block>() };
        return new PostItemViewModel(post, new MainViewModel());
    }

    [Fact]
    public void DefaultsToWithText()
    {
        var vm = new ReblogViewModel(Item(), new TumblrClient(), new[] { "adelaidesf" });
        Assert.Equal(ReblogMode.WithText, vm.Mode);
        Assert.True(vm.IsWithText);
        Assert.False(vm.IsSchedule);
        Assert.Equal("adelaidesf", vm.SelectedBlog);
    }

    [Fact]
    public void ModeSwitchesExposeVisibilityFlags()
    {
        var vm = new ReblogViewModel(Item(), new TumblrClient(), new[] { "adelaidesf" });
        vm.SetModeCommand.Execute("schedule");
        Assert.True(vm.IsSchedule);
        Assert.False(vm.IsWithText);
        vm.SetModeCommand.Execute("now");
        Assert.True(vm.IsNow);
        Assert.False(vm.IsSchedule);
        vm.SetModeCommand.Execute("withtext");
        Assert.True(vm.IsWithText);
    }

    [Fact]
    public void ReblogWithoutABlogFailsBeforeNetwork()
    {
        var vm = new ReblogViewModel(Item(), new TumblrClient(), System.Array.Empty<string>());
        vm.ReblogCommand.Execute(null);
        Assert.True(vm.HasError);
        Assert.Contains("Choose a blog", vm.Error);
    }

    [Fact]
    public void ScheduleWithoutDateFailsBeforeNetwork()
    {
        var vm = new ReblogViewModel(Item(), new TumblrClient(), new[] { "adelaidesf" });
        vm.Mode = ReblogMode.Schedule;
        vm.PublishAt = null;
        vm.ReblogCommand.Execute(null);
        Assert.True(vm.HasError);
        Assert.Contains("Pick when", vm.Error);
    }
}
