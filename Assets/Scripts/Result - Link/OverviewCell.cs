using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OverviewCell : MonoBehaviour
{
    [SerializeField] Image image;
    [SerializeField] TMP_Text label;

    public void UpdateColor(Color color)
    {
        image.color = color;
    }

    public void UpdateLabel(string text)
    {
        label.text = text;
    }

    public void UpdateLabelColor(Color color)
    {
        label.color = color;
    }
}
