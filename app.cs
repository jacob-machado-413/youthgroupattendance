using System.Diagnostics;

var backendDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "backend");
var frontendDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "frontend");

var backend = StartProcess("dotnet", $"run --project \"{backendDir}\" --urls http://localhost:5000");
var frontend = StartProcess("dotnet", $"run --project \"{frontendDir}\"");

Console.WriteLine();
Console.WriteLine("-- Youth Group Attendance --");
Console.WriteLine("  Backend  API: http://localhost:5000");
Console.WriteLine("  Scalar   UI: http://localhost:5000/scalar/v1");
Console.WriteLine("  Frontend   : http://localhost:5091");
Console.WriteLine();
Console.WriteLine("Press Ctrl+C to stop both.");
Console.WriteLine("-----------------------------");

var exitEvent = new ManualResetEvent(false);
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    exitEvent.Set();
};
exitEvent.WaitOne();

KillProcess(backend);
KillProcess(frontend);

Console.WriteLine("Done.");

static Process StartProcess(string fileName, string arguments)
{
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true
        }
    };
    process.Start();
    return process;
}

static void KillProcess(Process process)
{
    try
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(3000);
        }
    }
    catch { }
}
