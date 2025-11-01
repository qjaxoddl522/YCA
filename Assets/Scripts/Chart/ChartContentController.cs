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
    [SerializeField] private Image thumbnailImage;
    [SerializeField] private TMP_Text titleText;

    public void ShowOverallKeywordContent()
    {
        if (overallKeywordContentObject != null)
            overallKeywordContentObject.SetActive(true);
        
        if (videoSelectedContent != null)
            videoSelectedContent.SetActive(false);

        var pie = overallKeywordContentObject?.GetComponent<PieChartManager>();
    }
    
    public void ShowVideoSelectedContent(Sprite thumbnail, string title)
    {   
        if (overallKeywordContentObject != null)
            overallKeywordContentObject.SetActive(false);
        
        if (videoSelectedContent != null)
            videoSelectedContent.SetActive(true);

        // null 체크
        if (thumbnailImage == null)
        {
            Debug.LogError("❌ NRE 원인: thumbnailImage가 null입니다!");
            Debug.LogError("→ ChartContentController Inspector에서 thumbnailImage를 할당하세요.");
        }
        else
        {
            thumbnailImage.sprite = thumbnail;
        }
        
        if (titleText == null)
        {
            Debug.LogError("❌ NRE 원인: titleText가 null입니다!");
            Debug.LogError("→ ChartContentController Inspector에서 titleText를 할당하세요.");
        }
        else
        {
            titleText.text = title;
        }

        var bar = videoSelectedContent?.GetComponent<PositiveBarChartManager>();
    }
}

