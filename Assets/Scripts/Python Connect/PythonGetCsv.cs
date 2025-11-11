using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class PythonGetCsv
{
    public static readonly string ToolsDirectory = Path.Combine(Path.GetDirectoryName(Application.dataPath)!, "Tools");

    public static string analyzeDir = Path.Combine(ToolsDirectory, "Analyze");
    static string analyzeExe => Path.Combine(analyzeDir, "Analyze.exe");
    
    public static readonly string youtubeCollectorDir = Path.Combine(ToolsDirectory, "YoutubeCollector");
    static string youtubeCollectorExe => Path.Combine(youtubeCollectorDir, "YoutubeCollector.exe");

    public static IEnumerator RunYoutubeCollectorCoroutine(string searchText, bool isLinkSearch,
        Action<float> onProgress = null, Action<string> onOutput = null, Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(searchText))
        {
            Debug.LogError("searchText가 비어 있습니다.");
            yield break;
        }

        if (!File.Exists(youtubeCollectorExe))
        {
            Debug.LogError($"YoutubeCollector.exe not found: {youtubeCollectorExe}");
            yield break;
        }

        string resultFile = Path.Combine(
            youtubeCollectorDir,
            isLinkSearch ? "youtube_link_results.csv" : "youtube_keyword_results.csv"
        );

        if (File.Exists(resultFile))
        {
            try
            {
                File.Delete(resultFile);
            }
            catch (Exception e)
            {
                Debug.LogError($"파일 삭제 실패: {e.Message}");
                yield break;
            }
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = youtubeCollectorExe,
            Arguments = isLinkSearch
                ? $"--url \"{searchText}\""
                : $"--text \"{searchText}\" --period_type \"month\" --amount 1",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = ToolsDirectory
        };

        string tempPath = Path.Combine(ToolsDirectory, "Temp");
        if (!Directory.Exists(tempPath))
        {
            Directory.CreateDirectory(tempPath);
        }

        startInfo.EnvironmentVariables["TEMP"] = tempPath;
        startInfo.EnvironmentVariables["TMP"] = tempPath;
        startInfo.EnvironmentVariables["_MEIPASS2"] = tempPath;
        startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            Debug.LogError("Failed to start YoutubeCollector process.");
            yield break;
        }

        var stdoutQueue = new ConcurrentQueue<string>();
        var stderrQueue = new ConcurrentQueue<string>();

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                stdoutQueue.Enqueue(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                stderrQueue.Enqueue(e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        while (!process.HasExited)
        {
            DrainQueues(stdoutQueue, onOutput, onProgress);
            DrainQueues(stderrQueue, onError, onProgress);
            yield return null;
        }

        process.WaitForExit();

        DrainQueues(stdoutQueue, onOutput, onProgress);
        DrainQueues(stderrQueue, onError, onProgress);

        if (onProgress != null)
        {
            onProgress(1f);
        }

        if (process.ExitCode != 0)
        {
            Debug.LogError($"YoutubeCollector 실패. ExitCode: {process.ExitCode}");
        }
    }

    static void DrainQueues(ConcurrentQueue<string> queue, Action<string> callback, Action<float> onProgress)
    {
        while (queue.TryDequeue(out var line))
        {
            callback?.Invoke(line);
            if (onProgress != null && TryExtractProgress(line, out var progress))
            {
                onProgress(progress);
            }
        }
    }

    public static IEnumerator RunAnalyzeCoroutine(
        string inputCsv,
        string outputCsv = null,
        bool disableNer = false,
        string sentimentModel = null,
        float? neutralThreshold = null,
        int? minLen = null,
        int? topn = null,
        Action<string> onOutput = null,
        Action<string> onError = null,
        Action<float> onProgress = null)
    {
        yield break;
        if (string.IsNullOrEmpty(inputCsv))
        {
            Debug.LogError("inputCsv 경로가 비어 있습니다.");
            yield break;
        }

        if (!File.Exists(analyzeExe))
        {
            Debug.LogError($"Python exe not found: {analyzeExe}");
            yield break;
        }

        string baseArg = $"--input \"{inputCsv}\"";
        if (!string.IsNullOrEmpty(outputCsv))
        {
            baseArg += $" --output \"{outputCsv}\"";
        }
        if (disableNer)
        {
            baseArg += " --disable_ner";
        }
        if (!string.IsNullOrEmpty(sentimentModel))
        {
            baseArg += $" --sentiment_model \"{sentimentModel}\"";
        }
        if (neutralThreshold.HasValue)
        {
            baseArg += $" --neutral_threshold {neutralThreshold.Value.ToString(CultureInfo.InvariantCulture)}";
        }
        if (minLen.HasValue)
        {
            baseArg += $" --min_len {minLen.Value}";
        }
        if (topn.HasValue)
        {
            baseArg += $" --topn {topn.Value}";
        }

        var psi = new ProcessStartInfo
        {
            FileName = analyzeExe,
            Arguments = baseArg,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = analyzeDir,
        };
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["PYTHONUTF8"] = "1";
        psi.StandardOutputEncoding = Encoding.UTF8;
        psi.StandardErrorEncoding = Encoding.UTF8;

        using var p = Process.Start(psi);
        if (p == null)
        {
            Debug.LogError("Failed to start process.");
            yield break;
        }

        var stdoutQueue = new ConcurrentQueue<string>();
        var stderrQueue = new ConcurrentQueue<string>();

        p.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                stdoutQueue.Enqueue(e.Data);
        };
        p.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                stderrQueue.Enqueue(e.Data);
        };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        bool TryDrainStdOut(ConcurrentQueue<string> queue, Action<string> callback)
        {
            var invoked = false;
            while (queue.TryDequeue(out var line))
            {
                invoked = true;
                callback?.Invoke(line);
                if (callback == onOutput && onProgress != null && TryExtractProgress(line, out var progress))
                {
                    onProgress(progress);
                }
            }

            return invoked;
        }

        bool TryDrainStdErr(ConcurrentQueue<string> queue, Action<string> callback)
        {
            var invoked = false;
            while (queue.TryDequeue(out var line))
            {
                if (LooksLikeProgressLine(line))
                {
                    onOutput?.Invoke(line);
                    if (onProgress != null && TryExtractProgress(line, out var progress))
                    {
                        onProgress(progress);
                    }
                    continue;
                }

                invoked = true;
                callback?.Invoke(line);
            }

            return invoked;
        }

        while (!p.HasExited)
        {
            TryDrainStdOut(stdoutQueue, onOutput);
            TryDrainStdErr(stderrQueue, onError);
            yield return null;
        }

        p.WaitForExit();

        TryDrainStdOut(stdoutQueue, onOutput);
        TryDrainStdErr(stderrQueue, onError);

        if (onProgress != null)
        {
            onProgress(1f);
        }

        Debug.Log($"[Analyze] 종료 코드: {p.ExitCode}");
    }

    static bool TryExtractProgress(string line, out float progress)
    {
        progress = 0f;
        if (string.IsNullOrEmpty(line))
            return false;

        int percentIndex = line.IndexOf('%');
        if (percentIndex <= 0)
            return false;

        int start = percentIndex - 1;
        while (start >= 0 && (char.IsDigit(line[start]) || line[start] == '.' || line[start] == ','))
        {
            start--;
        }
        start++;
        var numberStr = line.Substring(start, percentIndex - start).Replace(',', '.');
        if (float.TryParse(numberStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            progress = Mathf.Clamp01(value / 100f);
            return true;
        }

        return false;
    }

    static bool LooksLikeProgressLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return false;

        return line.Contains("%|") && line.Contains("|");
    }
}
