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

public enum Stance
{
    Positive,
    Negative,
    Neutral
}

public struct LinkTemplete
{
    public string comment;
    public string date;
    public string keyword;
    public Stance stance;
}

/// <summary>
/// CSV의 각 행 데이터를 저장하는 클래스 (날짜, 키워드, 긍부정을 하나로 묶음)
/// </summary>
[Serializable]
public class LinkDataEntry
{
    public DateTime date;           // 날짜
    public List<string> keywords;   // 키워드 리스트
    public Stance stance;           // 긍부정

    public LinkDataEntry()
    {
        date = DateTime.MinValue;
        keywords = new List<string>();
        stance = Stance.Neutral;
    }

    public LinkDataEntry(DateTime date, List<string> keywords, Stance stance)
    {
        this.date = date;
        this.keywords = keywords;
        this.stance = stance;
    }
}

public class LinkResult
{
    // 키워드 : 개수
    public Dictionary<string, int> keywordNumbers;
    // 긍부정 : 개수
    public Dictionary<Stance, int> stanceNumbers;
    // 전체 데이터 (날짜, 키워드, 긍부정을 하나로 묶은 리스트)
    public List<LinkDataEntry> dataEntries;

    public LinkResult()
    {
        keywordNumbers = new Dictionary<string, int>();
        stanceNumbers = new Dictionary<Stance, int>();
        stanceNumbers[Stance.Positive] = 0;
        stanceNumbers[Stance.Negative] = 0;
        stanceNumbers[Stance.Neutral] = 0;
        dataEntries = new List<LinkDataEntry>();
    }
}

public static class CsvDataReader
{
    public static LinkResult ReadLinkResultCsv()
    {
        var path = Path.Combine(Application.persistentDataPath, "analyzed_comments.csv");
        if (!File.Exists(path)) 
        { 
            Debug.LogError("CSV 없음: " + path); 
            return new LinkResult(); 
        }

        Debug.Log($"CSV 읽기: {path}");

        var result = new LinkResult();

        try
        {
            using (StreamReader reader = new StreamReader(path))
            {
                // 헤더 라인 읽기
                string headerLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(headerLine))
                {
                    Debug.LogError("CSV 헤더가 비어있습니다.");
                    return result;
                }

                // 헤더에서 컬럼 인덱스 찾기
                string[] headers = ParseCSVLine(headerLine);
                int keywordIdx = Array.IndexOf(headers, "keyword");
                int stanceIdx = Array.IndexOf(headers, "sentiment");
                int timeIdx = Array.IndexOf(headers, "time");

                if (keywordIdx < 0 || stanceIdx < 0)
                {
                    Debug.LogError($"필수 컬럼을 찾을 수 없습니다. keyword: {keywordIdx}, sentiment: {stanceIdx}");
                    return result;
                }

                // 데이터 라인 읽기
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    string[] values = ParseCSVLine(line);

                    // 키워드 처리
                    string keyword = keywordIdx < values.Length ? values[keywordIdx] : "";
                    var keywordsList = new List<string>();
                    
                    if (!string.IsNullOrWhiteSpace(keyword))
                    {
                        foreach (var kw in keyword.Split('|', StringSplitOptions.RemoveEmptyEntries))
                        {
                            var trimmed = kw.Trim();
                            if (trimmed.Length > 0)
                            {
                                keywordsList.Add(trimmed);
                                if (result.keywordNumbers.ContainsKey(trimmed))
                                    result.keywordNumbers[trimmed]++;
                                else
                                    result.keywordNumbers[trimmed] = 1;
                            }
                        }
                    }

                    // 긍부정 처리
                    string stanceStr = stanceIdx < values.Length ? values[stanceIdx] : "";
                    Stance stance = stanceStr == "긍정" ? Stance.Positive
                                  : stanceStr == "부정" ? Stance.Negative
                                  : Stance.Neutral;
                    result.stanceNumbers[stance]++;

                    // 날짜 처리
                    DateTime date = DateTime.MinValue;
                    if (timeIdx >= 0 && timeIdx < values.Length)
                    {
                        DateTime.TryParse(values[timeIdx], out date);
                    }

                    // LinkDataEntry 생성 및 추가
                    result.dataEntries.Add(new LinkDataEntry(date, keywordsList, stance));
                }
            }

            Debug.Log($"CSV 읽기 완료: {result.dataEntries.Count}개 엔트리");
        }
        catch (Exception e)
        {
            Debug.LogError($"CSV 파일 읽기 중 오류 발생: {e.Message}");
        }

        return result;
    }

    /// <summary>
    /// CSV 파일을 읽어서 고유한 YouTube 비디오 데이터 리스트를 반환합니다
    /// </summary>
    /// <param name="csvFileName">CSV 파일 이름 (Assets/Data/ 폴더 내)</param>
    /// <returns>YouTube 비디오 데이터 리스트</returns>
    public static List<YoutubeVideoData> ReadYoutubeVideoDataCSV()
    {
        List<YoutubeVideoData> videoList = new List<YoutubeVideoData>();
        HashSet<string> processedLinks = new HashSet<string>(); // 중복 제거용

        string filePath = Path.Combine(Application.persistentDataPath, "youtube_keyword_results.csv");

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
