using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public static class CsvLoader
{

    public static List<Question> LoadQuestionsFromResources(string path = "quiz", string onlyPartId = null)
    {
        var txt = Resources.Load<TextAsset>(path);
        if (!txt) { Debug.LogError("CSV not found at Resources/" + path); return new List<Question>(); }

        var lines = txt.text.Replace("\r", "").Split('\n');
        var list = new List<Question>();
        if (lines.Length <= 1) return list;

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = ParseCsvLine(line);
            if (cols.Count < 7) continue;

            string partId = cols[0].Trim();
            if (!string.IsNullOrWhiteSpace(onlyPartId) &&
                !string.Equals(partId, onlyPartId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var q = new Question
            {
                partId = partId,
                title = cols[1],
                opts = new[] { cols[2], cols[3], cols[4], cols[5] },
                correct = Mathf.Clamp("ABCD".IndexOf(cols[6].Trim().ToUpperInvariant()[0]), 0, 3)
            };
            list.Add(q);
        }
        return list;
    }

    // Load random số lượng câu hỏi từ file CSV trong Resources (mặc định 20 câu).
    // Nếu ít hơn số lượng yêu cầu thì sẽ trả về toàn bộ và log warning.
    public static List<Question> LoadRandomQuestionsFromResources(string path = "quiz", int count = 20, string onlyPartId = null)
    {
        var allQuestions = LoadQuestionsFromResources(path, onlyPartId);
        if (allQuestions == null || allQuestions.Count == 0) return new List<Question>();

        if (allQuestions.Count <= count)
        {
            // Trộn nhẹ để UI vẫn thấy thứ tự không quá giống nhau.
            var shuffled = new List<Question>(allQuestions);
            ShuffleInPlace(shuffled);
            return shuffled;
        }

        var list = new List<Question>(allQuestions);
        ShuffleInPlace(list);
        return list.GetRange(0, count);
    }

    static void ShuffleInPlace(List<Question> list)
    {
        // Fisher-Yates shuffle
        for (int i = list.Count - 1; i > 0; i--)
        {
            // Explicit UnityEngine.Random để tránh mơ hồ với System.Random
            int j = UnityEngine.Random.Range(0, i + 1);
            var tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }


    static List<string> ParseCsvLine(string line)
    {
        var res = new List<string>();
        bool quoted = false;
        var cur = new System.Text.StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '\"') { cur.Append('\"'); i++; }
                else quoted = !quoted;
            }
            else if (c == ',' && !quoted)
            {
                res.Add(cur.ToString());
                cur.Length = 0;
            }
            else cur.Append(c);
        }
        res.Add(cur.ToString());
        return res;
    }
}


[Serializable]
public class Question
{
    public string partId;
    public string title;
    public string[] opts = new string[4];
    public int correct;
}

[Serializable]
public class QuizSet
{
    public string partId;
    public List<Question> questions = new();
}