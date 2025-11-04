using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SearchInputField : MonoBehaviour
{
    [SerializeField] Button searchButton;
    [SerializeField] Canvas loadingCanvas;
    [SerializeField] GameObject loadingPrefab;
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
        Instantiate(loadingPrefab, loadingCanvas.transform);
        DontDestroyOnLoad(loadingCanvas);
    }
}
