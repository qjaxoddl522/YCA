using System.Collections.Generic;
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
    }
}
