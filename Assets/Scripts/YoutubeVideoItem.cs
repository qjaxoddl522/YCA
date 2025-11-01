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
    public TextMeshProUGUI title;

    [Tooltip("비디오 조회수를 표시할 텍스트 (선택사항)")]
    public TextMeshProUGUI viewsText;

    [Tooltip("썸네일 이미지를 표시할 Image 컴포넌트")]
    public Image thumbnailImage;

    [Tooltip("선택 범위 오브젝트")]
    public GameObject selectArea;

    [Tooltip("링크 버튼")]
    public Button linkButton;

    [Tooltip("썸네일 로딩 중 표시할 오브젝트 (선택사항)")]
    public GameObject thumbnailLoadingIndicator;

    [Header("기본 이미지")]
    [Tooltip("썸네일 로딩 실패 시 표시할 기본 스프라이트")]
    public Sprite defaultThumbnailSprite;

    private static YoutubeVideoItem currentSelectedItem = null;
    Color selectedColor = new Color(0, 0, 0, 0.3f);
    Color deselectedColor = new Color(1, 1, 1, 0);

    private ChartContentController contentController;
    private YoutubeVideoData videoData;
    private int itemIndex;
    private Image itemButtonImage;
    private Button itemButton;

    /// <summary>
    /// 비디오 데이터를 설정하고 UI를 업데이트합니다
    /// </summary>
    public void SetVideoData(ChartContentController controller, YoutubeVideoData data, int index)
    {
        videoData = data;
        itemIndex = index;
        itemButtonImage = selectArea.GetComponent<Image>();
        itemButton = selectArea.GetComponent<Button>();
        contentController = controller;

        UpdateUI();
    }

    /// <summary>
    /// UI 요소들을 업데이트
    /// </summary>
    private void UpdateUI()
    {
        if (videoData == null)
            return;

        // 제목 설정
        if (title != null)
        {
            title.text = videoData.title;
        }

        // 조회수 설정
        if (viewsText != null)
        {
            viewsText.text = FormatViews(videoData.views);
        }

        // 썸네일 로드 (16:9 비율로 변환)
        if (thumbnailImage != null && !string.IsNullOrEmpty(videoData.thumbnailUrl))
        {
            string thumbnail16x9 = ConvertTo16x9Thumbnail(videoData.thumbnailUrl);
            StartCoroutine(LoadThumbnail(thumbnail16x9));
        }

        // 링크 버튼 설정
        if (linkButton != null)
        {
            linkButton.onClick.RemoveAllListeners();
            linkButton.onClick.AddListener(OpenVideoLink);
        }

        if (itemButton != null)
        {
            itemButton.onClick.RemoveAllListeners();
            itemButton.onClick.AddListener(SetSelected);
        }
    }

    /// <summary>
    /// 썸네일 URL을 16:9 비율로 변환 (hqdefault → mqdefault)
    /// </summary>
    private string ConvertTo16x9Thumbnail(string thumbnailUrl)
    {
        if (string.IsNullOrEmpty(thumbnailUrl))
            return thumbnailUrl;
        
        // hqdefault.jpg (480x360, 4:3) → mqdefault.jpg (320x180, 16:9)
        thumbnailUrl = thumbnailUrl.Replace("hqdefault.jpg", "mqdefault.jpg");
        
        // sddefault.jpg (640x480, 4:3) → mqdefault.jpg (320x180, 16:9)
        thumbnailUrl = thumbnailUrl.Replace("sddefault.jpg", "mqdefault.jpg");
        
        // 고화질 원하면 maxresdefault.jpg (1280x720, 16:9) 사용
        // 주의: 일부 오래된 영상은 maxresdefault가 없을 수 있음
        // thumbnailUrl = thumbnailUrl.Replace("hqdefault.jpg", "maxresdefault.jpg");
        
        return thumbnailUrl;
    }
    
    /// <summary>
    /// 썸네일 이미지를 다운로드하여 표시
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
    /// 조회수를 포맷팅 (예: 1,234,567 -> 1.2M)
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
    /// 비디오 링크를 브라우저에서 열기
    /// </summary>
    public void OpenVideoLink()
    {
        if (videoData != null && !string.IsNullOrEmpty(videoData.link))
        {
            Application.OpenURL(videoData.link);
        }
    }

    /// <summary>
    /// 선택 처리하여 데이터 표시
    /// </summary>
    public void SetSelected()
    {
        if (currentSelectedItem != null && currentSelectedItem != this)
        {
            currentSelectedItem.SetDeselected();
        }

        // 현재 아이템 선택
        currentSelectedItem = this;

        Sprite sprite = thumbnailImage.sprite;
        string titleText = title.text;

        itemButtonImage.color = selectedColor;
        selectArea.SetActive(true);

        contentController.ShowVideoSelectedContent(sprite, titleText);
    }

    public void SetDeselected()
    {
        if (itemButtonImage != null)
            itemButtonImage.color = deselectedColor;
    }

    /// <summary>
    /// 현재 비디오 데이터 반환
    /// </summary>
    public YoutubeVideoData GetVideoData()
    {
        return videoData;
    }

    /// <summary>
    /// 아이템 인덱스 반환
    /// </summary>
    public int GetIndex()
    {
        return itemIndex;
    }
}