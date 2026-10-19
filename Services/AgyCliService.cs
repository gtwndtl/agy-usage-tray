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
        var expectedPath = Path.Combine(localAppData, "agy", "bin", "agy.exe");
        if (File.Exists(expectedPath))
        {
            return expectedPath;
        }

        // 2. Check in PATH environment variable
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            var paths = pathEnv.Split(Path.PathSeparator);
            foreach (var p in paths)
            {
                var candidate = Path.Combine(p.Trim(), "agy.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return "agy.exe";
    }

    public async Task<(AgyUsageRoot? Data, string? ErrorMessage)> FetchUsageAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var agyPath = FindAgyExecutable();
            var startInfo = new ProcessStartInfo
            {
                FileName = agyPath,
                Arguments = "--output-format json -p \"/usage\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            var exitTask = process.WaitForExitAsync(cancellationToken);
            var completed = await Task.WhenAny(exitTask, Task.Delay(30000, cancellationToken));
            if (completed != exitTask)
            {
                try { process.Kill(true); } catch { }
                return (null, "Timed out waiting for agy response (30s).");
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(stdout))
            {
                return (null, string.IsNullOrWhiteSpace(stderr) ? $"agy exited with code {process.ExitCode}" : stderr);
            }

            if (string.IsNullOrWhiteSpace(stdout))
            {
                return (null, "Received empty response from agy.");
            }

            // Find JSON start if there are warning banners
            var jsonStart = stdout.IndexOf('{');
            if (jsonStart < 0)
            {
                return (null, $"Unexpected non-JSON output: {stdout.Trim()}");
            }
            var json = stdout[jsonStart..].Trim();

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
