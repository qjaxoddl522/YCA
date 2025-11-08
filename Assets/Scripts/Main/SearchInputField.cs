using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SearchInputField : MonoBehaviour
{
    [SerializeField] Button searchButton;
    [SerializeField] Canvas loadingCanvas;
    [SerializeField] GameObject loadingPrefab;
    TMP_InputField input;

    void Awake()
    {
        input = GetComponent<TMP_InputField>();
        input.onSubmit.AddListener(OnSubmit);
        searchButton.onClick.AddListener(() => OnSubmit(input.text));
    }

    void OnSubmit(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning("검색어가 비어있습니다.");
            return;
        }

        text = text.Trim();

        // 링크인지 확인
        if (IsYouTubeLink(text))
        {
            OnLinkSearch(text);
        }
        else
        {
            OnKeywordSearch(text);
        }
    }

    /// <summary>
    /// YouTube 링크인지 확인
    /// </summary>
    bool IsYouTubeLink(string text)
    {
        // YouTube URL 패턴: youtube.com/watch?v= 또는 youtu.be/
        string pattern = @"(?:youtube\.com/watch\?v=|youtu\.be/)([a-zA-Z0-9_-]{11})";
        return Regex.IsMatch(text, pattern);
    }

    /// <summary>
    /// 링크 검색 처리
    /// </summary>
    void OnLinkSearch(string link)
    {
        var loading = Instantiate(loadingPrefab, loadingCanvas.transform).GetComponent<LoadingScreen>();
        loading.isLinkSearch = true;
        loading.text = link;
        DontDestroyOnLoad(loadingCanvas);
    }

    /// <summary>
    /// 키워드 검색 처리
    /// </summary>
    void OnKeywordSearch(string keyword)
    {
        var loading = Instantiate(loadingPrefab, loadingCanvas.transform).GetComponent<LoadingScreen>();
        loading.isLinkSearch = false;
        loading.text = keyword;
        DontDestroyOnLoad(loadingCanvas);
    }
}
