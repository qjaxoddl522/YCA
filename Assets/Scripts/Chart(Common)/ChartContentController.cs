using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 차트 오브젝트를 전환하고 매니저를 제공하는 컨트롤러
/// </summary>
public class ChartContentController : MonoBehaviour
{
    [Header("Content Objects")]
    [SerializeField] private GameObject overallKeywordContentObject;
    [SerializeField] private GameObject videoSelectedContent;

    [Header("썸네일, 제목")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text analyzeStatusText;
    [SerializeField] private Slider analyzeProgressSlider;
    [SerializeField] private GameObject analyzeLoadingIndicator;

    Coroutine analyzeRoutine;

    private void Start()
    {
        ShowOverallKeywordContent();
    }

    void ShowOverallKeywordContent()
    {
        if (overallKeywordContentObject != null)
            overallKeywordContentObject.SetActive(true);
        
        if (videoSelectedContent != null)
            videoSelectedContent.SetActive(false);

        var pie = overallKeywordContentObject?.GetComponent<PieChartManager>();
        if (pie == null)
            return;

        pie.ClearAllCategories();

        var keywordTotals = CsvDataReader.ReadKeywordSummaryCsv();
        if (keywordTotals == null || keywordTotals.Count == 0)
        {
            Debug.LogWarning("표시할 키워드 데이터가 없습니다.");
            return;
        }

        foreach (var pair in BuildKeywordDisplayList(keywordTotals))
        {
            pie.AddCategory(pair.Key, pair.Value);
        }
    }
    
    public void ShowVideoSelectedContent(string title, string videoLink)
    {
        if (analyzeRoutine != null)
        {
            StopCoroutine(analyzeRoutine);
            analyzeRoutine = null;
        }

        analyzeRoutine = StartCoroutine(ShowVideoSelectedContentRoutine(title, videoLink));
    }

    IEnumerator ShowVideoSelectedContentRoutine(string title, string videoLink)
    {
        if (overallKeywordContentObject != null)
            overallKeywordContentObject.SetActive(false);
        
        if (videoSelectedContent != null)
            videoSelectedContent.SetActive(true);
        
        titleText.text = title;
        SetAnalyzeUi(true, 0f, "댓글 수집 준비 중...");

        if (string.IsNullOrWhiteSpace(videoLink))
        {
            Debug.LogError("비디오 링크가 없습니다.");
            SetAnalyzeUi(false, 0f, "비디오 링크가 없습니다.");
            analyzeRoutine = null;
            yield break;
        }

        string inputCsv = Path.Combine(PythonGetCsv.youtubeCollectorDir, "youtube_link_results.csv");
        string outputCsv = Path.Combine(PythonGetCsv.analyzeDir, "analyzed_comments.csv");

        TryDeleteFile(inputCsv);
        //TryDeleteFile(outputCsv);

        yield return StartCoroutine(PythonGetCsv.RunYoutubeCollectorCoroutine(
            videoLink,
            true,
            onOutput: line =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                    SetAnalyzeStatus(line);
            },
            onError: line =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                    SetAnalyzeStatus($"수집 오류: {line}");
            },
            onProgress: progress => SetAnalyzeProgress(Mathf.Clamp01(progress) * 0.4f)
        ));

        if (!File.Exists(inputCsv))
        {
            string message = $"입력 CSV를 찾을 수 없습니다: {inputCsv}";
            Debug.LogError(message);
            SetAnalyzeUi(false, 0f, "CSV 파일이 없습니다.");
            analyzeRoutine = null;
            yield break;
        }

        SetAnalyzeStatus("댓글 분석 중...");

        yield return StartCoroutine(PythonGetCsv.RunAnalyzeCoroutine(
            inputCsv,
            outputCsv,
            onOutput: line =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    SetAnalyzeStatus(line);
                }
            },
            onError: line =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    SetAnalyzeStatus($"오류: {line}");
                }
            },
            onProgress: progress =>
            {
                float adjusted = 0.4f + Mathf.Clamp01(progress) * 0.6f;
                SetAnalyzeProgress(adjusted);
            }
        ));

        if (!File.Exists(outputCsv))
        {
            Debug.LogError($"분석 결과 CSV를 찾을 수 없습니다: {outputCsv}");
            SetAnalyzeUi(false, 0f, "분석 결과가 생성되지 않았습니다.");
            analyzeRoutine = null;
            yield break;
        }

        SetAnalyzeStatus("분석 결과 불러오는 중...");

        var result = CsvDataReader.ReadLinkResultCsv();

        var pie = videoSelectedContent?.GetComponent<PieChartManager>();
        if (pie != null)
        {
            pie.ClearAllCategories();

            foreach (var pair in BuildKeywordDisplayList(result.keywordNumbers))
            {
                pie.AddCategory(pair.Key, pair.Value);
            }
        }
        else
        {
            Debug.LogError("PieChartManager를 찾을 수 없습니다!");
        }

        var bar = videoSelectedContent?.GetComponent<StanseBarChartManager>();
        if (bar != null)
        {
            bar.SetCategoryValue("긍정", result.stanceNumbers[Stance.Positive]);
            bar.SetCategoryValue("부정", result.stanceNumbers[Stance.Negative]);
            bar.SetCategoryValue("중립", result.stanceNumbers[Stance.Neutral]);
        }
        else
        {
            Debug.LogError("PositiveBarChartManager를 찾을 수 없습니다!");
        }

        SetAnalyzeUi(false, 1f, "분석 완료");
        analyzeRoutine = null;
    }

    static IEnumerable<KeyValuePair<string, int>> BuildKeywordDisplayList(Dictionary<string, int> source)
    {
        if (source == null || source.Count == 0)
            yield break;

        const int maxCategoryCount = 6;

        var ordered = source
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .ToList();

        int takeCount = Mathf.Min(maxCategoryCount, ordered.Count);

        for (int i = 0; i < takeCount; i++)
        {
            yield return ordered[i];
        }

        if (ordered.Count > maxCategoryCount)
        {
            int othersSum = ordered.Skip(maxCategoryCount).Sum(pair => pair.Value);
            if (othersSum > 0)
            {
                yield return new KeyValuePair<string, int>("기타", othersSum);
            }
        }
    }

    void SetAnalyzeUi(bool isLoading, float progress, string status)
    {
        if (analyzeLoadingIndicator != null)
            analyzeLoadingIndicator.SetActive(isLoading);

        SetAnalyzeProgress(isLoading ? progress : 1f);
        SetAnalyzeStatus(status);
    }

    void SetAnalyzeProgress(float progress)
    {
        if (analyzeProgressSlider != null)
        {
            analyzeProgressSlider.gameObject.SetActive(true);
            analyzeProgressSlider.normalizedValue = Mathf.Clamp01(progress);
            if (progress >= 1f)
            {
                analyzeProgressSlider.gameObject.SetActive(false);
            }
        }
    }

    void SetAnalyzeStatus(string message)
    {
        if (analyzeStatusText != null)
        {
            analyzeStatusText.text = message;
        }
    }

    static void TryDeleteFile(string path)
    {
        if (!File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"파일 삭제 실패: {path}\n{e.Message}");
        }
    }
}

