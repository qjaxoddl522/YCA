#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;
/// <summary>
/// - 외부 파이썬 자동 탐색 + 실행기
/// - pip 준비: pip이 없으면 설치하고, 있으면 OK
/// - Unity 내부 Python이 아닌 PC의 외부 Python을 자동으로 탐색
/// - .py 파일을 실행하고, Unity에서 지정한 CSV 경로로 결과 저장
/// - CSV 경로는 환경변수 OUTPUT_CSV로 전달
/// </summary>
public static class ExternalPythonAutoFinder
{
    // 파이썬 후보 경로들
    private static readonly string[] CommonPythonPaths =
    {
        @"C:\Users\" + "%USERNAME%" + @"\AppData\Local\Programs\Python",
        @"C:\Python311",
        @"C:\Python312",
        @"C:\Program Files\Python311",
        @"C:\Program Files\Python312",
        @"C:\Program Files (x86)\Python311",
        @"C:\Program Files (x86)\Python312",
    };

    //설치해두고 싶은 패키지
    private static readonly string[] RequiredPackages =
    {
        "google-api-python-client",
        "pandas",
        "tqdm"
    };

    [MenuItem("Tools/Python (external)/Auto Run Python")]
    public static void RunExternalPython()
    {
        // 1) python 찾기
        string pythonExe = FindPythonPath();
        if (string.IsNullOrEmpty(pythonExe) || !File.Exists(pythonExe))
        {
            EditorUtility.DisplayDialog("Python not found",
                "Python 실행 파일을 찾을 수 없습니다.\npython.org에서 설치 후 다시 시도하세요.",
                "OK");
            return;
        }

        Debug.Log($"[ExtPyAuto] 사용할 파이썬: {pythonExe}");

        // 2) pip 준비
        if (!EnsurePip(pythonExe))
        {
            EditorUtility.DisplayDialog("pip 오류",
                "pip을 자동으로 설치/활성화하지 못했습니다.\n관리자 CMD에서 수동으로 설치해 주세요.",
                "OK");
            return;
        }

        // 3) 필수 패키지 설치
        foreach (var pkg in RequiredPackages)
        {
            if (!IsPackageInstalled(pythonExe, pkg))
            {
                Debug.Log($"[ExtPyAuto] '{pkg}' 설치 시도...");
                bool ok = RunProcess(pythonExe, $"-m pip install {pkg}");
                if (!ok)
                {
                    Debug.LogWarning($"[ExtPyAuto] '{pkg}' 설치 실패(무시하고 진행).");
                }
            }
            else
            {
                Debug.Log($"[ExtPyAuto] '{pkg}' 이미 설치됨.");
            }
        }

        // 4) 실제 실행할 .py 지정
        string projectRoot = Application.dataPath.Replace("/Assets", "");
        string pyPath = Path.Combine(projectRoot, "Assets", "Python", "make_csv.py"); //사용할 파일로 변경

        if (!File.Exists(pyPath))
        {
            EditorUtility.DisplayDialog("실행 실패", $".py 파일을 찾을 수 없습니다:\n{pyPath}", "확인");
            return;
        }

        // 5) CSV 저장 위치 선택
        string defaultDir = Path.Combine(projectRoot, "Assets", "Python");
        Directory.CreateDirectory(defaultDir);
        string csvPath = EditorUtility.SaveFilePanel("CSV 저장 위치 선택", defaultDir, "report.csv", "csv");
        if (string.IsNullOrEmpty(csvPath))
            return;

        // 6) .py 실행 (CSV 경로 환경변수로 전달)
        bool runOk = RunProcessWithEnv(pythonExe, $"\"{pyPath}\"", ("OUTPUT_CSV", csvPath));
        if (!runOk)
        {
            Debug.LogError("[ExtPyAuto] 외부 파이썬 실행 실패");
            return;
        }

        // 7) Assets 안에 있으면 Import
        string norm = csvPath.Replace("\\", "/");
        if (norm.Contains("/Assets/") && File.Exists(csvPath))
        {
            string rel = norm.Substring(norm.IndexOf("Assets/"));
            AssetDatabase.ImportAsset(rel);
            Debug.Log($"[ExtPyAuto] CSV imported: {rel}");
        }

        Debug.Log("[ExtPyAuto]  전체 작업 완료");
    }

