using System;
using System.Threading.Tasks;

namespace PresenceClient.Helpers;

public static class TaskExtensions
{
    public static void FireAndForget(this Task task)
    {
        task.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Console.WriteLine($"An error occurred: {t.Exception}");
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}