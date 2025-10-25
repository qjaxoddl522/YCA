using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// YouTube 비디오 데이터를 저장하는 클래스
/// </summary>
[Serializable]
public class YoutubeVideoData
{
    public string title;           // 비디오 제목
    public string link;            // 비디오 링크
    public string thumbnailUrl;    // 썸네일 이미지 URL
    public int views;              // 조회수

    public YoutubeVideoData(string title, string link, string thumbnailUrl, int views)
    {
        this.title = title;
        this.link = link;
        this.thumbnailUrl = thumbnailUrl;
        this.views = views;
    }
}

/// <summary>
/// CSV 파일에서 YouTube 데이터를 읽어오는 정적 클래스
/// </summary>
public static class YoutubeDataReader
{
    /// <summary>
    /// CSV 파일을 읽어서 고유한 YouTube 비디오 데이터 리스트를 반환합니다
    /// </summary>
    /// <param name="csvFileName">CSV 파일 이름 (Assets/Data/ 폴더 내)</param>
    /// <returns>YouTube 비디오 데이터 리스트</returns>
    public static List<YoutubeVideoData> ReadCSV(string csvFileName)
    {
        List<YoutubeVideoData> videoList = new List<YoutubeVideoData>();
        HashSet<string> processedLinks = new HashSet<string>(); // 중복 제거용

        string filePath = Path.Combine(Application.dataPath, "Data", csvFileName);

        if (!File.Exists(filePath))
        {
            Debug.LogError($"CSV 파일을 찾을 수 없습니다: {filePath}");
            return videoList;
        }

        try
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                // 헤더 라인 건너뛰기
                string headerLine = reader.ReadLine();
                
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // CSV 파싱 (콤마로 구분, 단 따옴표 안의 콤마는 무시)
                    string[] values = ParseCSVLine(line);

                    if (values.Length >= 4)
                    {
                        string videoTitle = values[0];
                        string videoLink = values[1];
                        string viewsStr = values[2];
                        string thumbnailUrl = values[3];

                        // 중복된 링크는 건너뛰기 (같은 비디오가 여러 댓글과 함께 반복됨)
                        if (processedLinks.Contains(videoLink))
                            continue;

                        processedLinks.Add(videoLink);

                        // 조회수 파싱
                        int views = 0;
                        int.TryParse(viewsStr, out views);

                        YoutubeVideoData videoData = new YoutubeVideoData(
                            videoTitle,
                            videoLink,
                            thumbnailUrl,
                            views
                        );

                        videoList.Add(videoData);
                    }
                }
            }

            Debug.Log($"총 {videoList.Count}개의 고유한 비디오를 로드했습니다.");
        }
        catch (Exception e)
        {
            Debug.LogError($"CSV 파일 읽기 중 오류 발생: {e.Message}");
        }

        return videoList;
    }

    /// <summary>
    /// CSV 라인을 파싱합니다 (따옴표 처리 포함)
    /// </summary>
    private static string[] ParseCSVLine(string line)
    {
        List<string> values = new List<string>();
        bool inQuotes = false;
        string currentValue = "";

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(currentValue);
                currentValue = "";
            }
            else
            {
                currentValue += c;
            }
        }

        // 마지막 값 추가
        values.Add(currentValue);

        return values.ToArray();
    }

    /// <summary>
    /// 기본 CSV 파일("youtube_keyword_results 인기순.csv")을 읽어옵니다
    /// </summary>
    public static List<YoutubeVideoData> ReadDefaultCSV()
    {
        return ReadCSV("youtube_keyword_results 인기순.csv");
    }

    /// <summary>
    /// 비디오 데이터를 콘솔에 출력합니다 (디버깅용)
    /// </summary>
    public static void PrintVideoData(List<YoutubeVideoData> videos)
    {
        Debug.Log("=== YouTube 비디오 목록 ===");
        for (int i = 0; i < videos.Count; i++)
        {
            YoutubeVideoData video = videos[i];
            Debug.Log($"[{i + 1}] 제목: {video.title}");
            Debug.Log($"    링크: {video.link}");
            Debug.Log($"    썸네일: {video.thumbnailUrl}");
            Debug.Log($"    조회수: {video.views:N0}");
            Debug.Log("---");
        }
    }
}

