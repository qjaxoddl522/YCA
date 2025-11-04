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

    public void ShowOverallKeywordContent()
    {
        if (overallKeywordContentObject != null)
            overallKeywordContentObject.SetActive(true);
        
        if (videoSelectedContent != null)
            videoSelectedContent.SetActive(false);

        var pie = overallKeywordContentObject?.GetComponent<PieChartManager>();
    }
    
    public void ShowVideoSelectedContent(string title)
    {
        if (overallKeywordContentObject != null)
            overallKeywordContentObject.SetActive(false);
        
        if (videoSelectedContent != null)
            videoSelectedContent.SetActive(true);
        
        titleText.text = title;

        var result = CsvReader.ReadLinkResultCsv();

        var pie = videoSelectedContent?.GetComponent<PieChartManager>();
        if (pie != null)
        {
            pie.ClearAllCategories();
            foreach (var keyword in result.keywordNumbers.Keys)
            {
                pie.AddCategory(keyword, result.keywordNumbers[keyword]);
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
    }
}

