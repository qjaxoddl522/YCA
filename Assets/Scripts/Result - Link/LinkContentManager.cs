using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LinkContentManager : MonoBehaviour
{
    [SerializeField] Image thumbnail;
    [SerializeField] TMP_Text title;

    [SerializeField] GameObject timeChartPrefab;

    List<PieChartManager> pieCharts = new List<PieChartManager>();
    List<StanseBarChartManager> barCharts = new List<StanseBarChartManager>();

    private void Start()
    {
        // 정보 세팅
    }
}
