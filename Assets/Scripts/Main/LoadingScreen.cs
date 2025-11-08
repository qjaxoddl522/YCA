using System.Collections;
using TMPro;
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
        StartCoroutine(isLinkSearch ? LinkSearch() : KeywordSearch());
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
        yield return StartCoroutine(PythonGetCsv.RunYoutubeCollectorCoroutine(
            text,
            true,
            onProgress: _ => { },
            onError: line => Debug.LogWarning($"[Collector err] {line}")
        ));
        
        yield return StartCoroutine(PythonGetCsv.RunAnalyzeCoroutine(
            inputCsv: System.IO.Path.Combine(PythonGetCsv.youtubeCollectorDir, "youtube_link_results.csv"),
            outputCsv: System.IO.Path.Combine(PythonGetCsv.analyzeDir, "analyzed_comments.csv"),
            sentimentModel: "kcelectra",
            onProgress: _ => { },
            onError: line => Debug.LogWarning($"[Analyze err] {line}")
        ));

        yield return StartCoroutine(LoadSceneAsync("Result - Link"));

        if (CsvDataReader.TryReadLinkPrimaryVideo(out var video))
        {
            var manager = FindFirstObjectByType<LinkContentManager>();
            if (manager != null)
            {
                manager.ApplyPrimaryVideo(video);
            }
        }
        else
        {
            Debug.LogWarning("링크 검색 비디오 정보를 불러오지 못했습니다.");
        }

        OnLoadEnd();
    }

    IEnumerator KeywordSearch()
    {
        Debug.Log($"키워드 검색 시작: {text}");
        yield return StartCoroutine(PythonGetCsv.RunYoutubeCollectorCoroutine(
            text,
            false,
            onProgress: _ => { },
            onOutput: line => Debug.Log($"Collector: {line}"),
            onError: line => Debug.LogWarning($"Collector err: {line}")
        ));
        
        yield return StartCoroutine(PythonGetCsv.RunAnalyzeCoroutine(
            inputCsv: System.IO.Path.Combine(PythonGetCsv.youtubeCollectorDir, "youtube_keyword_results.csv"),
            outputCsv: System.IO.Path.Combine(PythonGetCsv.analyzeDir, "analyzed_keywords.csv"),
            sentimentModel: "kcelectra",
            onProgress: _ => { },
            onOutput: line => Debug.Log($"[Analyze] {line}"),
            onError: line => Debug.LogWarning($"[Analyze err] {line}")
        ));

        yield return StartCoroutine(LoadSceneAsync("Result - Keyword"));

        TMP_Text searchWord = GameObject.Find("SearchWord").GetComponent<TMP_Text>();
        searchWord.text = text;

        OnLoadEnd();
    }

    IEnumerator LoadSceneAsync(string sceneName)
    {
        var op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (!op.isDone)
        {
            if (op.progress >= 0.9f)
            {
                op.allowSceneActivation = true;
            }
            yield return null;
        }
    }
}
