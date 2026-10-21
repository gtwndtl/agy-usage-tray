using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AgyUsageTray.Models;

namespace AgyUsageTray.Services;

public class AgyCliService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string FindAgyExecutable()
    {
        // 1. Try local appdata
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var searchDirs = new List<string>
        {
            Path.Combine(localAppData, "agy", "bin"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini", "bin")
        };

        // 2. Check in PATH environment variable
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            searchDirs.AddRange(pathEnv.Split(Path.PathSeparator).Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)));
        }

        string[] extensions = { "agy.exe", "agy.cmd", "agy.bat" };
        foreach (var dir in searchDirs)
        {
            foreach (var ext in extensions)
            {
                var candidate = Path.Combine(dir, ext);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return "agy.exe";
    }

    private static async Task<(string stdout, string stderr, int exitCode)> RunProcessAsync(
        string fileName,
        string args,
        CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        var exitTask = process.WaitForExitAsync(ct);
        var completed = await Task.WhenAny(exitTask, Task.Delay(30000, ct));
        if (completed != exitTask)
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException("Timed out waiting for agy response (30s).");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return (stdout, stderr, process.ExitCode);
    }

    public async Task<(AgyUsageRoot? Data, string? ErrorMessage)> FetchUsageAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var agyPath = FindAgyExecutable();
            bool isScript = agyPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                            agyPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);

            var targetFile = isScript ? "cmd.exe" : agyPath;
            var targetArgs = isScript
                ? $"/c \"\"{agyPath}\" --output-format json -p \"/usage\"\""
                : "--output-format json -p \"/usage\"";

            var (stdout, stderr, exitCode) = await RunProcessAsync(targetFile, targetArgs, cancellationToken);

            if (exitCode != 0 && string.IsNullOrWhiteSpace(stdout))
            {
                return (null, string.IsNullOrWhiteSpace(stderr) ? $"agy exited with code {exitCode}" : stderr);
            }

            if (string.IsNullOrWhiteSpace(stdout))
            {
                return (null, "Received empty response from agy.");
            }

            // Strip ANSI escape sequences (e.g. 0x1B codes emitted by terminal)
            var cleanStdout = System.Text.RegularExpressions.Regex.Replace(stdout, @"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])", "");

            // Extract exact JSON object between first '{' and last '}'
            var jsonStart = cleanStdout.IndexOf('{');
            var jsonEnd = cleanStdout.LastIndexOf('}');
            if (jsonStart < 0 || jsonEnd <= jsonStart)
            {
                return (null, $"Unexpected non-JSON output: {cleanStdout.Trim()}");
            }
            var json = cleanStdout.Substring(jsonStart, jsonEnd - jsonStart + 1).Trim();

            var result = JsonSerializer.Deserialize<AgyUsageRoot>(json, JsonOptions);
            if (result == null || result.Command?.Data == null)
            {
                return (null, "Failed to parse usage data from agy response.");
            }

            return (result, null);
        }
        catch (OperationCanceledException)
        {
            return (null, "Request was cancelled.");
        }
        catch (Exception ex)
        {
            return (null, $"Error: {ex.Message}");
        }
    }
}
