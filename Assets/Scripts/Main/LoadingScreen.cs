using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour
{
    public string text = "";
    public bool isLinkSearch = false;

    Animator animator;
    
    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.Space))
        {
            OnLoadEnd();
        }
    }

    public void OnRevealEnd()
    {
        if (isLinkSearch)
        {
            StartCoroutine(LinkSearch());
        }
        else
        {
            StartCoroutine(KeywordSearch());
        }
    }

    public void OnLoadEnd()
    {
        animator.SetTrigger("Hide");
    }

    public void OnHideEnd()
    {
        Destroy(GetComponentInParent<Canvas>(true).gameObject);
    }

    IEnumerator LinkSearch()
    {
        // YoutubeCollector.exe 실행 (링크 검색)
        yield return StartCoroutine(RunYoutubeCollector(text));
        
        // 씬 로딩
        var op = SceneManager.LoadSceneAsync("Result - Link");
        op.allowSceneActivation = false;
        
        // 로딩 완료 대기
        while (!op.isDone)
        {
            if (op.progress >= 0.9f)
            {
                op.allowSceneActivation = true;
            }
            yield return null;
        }
    }

    IEnumerator KeywordSearch()
    {
        // YoutubeCollector.exe 실행 (키워드 검색)
        yield return StartCoroutine(RunYoutubeCollector(text));
        
        // 씬 로딩
        var op = SceneManager.LoadSceneAsync("Result - Keyword");
        op.allowSceneActivation = false;
        
        // 로딩 완료 대기
        while (!op.isDone)
        {
            if (op.progress >= 0.9f)
            {
                op.allowSceneActivation = true;
            }
            yield return null;
        }
    }

    IEnumerator RunYoutubeCollector(string searchText)
    {
        // YoutubeCollector.exe 경로 설정
        string exePath = System.IO.Path.Combine(Application.persistentDataPath, "YoutubeCollector.exe");

        string resultFile = System.IO.Path.Combine(
            Application.persistentDataPath,
            isLinkSearch ? "youtube_link_results.csv" : "youtube_keyword_results.csv"
        );

        if (System.IO.File.Exists(resultFile))
        {
            try
            {
                System.IO.File.Delete(resultFile);
                UnityEngine.Debug.Log($"기존 파일 삭제: {resultFile}");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"파일 삭제 실패: {e.Message}");
                yield break;
            }
        }

        // 실행 파일이 존재하는지 확인
        if (!System.IO.File.Exists(exePath))
        {
            UnityEngine.Debug.LogError($"YoutubeCollector.exe를 찾을 수 없습니다: {exePath}");
            yield break;
        }

        // 프로세스 시작 정보 설정
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = isLinkSearch
            ? $"--url \"{searchText}\""
            : $"--text \"{searchText}\" --period_type \"month\" --amount 1",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Application.persistentDataPath
        };

        string tempPath = System.IO.Path.Combine(Application.persistentDataPath, "Temp");
        if (!System.IO.Directory.Exists(tempPath))
        {
            System.IO.Directory.CreateDirectory(tempPath);
        }

        startInfo.EnvironmentVariables["TEMP"] = tempPath;
        startInfo.EnvironmentVariables["TMP"] = tempPath;
        startInfo.EnvironmentVariables["_MEIPASS2"] = tempPath;

        // 프로세스 실행
        Process process = new Process { StartInfo = startInfo };
        
        try
        {
            process.Start();

            // 프로세스가 종료될 때까지 대기
            while (!process.HasExited)
            {
                yield return new WaitForSeconds(0.5f);
            }

            // 종료 코드 확인
            int exitCode = process.ExitCode;
            UnityEngine.Debug.Log($"YoutubeCollector 완료. 종료 코드: {exitCode}");

            if (exitCode != 0)
            {
                string error = process.StandardError.ReadToEnd();
                UnityEngine.Debug.LogError($"YoutubeCollector 실행 중 오류 발생: {error}");
            }
            else
            {
                string output = process.StandardOutput.ReadToEnd();
                UnityEngine.Debug.Log($"YoutubeCollector 출력: {output}");
            }
        }
        finally
        {
            process.Close();
        }
    }
}
