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
    /// <summary>
    /// 제목 최대 길이 (이 값을 초과하면 "..."으로 생략됨)
    /// </summary>
    public static int MaxTitleLength = 50;

    public static LinkResult ReadLinkResultCsv()
    {
        var filePath = Path.Combine(PythonGetCsv.analyzeDir, "analyzed_comments.csv");
        if (!File.Exists(filePath))
        { 
            Debug.LogError("CSV 없음: " + filePath); 
            return new LinkResult(); 
        }

        Debug.Log($"CSV 읽기: {filePath}");

        var result = new LinkResult();

        try
        {
            using (StreamReader reader = new StreamReader(filePath))
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

        string filePath = Path.Combine(PythonGetCsv.youtubeCollectorDir, "youtube_keyword_results.csv");

        if (!File.Exists(filePath))
        {
            Debug.LogError($"CSV 파일을 찾을 수 없습니다: {filePath}");
            return videoList;
        }

        try
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                // 헤더 라인 읽기
                string headerLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(headerLine))
                {
                    Debug.LogError("CSV 헤더가 비어있습니다.");
                    return videoList;
                }

                // 헤더에서 컬럼 인덱스 찾기
                string[] headers = ParseCSVLine(headerLine);
                int titleIdx = Array.IndexOf(headers, "video_title");
                int linkIdx = Array.IndexOf(headers, "video_link");
                int viewsIdx = Array.IndexOf(headers, "views");
                int thumbnailIdx = Array.IndexOf(headers, "thumbnail");

                if (titleIdx < 0 || linkIdx < 0 || viewsIdx < 0 || thumbnailIdx < 0)
                {
                    Debug.LogError($"필수 컬럼을 찾을 수 없습니다. video_title: {titleIdx}, video_link: {linkIdx}, views: {viewsIdx}, thumbnail: {thumbnailIdx}");
                    return videoList;
                }

                Debug.Log($"컬럼 인덱스 - 제목: {titleIdx}, 링크: {linkIdx}, 조회수: {viewsIdx}, 썸네일: {thumbnailIdx}");

                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // CSV 파싱 (콤마로 구분, 단 따옴표 안의 콤마는 무시)
                    string[] values = ParseCSVLine(line);

                    if (values.Length > Mathf.Max(titleIdx, linkIdx, viewsIdx, thumbnailIdx))
                    {
                        string videoTitle = titleIdx < values.Length ? values[titleIdx] : "";
                        string videoLink = linkIdx < values.Length ? values[linkIdx] : "";
                        string viewsStr = viewsIdx < values.Length ? values[viewsIdx] : "0";
                        string thumbnailUrl = thumbnailIdx < values.Length ? values[thumbnailIdx] : "";

                        // 제목에서 폰트 미지원 문자 제거 및 길이 제한
                        videoTitle = RemoveUnsupportedCharacters(videoTitle);
                        videoTitle = TruncateTitle(videoTitle, MaxTitleLength);

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
    /// 링크 검색 결과 CSV에서 첫 번째 비디오 정보를 읽습니다.
    /// </summary>
    public static bool TryReadLinkPrimaryVideo(out YoutubeVideoData videoData)
    {
        videoData = null;
        string filePath = Path.Combine(PythonGetCsv.youtubeCollectorDir, "youtube_link_results.csv");
        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"링크 CSV 파일을 찾을 수 없습니다: {filePath}");
            return false;
        }

        try
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                string headerLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(headerLine))
                {
                    Debug.LogWarning("링크 CSV 헤더가 비어있습니다.");
                    return false;
                }

                string[] headers = ParseCSVLine(headerLine);
                int titleIdx = Array.IndexOf(headers, "video_title");
                int linkIdx = Array.IndexOf(headers, "video_link");
                int viewsIdx = Array.IndexOf(headers, "views");
                int thumbnailIdx = Array.IndexOf(headers, "thumbnail");

                if (titleIdx < 0 || linkIdx < 0 || viewsIdx < 0 || thumbnailIdx < 0)
                {
                    Debug.LogWarning("링크 CSV에서 필수 컬럼을 찾을 수 없습니다.");
                    return false;
                }

                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    string[] values = ParseCSVLine(line);
                    if (values.Length <= Mathf.Max(titleIdx, linkIdx, viewsIdx, thumbnailIdx))
                        continue;

                    string videoTitle = titleIdx < values.Length ? values[titleIdx] : "";
                    string videoLink = linkIdx < values.Length ? values[linkIdx] : "";
                    string viewsStr = viewsIdx < values.Length ? values[viewsIdx] : "0";
                    string thumbnailUrl = thumbnailIdx < values.Length ? values[thumbnailIdx] : "";

                    videoTitle = RemoveUnsupportedCharacters(videoTitle);
                    videoTitle = TruncateTitle(videoTitle, MaxTitleLength);

                    int views = 0;
                    int.TryParse(viewsStr, out views);

                    videoData = new YoutubeVideoData(videoTitle, videoLink, thumbnailUrl, views);
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"링크 CSV 읽기 중 오류 발생: {e.Message}");
        }

        return false;
    }

    /// <summary>
    /// analyzed_keywords.csv에서 키워드별 총 합계를 읽어옵니다.
    /// </summary>
    public static Dictionary<string, int> ReadKeywordSummaryCsv()
    {
        var keywordTotals = new Dictionary<string, int>();
        string filePath = Path.Combine(PythonGetCsv.analyzeDir, "analyzed_keywords.csv");

        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"키워드 CSV 파일을 찾을 수 없습니다: {filePath}");
            return keywordTotals;
        }

        try
        {
            using (var reader = new StreamReader(filePath))
            {
                string headerLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(headerLine))
                {
                    Debug.LogWarning("키워드 CSV 헤더가 비어있습니다.");
                    return keywordTotals;
                }

                string[] headers = ParseCSVLine(headerLine);
                int keywordIdx = Array.IndexOf(headers, "keyword");
                int countIdx = Array.IndexOf(headers, "count");

                if (keywordIdx < 0 || countIdx < 0)
                {
                    Debug.LogWarning("키워드 CSV에서 필수 컬럼을 찾을 수 없습니다.");
                    return keywordTotals;
                }

                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    string[] values = ParseCSVLine(line);
                    if (values.Length <= Mathf.Max(keywordIdx, countIdx))
                        continue;

                    string keyword = keywordIdx < values.Length ? values[keywordIdx].Trim() : string.Empty;
                    string countStr = countIdx < values.Length ? values[countIdx] : "0";

                    if (string.IsNullOrEmpty(keyword))
                        continue;

                    if (!int.TryParse(countStr, out int count))
                        count = 0;

                    if (keywordTotals.ContainsKey(keyword))
                        keywordTotals[keyword] += count;
                    else
                        keywordTotals[keyword] = count;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"키워드 CSV 읽기 중 오류 발생: {e.Message}");
        }

        return keywordTotals;
    }

    /// <summary>
    /// 이모지 및 폰트 미지원 문자를 제거합니다
    /// </summary>
    public static string RemoveUnsupportedCharacters(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            
            // 서로게이트 페어(2바이트 문자) 처리
            if (char.IsHighSurrogate(c) && i + 1 < text.Length)
            {
                int codePoint = char.ConvertToUtf32(text, i);
                
                // 이모지 범위 확인하여 제거
                if ((codePoint >= 0x1F300 && codePoint <= 0x1F9FF) ||  // 이모지
                    (codePoint >= 0x1FA00 && codePoint <= 0x1FAFF) ||  // 추가 이모지
                    (codePoint >= 0x2600 && codePoint <= 0x26FF) ||    // 기타 심볼
                    (codePoint >= 0x2700 && codePoint <= 0x27BF) ||    // 딩뱃
                    (codePoint >= 0x1F600 && codePoint <= 0x1F64F))    // 얼굴 이모지
                {
                    i++; // 서로게이트 페어의 두 번째 문자 건너뛰기
                    continue;
                }
                else
                {
                    // 이모지가 아니면 포함
                    sb.Append(c);
                    i++;
                    if (i < text.Length)
                        sb.Append(text[i]);
                }
            }
            else
            {
                int charCode = (int)c;
                
                // 한글 자모 범위 제거 (폰트에서 지원 안 함)
                // U+1100~U+11FF: 한글 자모 (초성, 중성, 종성)
                // U+3130~U+318F: 한글 호환 자모
                if ((charCode >= 0x1100 && charCode <= 0x11FF) ||
                    (charCode >= 0x3130 && charCode <= 0x318F))
                {
                    continue; // 건너뛰기
                }
                
                // 기타 특수 심볼 제거
                if ((charCode >= 0x2190 && charCode <= 0x21FF) ||  // 화살표
                    (charCode >= 0x2200 && charCode <= 0x22FF) ||  // 수학 기호
                    (charCode >= 0x25A0 && charCode <= 0x25FF))    // 도형
                {
                    continue; // 건너뛰기
                }
                
                // 제어 문자 제거 (탭, 줄바꿈 제외)
                if (char.IsControl(c) && c != '\t' && c != '\n' && c != '\r')
                {
                    continue;
                }
                
                // 정상 문자는 추가
                sb.Append(c);
            }
        }
        
        return sb.ToString().Trim();
    }

    /// <summary>
    /// 제목을 지정된 길이로 제한합니다 (초과 시 "..."으로 생략)
    /// </summary>
    public static string TruncateTitle(string title, int maxLength = 50)
    {
        if (string.IsNullOrEmpty(title))
            return title;

        title = title.Trim();
        
        if (title.Length <= maxLength)
            return title;

        return title.Substring(0, maxLength) + "...";
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
