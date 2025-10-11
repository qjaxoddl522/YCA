using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SearchInputField : MonoBehaviour
{
    [SerializeField] Button searchButton;
    TMP_InputField input;

    void Awake()
    {
        input = GetComponent<TMP_InputField>();
        input.onSubmit.AddListener(OnSubmit);
        searchButton.onClick.AddListener(() => OnSubmit(input.text));
    }

    void OnSubmit(string text)
    {
        Debug.Log($"검색: {text}");
    }
}
