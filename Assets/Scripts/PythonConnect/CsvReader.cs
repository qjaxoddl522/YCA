using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

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

public struct LinkResult
{
    // 키워드 : 개수
    public Dictionary<string, int> keywordNumbers;
    // 긍부정 : 개수
    public Dictionary<Stance, int> stanceNumbers;
}

public static class CsvReader
{
    public static LinkResult ReadLinkResultCsv()
    {
        var path = Path.Combine(Application.persistentDataPath, "result_link.csv");
        if (!File.Exists(path)) { Debug.LogError("CSV 없음: " + path); return new LinkResult(); }
        Debug.Log($"CSV 읽기: {path}");

        var lines = File.ReadAllLines(path); // UTF-8(BOM) 자동 처리
        if (lines.Length == 0) return new LinkResult();

        var headers = lines[0].Split(',');
        var result = new LinkResult();
        result.keywordNumbers = new Dictionary<string, int>();
        result.stanceNumbers = new Dictionary<Stance, int>();
        result.stanceNumbers[Stance.Positive] = 0;
        result.stanceNumbers[Stance.Negative] = 0;
        result.stanceNumbers[Stance.Neutral] = 0;

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cols = lines[i].Split(',');

            int keywordIdx = Array.IndexOf(headers, "키워드");
            int stanceIdx = Array.IndexOf(headers, "긍부정");
            if (keywordIdx >= 0 && stanceIdx >= 0)
            {
                string keyword = cols[keywordIdx];
                var keywordsArray = keyword.Split('|');
                foreach (var kw in keywordsArray)
                {
                    if (result.keywordNumbers.ContainsKey(kw))
                        result.keywordNumbers[kw]++;
                    else
                        result.keywordNumbers[kw] = 1;
                }

                string stanceStr = cols[stanceIdx];
                Stance stance = Stance.Neutral;
                if (stanceStr == "긍정") stance = Stance.Positive;
                else if (stanceStr == "부정") stance = Stance.Negative;
                result.stanceNumbers[stance]++;
            }
        }

        return result;
    }
}
