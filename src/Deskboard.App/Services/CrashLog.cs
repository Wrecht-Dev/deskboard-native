using System;
using System.IO;
using Deskboard.Core.Api;

namespace Deskboard.App.Services;

/// <summary>Last-resort crash logging so a failure leaves evidence instead of silence.</summary>
public static class CrashLog
{
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "deskboard-native", "crash.log");

    public static void Write(Exception exception, string context = "startup")
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            File.AppendAllText(FilePath,
                $"[{DateTimeOffset.Now:O}] {context}\n{exception}\n\n");
        }
        catch (Exception) { }
    }
}