    // ---------------------------------------------------
    // pip 준비: pip이 없으면 ensurepip으로 만들고, 있으면 OK
    private static bool EnsurePip(string pythonExe)
    {
        // pip 있는지 확인
        if (RunProcess(pythonExe, "-m pip --version"))
            return true;

        Debug.Log("[ExtPyAuto] pip 없음 → ensurepip 실행");
        RunProcess(pythonExe, "-m ensurepip --default-pip");

        // 다시 확인
        if (RunProcess(pythonExe, "-m pip --version"))
        {
            // 최신 pip로 올려두는 게 안전
            RunProcess(pythonExe, "-m pip install --upgrade pip");
            return true;
        }

        return false;
    }

    // 패키지 설치 여부 확인
    private static bool IsPackageInstalled(string pythonExe, string pkg)
    {
        return RunProcess(pythonExe, $"-m pip show {pkg}");
    }

    // ---------------------------------------------------
    // python.exe 자동 탐색
    private static string FindPythonPath()
    {
        // 1) PATH에서 찾기
        try
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var p in pathEnv.Split(';'))
                {
                    string full = Path.Combine(p, "python.exe");
                    if (File.Exists(full))
                    {
                        Debug.Log($"[ExtPyAuto] PATH에서 발견: {full}");
                        return full;
                    }
                }
            }
        }
        catch { }

        // 2) 대표 설치 위치에서 찾기
        foreach (string root in CommonPythonPaths)
        {
            try
            {
                string expanded = Environment.ExpandEnvironmentVariables(root);
                if (!Directory.Exists(expanded)) continue;

                var found = Directory.GetFiles(expanded, "python.exe", SearchOption.AllDirectories)
                                     .OrderByDescending(f => File.GetLastWriteTime(f))
                                     .FirstOrDefault();
                if (!string.IsNullOrEmpty(found))
                {
                    Debug.Log($"[ExtPyAuto] 일반 설치 경로에서 발견: {found}");
                    return found;
                }
            }
            catch { }
        }

        // 3) where python
        try
        {
            var psi = new ProcessStartInfo("where", "python")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                string first = output.Split('\n').FirstOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(first) && File.Exists(first))
                {
                    Debug.Log($"[ExtPyAuto] where python 으로 발견: {first}");
                    return first;
                }
            }
        }
        catch { }

        return null;
    }

    // ---------------------------------------------------
    private static bool RunProcess(string exe, string args)
    {
        var psi = new ProcessStartInfo();
        psi.FileName = exe;
        psi.Arguments = args;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        try
        {
            using (var p = Process.Start(psi))
            {
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (!string.IsNullOrEmpty(stdout))
                    Debug.Log("[ExtPyAuto][stdout]\n" + stdout);
                if (!string.IsNullOrEmpty(stderr))
                    Debug.LogWarning("[ExtPyAuto][stderr]\n" + stderr);

                return p.ExitCode == 0;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[ExtPyAuto] 프로세스 실행 예외: " + e);
            return false;
        }
    }

    private static bool RunProcessWithEnv(string exe, string args, params (string key, string value)[] extraEnv)
    {
        var psi = new ProcessStartInfo();
        psi.FileName = exe;
        psi.Arguments = args;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        if (extraEnv != null)
        {
            foreach (var pair in extraEnv)
            {
                if (!string.IsNullOrEmpty(pair.key) && pair.value != null)
                    psi.EnvironmentVariables[pair.key] = pair.value;
            }
        }

        try
        {
            using (var p = Process.Start(psi))
            {
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (!string.IsNullOrEmpty(stdout))
                    Debug.Log("[ExtPyAuto][stdout]\n" + stdout);
                if (!string.IsNullOrEmpty(stderr))
                    Debug.LogWarning("[ExtPyAuto][stderr]\n" + stderr);

                return p.ExitCode == 0;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[ExtPyAuto] 실행 예외: " + e);
            return false;
        }
    }
}
#endif
