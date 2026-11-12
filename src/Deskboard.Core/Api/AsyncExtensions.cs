namespace Deskboard.Core.Api;

internal static class AsyncExtensions
{
    /// <summary>Await a Task&lt;T&gt; for its side effect and discard the value.</summary>
    public static async Task AsVoid<T>(this Task<T> task) => _ = await task.ConfigureAwait(false);
}
