using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 현재 미사용
/// </summary>
public class PythonGetCsv : MonoBehaviour
{
    // A안: 시스템 파이썬
    // string python = @"C:\Python312\python.exe";

    // B안: 함께 배포한 PyInstaller exe (빌드 폴더 내 /Tools/make_csv.exe 가정)
    string pythonPackedExe =>
        Path.Combine(Path.GetDirectoryName(Application.dataPath)!, "Tools", "make_csv.exe");

    public void RunAndRead()
    {
        // 합의된 출력 경로 (플랫폼 안전)
        string outCsv = Path.Combine(Application.persistentDataPath, "result.csv");

        // 인자: 출력 csv 절대경로
        var psi = new ProcessStartInfo
        {
            FileName = pythonPackedExe,                // A안이면 FileName=python, Arguments=script + outCsv
            Arguments = $"\"{outCsv}\"",
            WorkingDirectory = Application.persistentDataPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var p = Process.Start(psi);
        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        if (!string.IsNullOrEmpty(stdout)) Debug.Log($"py out: {stdout}");
        if (!string.IsNullOrEmpty(stderr)) Debug.LogError($"py err: {stderr}");

        // 파이썬이 완전히 종료된 뒤에 읽기 (파일 잠금/레이스 방지)
        if (File.Exists(outCsv))
        {
            // 간단히 읽기
            string raw = File.ReadAllText(outCsv);
            Debug.Log($"CSV @ {outCsv}\n{raw}");
            // 필요하면 여기서 CSV 파싱 라이브러리 사용 (e.g., CsvHelper)
        }
        else
        {
            Debug.LogError($"CSV not found: {outCsv}");
        }
    }
}
