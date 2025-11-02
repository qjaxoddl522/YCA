using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class PythonGetCsv
{
    // 빌드 폴더/<앱 exe 옆>/Tools/Analyze.exe 가정
    static string pythonPackedExe =>
        Path.Combine(Path.GetDirectoryName(Application.dataPath)!, "Tools", "Analyze.exe");

    // inputText를 주면 STDIN으로 전달, 안 주면 입력 없이 실행
    public static void RunAndRead(string inputText = null)
    {
        // 1) 출력 경로(플랫폼 안전) + 폴더 보장
        string outCsv = Path.Combine(Application.persistentDataPath, "result.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(outCsv)!);

        // 이전 결과 csv를 지우고 시작
        if (File.Exists(outCsv))
            File.Delete(outCsv);

        // 2) exe 존재 확인
        if (!File.Exists(pythonPackedExe))
        {
            Debug.LogError($"Python exe not found: {pythonPackedExe}");
            return;
        }

        // 3) 프로세스 설정: OUTPUT_CSV 환경변수로 출력 경로 전달
        var psi = new ProcessStartInfo
        {
            FileName = pythonPackedExe,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = (inputText != null), // 입력이 있을 때만
            WorkingDirectory = Application.persistentDataPath,
        };
        psi.Environment["OUTPUT_CSV"] = outCsv;
        psi.Environment["PYTHONIOENCODING"] = "utf-8";

        try
        {
            using var p = Process.Start(psi);
            if (p == null) { Debug.LogError("Failed to start process."); return; }

            if (inputText != null)
            {
                p.StandardInput.Write(inputText);
                p.StandardInput.Close(); // EOF 전달
            }

            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            if (!string.IsNullOrEmpty(stdout)) Debug.Log($"py out:\n{stdout}");
            if (!string.IsNullOrEmpty(stderr)) Debug.LogWarning($"py err:\n{stderr}");

            // 4) 결과 읽기
            if (File.Exists(outCsv))
            {
                string raw = File.ReadAllText(outCsv);
                Debug.Log($"CSV @ {outCsv}\n{raw}");
            }
            else
            {
                Debug.LogError($"CSV not found: {outCsv}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }
}
