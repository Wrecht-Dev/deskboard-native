using Deskboard.App.Services;
var info = await UpdateChecker.CheckAsync(AppInfo.Version);
Console.WriteLine($"current={AppInfo.Version} latest={info?.LatestTag} newer={info is not null && AppInfo.IsNewer(info.LatestTag, AppInfo.Version)}");
