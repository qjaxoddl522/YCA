using UnityEngine;
using ChartAndGraph;

public class StanseBarChartManager : MonoBehaviour
{
    [SerializeField] CanvasBarChart barChart;
    VerticalAxis verticalAxis;

    void Start()
    {
        if (barChart == null)
        {
            Debug.LogError("BarChart가 할당되지 않았습니다!");
            return;
        }

        verticalAxis = barChart.GetComponent<VerticalAxis>();
        if (verticalAxis == null)
        {
            Debug.LogError("VerticalAxis를 찾을 수 없습니다!");
            return;
        }

        UpdateAxisDivisions();
    }

    /// <summary>
    /// 긍정, 부정, 중립 중 최댓값을 기준으로 MainDivisions.Total을 설정
    /// (최댓값 / 10)간격으로 표시
    /// </summary>
    public void UpdateAxisDivisions()
    {
        if (barChart == null || verticalAxis == null)
            return;

        // 각 카테고리의 값 가져오기
        double positiveValue = GetCategoryValue("긍정");
        double negativeValue = GetCategoryValue("부정");
        double neutralValue = GetCategoryValue("중립");

        // 최댓값 찾기
        int maxValue = Mathf.Max((int)positiveValue, (int)negativeValue, (int)neutralValue);

        // 커스텀 division 초기화
        barChart.ClearVerticalCustomDivisions();

        // 기본 5개 단위, 30개를 초과할 때마다 5씩 증가
        int intervalStep = Mathf.FloorToInt(Mathf.Max(0, maxValue - 1) / 30f);
        int interval = 5 + (intervalStep * 5);

        verticalAxis.MainDivisions.Total = 0;
        verticalAxis.MainDivisions.Messure = ChartDivisionInfo.DivisionMessure.DataUnits;
        verticalAxis.MainDivisions.UnitsPerDivision = interval;

        // 정수만 표시
        verticalAxis.MainDivisions.FractionDigits = 0;
    }

    /// <summary>
    /// 특정 카테고리의 값 가져오기
    /// </summary>
    private double GetCategoryValue(string categoryName)
    {
        try
        {
            var dataSource = barChart.DataSource;
            if (dataSource == null)
                return 0;

            string groupName = "긍부정";

            return dataSource.GetValue(categoryName, groupName);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"카테고리 '{categoryName}' 값을 가져오는 중 오류 발생: {e.Message}");
            return 0;
        }
    }

    /// <summary>
    /// 카테고리 값 업데이트 (외부에서 호출 가능)
    /// </summary>
    public void SetCategoryValue(string categoryName, double value)
    {
        if (barChart == null)
            return;

        string groupName = "긍부정";
        barChart.DataSource.SetValue(categoryName, groupName, value);
        
        // 축 divisions 업데이트
        UpdateAxisDivisions();
    }
}
