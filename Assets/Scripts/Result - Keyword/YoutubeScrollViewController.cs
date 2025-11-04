using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

/// <summary>
/// YouTube 비디오 목록을 스크롤뷰에 표시하는 컨트롤러
/// </summary>
public class YoutubeScrollViewController : MonoBehaviour
{
    [Header("UI 참조")]
    [Tooltip("비디오 아이템이 추가될 Content 오브젝트")]
    public Transform contentParent;

    [Tooltip("비디오 아이템 프리팹 (YoutubeVideoItem 컴포넌트 포함)")]
    public GameObject videoItemPrefab;

    [Tooltip("프리팹에 전달할 ContentController")]
    public ChartContentController contentController;

    [Header("설정")]
    [Tooltip("시작 시 자동으로 로드")]
    public bool loadOnStart = true;

    [Tooltip("최대 표시할 비디오 수 (0 = 전체)")]
    public int maxDisplayCount = 0;

    [Header("로딩")]
    [Tooltip("로딩 표시 오브젝트 (선택사항)")]
    public GameObject loadingIndicator;

    private List<YoutubeVideoData> currentVideos = new();
    private List<YoutubeVideoItem> instantiatedItems = new();

    private void Start()
    {
        if (loadOnStart)
        {
            LoadAndDisplayVideos();
        }
    }

    /// <summary>
    /// CSV에서 비디오 데이터를 로드하고 UI에 표시합니다
    /// </summary>
    public void LoadAndDisplayVideos()
    {
        StartCoroutine(LoadAndDisplayVideosCoroutine());
    }

    private IEnumerator LoadAndDisplayVideosCoroutine()
    {
        // 로딩 표시
        if (loadingIndicator != null)
            loadingIndicator.SetActive(true);

        // 기존 아이템 제거
        ClearVideoItems();

        // CSV에서 데이터 로드
        currentVideos = YoutubeDataReader.ReadDefaultCSV();

        if (currentVideos == null || currentVideos.Count == 0)
        {
            Debug.LogWarning("로드된 비디오가 없습니다.");
            if (loadingIndicator != null)
                loadingIndicator.SetActive(false);
            yield break;
        }

        // 표시할 비디오 개수 결정
        int displayCount = maxDisplayCount > 0 
            ? Mathf.Min(maxDisplayCount, currentVideos.Count) 
            : currentVideos.Count;

        Debug.Log($"{displayCount}개의 비디오를 표시합니다.");

        // UI 아이템 생성
        for (int i = 0; i < displayCount; i++)
        {
            CreateVideoItem(currentVideos[i], i);

            // 프레임마다 하나씩 생성하여 부드럽게 로딩
            if (i % 5 == 0)
                yield return null;
        }

        // 로딩 완료
        if (loadingIndicator != null)
            loadingIndicator.SetActive(false);

        Debug.Log($"총 {instantiatedItems.Count}개의 비디오 아이템이 생성되었습니다.");
    }

    /// <summary>
    /// 개별 비디오 아이템 UI를 생성합니다
    /// </summary>
    private void CreateVideoItem(YoutubeVideoData videoData, int index)
    {
        if (videoItemPrefab == null)
        {
            Debug.LogError("videoItemPrefab이 설정되지 않았습니다!");
            return;
        }

        if (contentParent == null)
        {
            Debug.LogError("contentParent가 설정되지 않았습니다!");
            return;
        }

        // 프리팹 인스턴스 생성
        GameObject itemObj = Instantiate(videoItemPrefab, contentParent);
        itemObj.name = $"VideoItem_{index}";

        YoutubeVideoItem videoItem = itemObj.GetComponent<YoutubeVideoItem>();
        if (videoItem != null)
        {
            videoItem.SetVideoData(contentController, videoData, index);
        }
        else
        {
            Debug.LogWarning($"VideoItem 프리팹에 YoutubeVideoItem 컴포넌트가 없습니다.");
        }

        instantiatedItems.Add(videoItem);
    }

    /// <summary>
    /// 생성된 모든 비디오 아이템을 제거합니다
    /// </summary>
    public void ClearVideoItems()
    {
        foreach (YoutubeVideoItem item in instantiatedItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        instantiatedItems.Clear();
    }

    /// <summary>
    /// 특정 비디오로 스크롤합니다
    /// </summary>
    public void ScrollToVideo(int index)
    {
        if (index < 0 || index >= instantiatedItems.Count)
            return;

        // ScrollRect 컴포넌트 찾기
        ScrollRect scrollRect = GetComponentInParent<ScrollRect>();
        if (scrollRect != null && contentParent != null)
        {
            Canvas.ForceUpdateCanvases();
            
            RectTransform targetRect = instantiatedItems[index].GetComponent<RectTransform>();
            RectTransform contentRect = contentParent.GetComponent<RectTransform>();
            
            float targetPosition = Mathf.Clamp01(
                1 - (targetRect.anchoredPosition.y / contentRect.rect.height)
            );
            
            scrollRect.verticalNormalizedPosition = targetPosition;
        }
    }

    /// <summary>
    /// 현재 로드된 비디오 수를 반환
    /// </summary>
    public int GetVideoCount()
    {
        return currentVideos.Count;
    }

    // Unity 에디터 테스트용
    [ContextMenu("비디오 로드 및 표시")]
    private void TestLoadVideos()
    {
        LoadAndDisplayVideos();
    }

    [ContextMenu("아이템 전부 제거")]
    private void TestClearItems()
    {
        ClearVideoItems();
    }
}


