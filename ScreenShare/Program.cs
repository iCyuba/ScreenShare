using SIPSorceryMedia.FFmpeg;

namespace ScreenShare;

internal static class Program
{
    public static async Task Main()
    {
        FFmpegInit.Initialise(libPath: "/opt/homebrew/Cellar/ffmpeg@6/6.1.2_2/lib");

        var monitors = FFmpegMonitorManager.GetMonitorDevices() ?? throw new("No monitors");
        int index = 0;

        if (monitors.Count > 1)
        {
            Console.WriteLine("Select a monitor:");
            for (int i = 0; i < monitors.Count; i++)
                Console.WriteLine($"\t{i + 1}. {monitors[i].Name}");

            index = int.Parse(Console.ReadLine()!) - 1;
        }

        Console.Write("Id: ");
        string id = Console.ReadLine()!;
        App app = new(id, monitors[index]);
        TaskCompletionSource task = new();

        app.OnClosed += (_, _) => task.SetResult();
        await task.Task;
    }
}