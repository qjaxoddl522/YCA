using TMPro;
using UnityEngine;

public class PieChartCategory : MonoBehaviour
{
    TMP_Text categoryText;
    Color myColor;

    private void Awake()
    {
        categoryText = GetComponent<TMP_Text>();
    }

    public void SetCategory(string name, Color color, float percent)
    {
        // TextMeshPro의 올바른 color 태그 형식
        string colorHex = ColorUtility.ToHtmlStringRGB(color);
        categoryText.text = $"<color=#{colorHex}>{name}</color> - {percent:F1}%";
        myColor = color;
    }

    public void HighlightCategory()
    {
        categoryText.fontStyle = FontStyles.Underline;
    }

    public void UnhighlightCategory()
    {
        categoryText.fontStyle = FontStyles.Normal;
    }
}
