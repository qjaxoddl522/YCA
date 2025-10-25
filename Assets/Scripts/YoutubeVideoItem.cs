using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

/// <summary>
/// 개별 YouTube 비디오 아이템 UI를 관리하는 클래스
/// </summary>
public class YoutubeVideoItem : MonoBehaviour
{
    [Header("UI 요소")]
    [Tooltip("비디오 제목을 표시할 텍스트")]
    public TextMeshProUGUI titleText;

    [Tooltip("비디오 조회수를 표시할 텍스트 (선택사항)")]
    public TextMeshProUGUI viewsText;

    [Tooltip("썸네일 이미지를 표시할 Image 컴포넌트")]
    public Image thumbnailImage;

    [Tooltip("링크 버튼 (선택사항)")]
    public Button linkButton;

    [Tooltip("썸네일 로딩 중 표시할 오브젝트 (선택사항)")]
    public GameObject thumbnailLoadingIndicator;

    [Header("기본 이미지")]
    [Tooltip("썸네일 로딩 실패 시 표시할 기본 스프라이트")]
    public Sprite defaultThumbnailSprite;

    private YoutubeVideoData videoData;
    private int itemIndex;

    /// <summary>
    /// 비디오 데이터를 설정하고 UI를 업데이트합니다
    /// </summary>
    public void SetVideoData(YoutubeVideoData data, int index)
    {
        videoData = data;
        itemIndex = index;

        UpdateUI();
    }

    /// <summary>
    /// UI 요소들을 업데이트합니다
    /// </summary>
    private void UpdateUI()
    {
        if (videoData == null)
            return;

        // 제목 설정
        if (titleText != null)
        {
            titleText.text = videoData.title;
        }

        // 조회수 설정
        if (viewsText != null)
        {
            viewsText.text = FormatViews(videoData.views);
        }

        // 썸네일 로드
        if (thumbnailImage != null && !string.IsNullOrEmpty(videoData.thumbnailUrl))
        {
            StartCoroutine(LoadThumbnail(videoData.thumbnailUrl));
        }

        // 링크 버튼 설정
        if (linkButton != null)
        {
            linkButton.onClick.RemoveAllListeners();
            linkButton.onClick.AddListener(OpenVideoLink);
        }
    }

    /// <summary>
    /// 썸네일 이미지를 다운로드하여 표시합니다
    /// </summary>
    private IEnumerator LoadThumbnail(string url)
    {
        // 로딩 표시
        if (thumbnailLoadingIndicator != null)
            thumbnailLoadingIndicator.SetActive(true);

        // 기본 이미지 설정
        if (defaultThumbnailSprite != null)
            thumbnailImage.sprite = defaultThumbnailSprite;

        // 이미지 다운로드
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 텍스처를 스프라이트로 변환
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f)
                );

                thumbnailImage.sprite = sprite;
            }
            else
            {
                Debug.LogWarning($"썸네일 로드 실패: {url}\n{request.error}");
            }
        }

        // 로딩 표시 숨김
        if (thumbnailLoadingIndicator != null)
            thumbnailLoadingIndicator.SetActive(false);
    }

    /// <summary>
    /// 조회수를 포맷팅합니다 (예: 1,234,567 -> 1.2M)
    /// </summary>
    private string FormatViews(int views)
    {
        if (views >= 1000000)
        {
            return $"{(views / 1000000f):F1}M";
        }
        else if (views >= 1000)
        {
            return $"{(views / 1000f):F1}K";
        }
        else
        {
            return $"{views}";
        }
    }

    /// <summary>
    /// 비디오 링크를 브라우저에서 엽니다
    /// </summary>
    public void OpenVideoLink()
    {
        if (videoData != null && !string.IsNullOrEmpty(videoData.link))
        {
            Debug.Log($"비디오 링크 열기: {videoData.link}");
            Application.OpenURL(videoData.link);
        }
    }

    /// <summary>
    /// 현재 비디오 데이터를 반환합니다
    /// </summary>
    public YoutubeVideoData GetVideoData()
    {
        return videoData;
    }

    /// <summary>
    /// 아이템 인덱스를 반환합니다
    /// </summary>
    public int GetIndex()
    {
        return itemIndex;
    }
}

