using ChartAndGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LinkContentManager : MonoBehaviour
{
    [SerializeField] Image thumbnail;
    [SerializeField] TMP_Text title;

    [SerializeField] GameObject timeChartPrefab;

    [SerializeField] Transform timeChartGroup;

    // 생성된 차트 리스트
    List<GameObject> instantiatedCharts = new List<GameObject>();
    List<PieChartManager> timePieCharts = new List<PieChartManager>();
    List<StanseBarChartManager> timeBarCharts = new List<StanseBarChartManager>();

    private void Start()
    {
        // 정보 세팅
        LoadLinkData();
    }

    private void LoadLinkData()
    {
        // CSV에서 LinkResult 읽기
        LinkResult result = CsvDataReader.ReadLinkResultCsv();

        if (result == null || result.dataEntries == null)
        {
            Debug.LogWarning("LinkResult가 null이거나 dataEntries가 없습니다.");
            return;
        }

        int dataCount = result.dataEntries.Count;
        // 차트 개수 계산: 최소 2개, 50개마다 +1개, 최대 5개
        int chartCount = Mathf.Clamp(2 + dataCount / 50, 2, 5);

        Debug.Log($"총 댓글 수: {dataCount}, 생성할 차트 수: {chartCount}");

        // 차트 생성
        for (int i = 0; i < chartCount; i++)
        {
            GameObject chart = Instantiate(timeChartPrefab, transform);
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
        DistributeDataToCharts(result.dataEntries, chartCount);
    }

    /// <summary>
    /// 댓글 데이터를 차트 개수로 나눠서 각 차트에 분배
    /// </summary>
    private void DistributeDataToCharts(List<LinkDataEntry> dataEntries, int chartCount)
    {
        if (dataEntries == null || dataEntries.Count == 0 || chartCount == 0)
            return;

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

            // PieChart에 키워드 데이터 추가 (개수 많은 순서대로, 최대 5개 + 기타)
            if (chartIndex < timePieCharts.Count && timePieCharts[chartIndex] != null)
            {
                PieChartManager pieChart = timePieCharts[chartIndex];
                
                // 키워드를 개수 기준으로 내림차순 정렬
                var sortedKeywords = keywordCounts.OrderByDescending(kvp => kvp.Value).ToList();
                
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
        if (newestDays <= 1)
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
            return $"{years}년 전";
        }
        // 1개월 이상
        else if (totalDays >= 30)
        {
            int months = totalDays / 30;
            return $"{months}개월 전";
        }
        // 1주 이상
        else if (totalDays >= 7)
        {
            int weeks = totalDays / 7;
            return $"{weeks}주 전";
        }
        // 1일 이상
        else if (totalDays >= 1)
        {
            return $"{totalDays}일 전";
        }
        // 오늘
        else
        {
            return "오늘";
        }
    }
}
