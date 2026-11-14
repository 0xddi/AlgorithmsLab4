using System.Text.RegularExpressions;

namespace SortLab.Services;

/// <summary>Разбиение текста на слова, нормализация, подсчёт частот.</summary>
public static partial class TextProcessor
{
    [GeneratedRegex(@"[A-Za-zА-Яа-яЁё0-9']+")]
    private static partial Regex WordRegex();

    /// <summary>Разбить текст на слова; если ignoreCase — привести к нижнему регистру.</summary>
    public static string[] SplitWords(string text, bool ignoreCase)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        var matches = WordRegex().Matches(text);
        var words = new List<string>(matches.Count);
        foreach (Match m in matches)
        {
            var w = m.Value.Trim('\'');
            if (w.Length == 0) continue;
            words.Add(ignoreCase ? w.ToLowerInvariant() : w);
        }
        return words.ToArray();
    }

    /// <summary>Подсчитать частоты слов. Возвращает список, упорядоченный по частоте (затем по слову).</summary>
    public static List<Models.WordFrequency> CountFrequencies(string[] words)
    {
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in words)
        {
            dict[w] = dict.TryGetValue(w, out var c) ? c + 1 : 1;
        }
        return dict
            .Select(kv => new Models.WordFrequency { Word = kv.Key, Count = kv.Value })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Word, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

/// <summary>Генератор тестовых текстов из встроенного словаря английских слов.</summary>
public static class TextGenerator
{
    private static readonly string[] Dictionary =
    {
        "the","be","to","of","and","a","in","that","have","it","for","not","on","with","he","as","you","do","at",
        "this","but","his","by","from","they","we","say","her","she","or","an","will","my","one","all","would","there",
        "their","what","so","up","out","if","about","who","get","which","go","me","when","make","can","like","time",
        "no","just","him","know","take","person","into","year","your","good","some","could","them","see","other","than",
        "then","now","look","only","come","its","over","think","also","back","after","use","two","how","our","work",
        "first","well","way","even","new","want","because","any","these","give","day","most","us","apple","banana",
        "orange","grape","lemon","cherry","melon","berry","peach","plum","pear","mango","cat","dog","fish","bird",
        "horse","mouse","lion","tiger","bear","wolf","fox","rabbit","frog","snake","eagle","whale","shark","zebra",
        "red","blue","green","yellow","black","white","purple","brown","pink","gray","sun","moon","star","cloud",
        "rain","snow","wind","fire","water","earth","tree","flower","grass","leaf","stone","river","mountain","forest",
        "city","street","house","room","door","window","table","chair","book","pen","paper","phone","music","game",
        "school","teacher","student","friend","family","mother","father","brother","sister","money","food","bread",
        "milk","sugar","coffee","tea","water","juice","run","walk","jump","swim","read","write","sing","dance","play",
        "sleep","eat","drink","talk","listen","open","close","start","stop","fast","slow","big","small","tall","short",
        "happy","sad","kind","brave","calm","smart","quick","bright","dark","light","heavy","soft","hard","warm","cold",
    };

    private static readonly Random Rng = new();

    /// <summary>Сгенерировать текст из n слов (частые слова встречаются чаще — как в настоящем тексте).</summary>
    public static string GenerateText(int n)
    {
        var sb = new System.Text.StringBuilder(n * 8);
        for (int i = 0; i < n; i++)
        {
            // Квадрат случайного числа смещает выбор к началу словаря (частые слова).
            double r = Rng.NextDouble();
            int idx = (int)(r * r * Dictionary.Length);
            if (idx >= Dictionary.Length) idx = Dictionary.Length - 1;
            sb.Append(Dictionary[idx]);
            sb.Append(i % 17 == 16 ? ". " : " ");
        }
        return sb.ToString();
    }
}
