using ChartAndGraph;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class LinkContentManager : MonoBehaviour
{
    [SerializeField] Image thumbnail;
    [SerializeField] TMP_Text title;

    [SerializeField] GameObject timeChartPrefab;
    [SerializeField] Transform timeChartGroup;

    [SerializeField] GameObject overviewPrefab;
    [SerializeField] Transform overviewGroup;

    // 생성된 차트 리스트
    List<GameObject> instantiatedCharts = new List<GameObject>();
    List<PieChartManager> timePieCharts = new List<PieChartManager>();
    List<StanseBarChartManager> timeBarCharts = new List<StanseBarChartManager>();

    [Header("Overview 설정")]
    [SerializeField, Min(1)] int segmentKeywordCount = 3;
    [SerializeField] Color headerColor = new Color(0.0f, 0.37f, 0.53f);
    [SerializeField] Color rowColorEven = new Color(0.82f, 0.86f, 0.91f);
    [SerializeField] Color rowColorOdd = new Color(0.88f, 0.9f, 0.93f);
    [SerializeField] string emptyCellPlaceholder = "";

    class ChartSegmentSummary
    {
        public string periodLabel;
        public List<string> topKeywords;
    }

    readonly List<ChartSegmentSummary> chartSummaries = new List<ChartSegmentSummary>();
    LinkResult cachedLinkResult;

    private void Start()
    {
        // 정보 세팅
        LoadLinkData();
    }

    public void LoadLinkData()
    {
        ClearGeneratedCharts();

        // CSV에서 LinkResult 읽기
        cachedLinkResult = CsvDataReader.ReadLinkResultCsv();

        if (cachedLinkResult == null || cachedLinkResult.dataEntries == null)
        {
            Debug.LogWarning("LinkResult가 null이거나 dataEntries가 없습니다.");
            return;
        }

        int dataCount = cachedLinkResult.dataEntries.Count;
        // 차트 개수 계산: 최소 2개, 100개마다 +1개, 최대 5개
        int chartCount = Mathf.Clamp(2 + dataCount / 100, 2, 5);

        Debug.Log($"총 댓글 수: {dataCount}, 생성할 차트 수: {chartCount}");

        // 차트 생성
        for (int i = 0; i < chartCount; i++)
        {
            Transform parent = timeChartGroup != null ? timeChartGroup : transform;
            GameObject chart = Instantiate(timeChartPrefab, parent);
            instantiatedCharts.Add(chart);

            PieChartManager pieChart = chart.GetComponent<PieChartManager>();
            if (pieChart != null)
            {
                timePieCharts.Add(pieChart);
                pieChart.ClearAllCategories();
            }

            StanseBarChartManager barChart = chart.GetComponent<StanseBarChartManager>();
            if (barChart != null)
            {
                timeBarCharts.Add(barChart);
            }
        }

        Debug.Log($"{chartCount}개의 차트가 생성되었습니다.");

        // 데이터를 차트에 분배
        DistributeDataToCharts(cachedLinkResult.dataEntries, chartCount);

        PopulateOverview();
    }

    public void ApplyPrimaryVideo(YoutubeVideoData videoData)
    {
        if (videoData == null)
            return;

        if (title != null)
        {
            title.text = videoData.title ?? string.Empty;
        }

        if (thumbnail != null && !string.IsNullOrEmpty(videoData.thumbnailUrl))
        {
            StartCoroutine(LoadThumbnailRoutine(thumbnail, videoData.thumbnailUrl));
        }
    }

    IEnumerator LoadThumbnailRoutine(Image target, string url)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f)
                );
                target.sprite = sprite;
            }
            else
            {
                Debug.LogWarning($"링크 썸네일 로드 실패: {url}\n{request.error}");
            }
        }
    }

    /// <summary>
    /// 댓글 데이터를 차트 개수로 나눠서 각 차트에 분배
    /// </summary>
    private void DistributeDataToCharts(List<LinkDataEntry> dataEntries, int chartCount)
    {
        if (dataEntries == null || dataEntries.Count == 0 || chartCount == 0)
            return;

        chartSummaries.Clear();

        int dataCount = dataEntries.Count;
        int entriesPerChart = Mathf.CeilToInt((float)dataCount / chartCount);

        Debug.Log($"차트당 할당될 데이터 수: 약 {entriesPerChart}개");

        for (int chartIndex = 0; chartIndex < chartCount; chartIndex++)
        {
            // 이 차트에 할당될 데이터 범위 계산
            int startIndex = chartIndex * entriesPerChart;
            int endIndex = Mathf.Min(startIndex + entriesPerChart, dataCount);

            if (startIndex >= dataCount)
                break;

            // 키워드별 개수와 긍부정별 개수를 집계
            Dictionary<string, int> keywordCounts = new Dictionary<string, int>();
            Dictionary<Stance, int> stanceCounts = new Dictionary<Stance, int>
            {
                { Stance.Positive, 0 },
                { Stance.Negative, 0 },
                { Stance.Neutral, 0 }
            };

            // 할당된 범위의 데이터를 집계
            DateTime oldestDate = DateTime.MaxValue;
            DateTime newestDate = DateTime.MinValue;
            for (int i = startIndex; i < endIndex; i++)
            {
                LinkDataEntry entry = dataEntries[i];

                // 가장 오래된 날짜와 최신 날짜 추적
                if (entry.date != DateTime.MinValue)
                {
                    if (entry.date < oldestDate)
                        oldestDate = entry.date;
                    if (entry.date > newestDate)
                        newestDate = entry.date;
                }

                // 키워드 카운트
                foreach (string keyword in entry.keywords)
                {
                    if (keywordCounts.ContainsKey(keyword))
                        keywordCounts[keyword]++;
                    else
                        keywordCounts[keyword] = 1;
                }

                // 긍부정 카운트
                stanceCounts[entry.stance]++;
            }

            // 기간 텍스트 생성 (범위로 표시)
            string periodText = GetPeriodRangeText(oldestDate, newestDate);

            // 키워드를 개수 기준으로 내림차순 정렬 (동일 개수는 이름순)
            var sortedKeywords = keywordCounts
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key, StringComparer.Ordinal)
                .ToList();

            chartSummaries.Add(new ChartSegmentSummary
            {
                periodLabel = periodText,
                topKeywords = sortedKeywords
                    .Select(kvp => kvp.Key)
                    .Take(segmentKeywordCount)
                    .ToList()
            });

            // PieChart에 키워드 데이터 추가 (개수 많은 순서대로, 최대 5개 + 기타)
            if (chartIndex < timePieCharts.Count && timePieCharts[chartIndex] != null)
            {
                PieChartManager pieChart = timePieCharts[chartIndex];

                int maxCategories = 5; // 개별 카테고리는 최대 5개 (6번째는 '기타')
                int etcCount = 0; // '기타' 항목의 합계
                
                // 6번째 이후 항목들을 '기타'로 합산
                for (int i = maxCategories; i < sortedKeywords.Count; i++)
                {
                    etcCount += sortedKeywords[i].Value;
                }
                
                // '기타'를 먼저 추가 (11시 방향에 위치, UI는 맨 아래)
                if (etcCount > 0)
                {
                    pieChart.AddCategory("기타", etcCount, addUIToTop: false);
                }
                
                // 상위 5개를 역순으로 추가 (5위 -> 1위 순서로, 1위가 1시 방향에 위치, UI는 맨 위에 추가하여 정순 정렬)
                int topCount = Mathf.Min(maxCategories, sortedKeywords.Count);
                for (int i = topCount - 1; i >= 0; i--)
                {
                    pieChart.AddCategory(sortedKeywords[i].Key, sortedKeywords[i].Value, addUIToTop: true);
                }
                
                // 기간 텍스트 업데이트
                pieChart.UpdatePeriod(periodText);
                
                if (etcCount > 0)
                {
                    Debug.Log($"차트 {chartIndex + 1}: 상위 {topCount}개 키워드 + 기타 ({sortedKeywords.Count - maxCategories}개 통합, 총 {etcCount}개)");
                }
                else
                {
                    Debug.Log($"차트 {chartIndex + 1}: {sortedKeywords.Count}개의 키워드 카테고리 추가");
                }
            }

            // BarChart에 긍부정 데이터 추가
            if (chartIndex < timeBarCharts.Count && timeBarCharts[chartIndex] != null)
            {
                StanseBarChartManager barChart = timeBarCharts[chartIndex];
                barChart.SetCategoryValue("긍정", stanceCounts[Stance.Positive]);
                barChart.SetCategoryValue("부정", stanceCounts[Stance.Negative]);
                barChart.SetCategoryValue("중립", stanceCounts[Stance.Neutral]);

                Debug.Log($"차트 {chartIndex + 1}: 긍정 {stanceCounts[Stance.Positive]}, 부정 {stanceCounts[Stance.Negative]}, 중립 {stanceCounts[Stance.Neutral]}");
            }
        }
    }

    /// <summary>
    /// 날짜 범위를 표시하는 텍스트 생성 ("최근 1주 이내" 또는 "1주 전 ~ 2주 전" 형식)
    /// </summary>
    private string GetPeriodRangeText(DateTime oldestDate, DateTime newestDate)
    {
        if (oldestDate == DateTime.MaxValue || newestDate == DateTime.MinValue)
        {
            return "기간 정보 없음";
        }

        DateTime now = DateTime.Now;
        TimeSpan oldestDiff = now - oldestDate;
        TimeSpan newestDiff = now - newestDate;

        int oldestDays = (int)oldestDiff.TotalDays;
        int newestDays = (int)newestDiff.TotalDays;

        // 최신 댓글이 오늘이거나 최근인 경우
        if (true)//(newestDays <= 1)
        {
            return "최근 " + GetSinglePeriodText(oldestDays) + " 이내";
        }
        
        // 범위 표시
        string oldestText = GetSinglePeriodText(oldestDays);
        string newestText = GetSinglePeriodText(newestDays);
        
        // 같은 범주면 하나만 표시
        if (oldestText == newestText)
        {
            return oldestText + " (약)";
        }
        
        return $"{newestText} ~ {oldestText}";
    }

    /// <summary>
    /// 일수를 "N개월 전", "N주 전" 등의 텍스트로 변환
    /// </summary>
    private string GetSinglePeriodText(int totalDays)
    {
        // 1년 이상
        if (totalDays >= 365)
        {
            int years = totalDays / 365;
            return $"{years}년";
        }
        // 1개월 이상
        else if (totalDays >= 30)
        {
            int months = totalDays / 30;
            return $"{months}개월";
        }
        // 1주 이상
        else if (totalDays >= 7)
        {
            int weeks = totalDays / 7;
            return $"{weeks}주";
        }
        // 1일 이상
        else if (totalDays >= 1)
        {
            return $"{totalDays}일";
        }
        // 오늘
        else
        {
            return "오늘";
        }
    }

    void PopulateOverview()
    {
        if (overviewGroup == null || overviewPrefab == null || cachedLinkResult == null)
            return;

        for (int i = overviewGroup.childCount - 1; i >= 0; i--)
        {
            var child = overviewGroup.GetChild(i);
            if (child == null)
                continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        int columnCount = chartSummaries.Count + 1; // 순위 + 각 기간
        if (columnCount <= 1)
            return;

        // 헤더 행 생성
        CreateOverviewCell("순위", headerColor, Color.black);
        foreach (var summary in chartSummaries)
        {
            CreateOverviewCell(summary.periodLabel, headerColor, Color.black);
        }

        // 데이터 행 생성
        int rowCount = segmentKeywordCount;
        for (int row = 0; row < rowCount; row++)
        {
            Color rowColor = (row % 2 == 0) ? rowColorEven : rowColorOdd;

            CreateOverviewCell((row + 1).ToString(), rowColor, Color.black);

            foreach (var summary in chartSummaries)
            {
                string label = row < summary.topKeywords.Count
                    ? summary.topKeywords[row]
                    : emptyCellPlaceholder;
                CreateOverviewCell(label, rowColor, Color.black);
            }
        }

        var grid = overviewGroup.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            int totalRows = Mathf.Max(1, segmentKeywordCount + 1); // 헤더 + 데이터 행
            grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = totalRows;

            int totalCells = overviewGroup.childCount;
            int calculatedColumns = Mathf.Max(1, Mathf.CeilToInt(totalCells / (float)totalRows));

            float cellWidth = grid.cellSize.x;
            float spacingX = grid.spacing.x;
            float paddingX = grid.padding.left + grid.padding.right;
            float targetWidth = calculatedColumns * cellWidth + Mathf.Max(0, calculatedColumns - 1) * spacingX + paddingX;

            float cellHeight = grid.cellSize.y;
            float spacingY = grid.spacing.y;
            float paddingY = grid.padding.top + grid.padding.bottom;
            float targetHeight = totalRows * cellHeight + Mathf.Max(0, totalRows - 1) * spacingY + paddingY;

            if (overviewGroup is RectTransform groupRect)
            {
                groupRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
                groupRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
            }
        }
    }

    void CreateOverviewCell(string text, Color background, Color labelColor)
    {
        GameObject instance = Instantiate(overviewPrefab, overviewGroup);
        var cell = instance.GetComponent<OverviewCell>();
        if (cell == null)
            return;

        cell.UpdateColor(background);
        cell.UpdateLabel(text);
        cell.UpdateLabelColor(labelColor);
    }

    void ClearGeneratedCharts()
    {
        if (instantiatedCharts.Count > 0)
        {
            for (int i = instantiatedCharts.Count - 1; i >= 0; i--)
            {
                var chart = instantiatedCharts[i];
                if (chart == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(chart);
                else
                    DestroyImmediate(chart);
            }
        }

        instantiatedCharts.Clear();
        timePieCharts.Clear();
        timeBarCharts.Clear();
    }
}
