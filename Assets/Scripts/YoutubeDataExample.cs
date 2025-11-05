using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// YoutubeDataReader 사용 예제
/// Unity 오브젝트에 이 스크립트를 추가하면 자동으로 CSV를 로드합니다
/// </summary>
public class YoutubeDataExample : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("자동으로 CSV 로드 여부")]
    public bool loadOnStart = true;

    [Header("읽기 전용 - 로드된 데이터")]
    [SerializeField]
    private List<YoutubeVideoData> loadedVideos = new List<YoutubeVideoData>();

    private void Start()
    {
        if (loadOnStart)
        {
            LoadYoutubeData();
        }
    }

    /// <summary>
    /// YouTube 데이터를 로드합니다
    /// </summary>
    public void LoadYoutubeData()
    {
        Debug.Log("YouTube 데이터 로드 시작...");
        
        // CSV 파일에서 데이터 읽기
        loadedVideos = CsvDataReader.ReadYoutubeVideoDataCSV();

        // 데이터 출력 (디버깅용)
        CsvDataReader.PrintVideoData(loadedVideos);

        // 로드된 데이터로 뭔가 하기
        DisplayVideoInfo();
    }

    /// <summary>
    /// 로드된 비디오 정보를 표시합니다
    /// </summary>
    private void DisplayVideoInfo()
    {
        if (loadedVideos == null || loadedVideos.Count == 0)
        {
            Debug.LogWarning("로드된 비디오가 없습니다.");
            return;
        }

        Debug.Log($"총 {loadedVideos.Count}개의 비디오가 로드되었습니다.");

        // 예시: 조회수가 가장 높은 비디오 찾기
        YoutubeVideoData mostViewed = GetMostViewedVideo();
        if (mostViewed != null)
        {
            Debug.Log($"<color=yellow>가장 조회수가 높은 비디오:</color>");
            Debug.Log($"제목: {mostViewed.title}");
            Debug.Log($"조회수: {mostViewed.views:N0}");
            Debug.Log($"링크: {mostViewed.link}");
        }
    }

    /// <summary>
    /// 조회수가 가장 높은 비디오를 반환합니다
    /// </summary>
    public YoutubeVideoData GetMostViewedVideo()
    {
        if (loadedVideos == null || loadedVideos.Count == 0)
            return null;

        YoutubeVideoData mostViewed = loadedVideos[0];
        foreach (var video in loadedVideos)
        {
            if (video.views > mostViewed.views)
            {
                mostViewed = video;
            }
        }

        return mostViewed;
    }

    /// <summary>
    /// 특정 인덱스의 비디오 데이터를 가져옵니다
    /// </summary>
    public YoutubeVideoData GetVideoAt(int index)
    {
        if (loadedVideos == null || index < 0 || index >= loadedVideos.Count)
            return null;

        return loadedVideos[index];
    }

    /// <summary>
    /// 로드된 모든 비디오 데이터를 반환합니다
    /// </summary>
    public List<YoutubeVideoData> GetAllVideos()
    {
        return loadedVideos;
    }

    /// <summary>
    /// 비디오 개수를 반환합니다
    /// </summary>
    public int GetVideoCount()
    {
        return loadedVideos != null ? loadedVideos.Count : 0;
    }

    // Unity 에디터에서 테스트용 버튼
    [ContextMenu("CSV 데이터 다시 로드")]
    private void ReloadData()
    {
        LoadYoutubeData();
    }

    [ContextMenu("첫 번째 비디오 정보 출력")]
    private void PrintFirstVideo()
    {
        if (loadedVideos != null && loadedVideos.Count > 0)
        {
            var video = loadedVideos[0];
            Debug.Log($"제목: {video.title}\n링크: {video.link}\n썸네일: {video.thumbnailUrl}");
        }
    }
}

