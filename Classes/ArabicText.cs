using System.Text;

namespace DentalLab.Classes
{
    /// <summary>
    /// مقارنة الأسماء العربية: توحيد الحروف المتشابهة (أ/إ/آ ← ا، ة ← ه، ى ← ي)، حذف التشكيل والتطويل،
    /// وتوحيد المسافات. تُستخدم في البحث وفي مطابقة الأسماء عند الاستيراد.
    /// </summary>
    public static class ArabicText
    {
        public static string Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return string.Empty;

            var sb = new StringBuilder(s.Length);
            var lastWasSpace = false;
            foreach (var ch in s.Trim().ToLowerInvariant())
            {
                if (ch is >= 'ً' and <= 'ْ' or 'ـ')
                    continue; // تشكيل وتطويل

                if (char.IsWhiteSpace(ch))
                {
                    if (!lastWasSpace) sb.Append(' ');
                    lastWasSpace = true;
                    continue;
                }

                lastWasSpace = false;
                sb.Append(ch switch
                {
                    'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                    'ة' => 'ه',
                    'ى' => 'ي',
                    _ => ch
                });
            }
            return sb.ToString();
        }

        /// <summary>اسم طبيب للمطابقة: بدون «د.» أو «دكتور» في أوله.</summary>
        public static string NormalizePersonName(string? s)
        {
            var n = Normalize(s);
            foreach (var prefix in new[] { "الدكتوره ", "الدكتور ", "دكتوره ", "دكتور ", "د. ", "د." , "د " })
            {
                if (n.StartsWith(prefix, StringComparison.Ordinal))
                    return n[prefix.Length..].Trim();
            }
            return n;
        }

        /// <summary>عدد التعديلات (حذف/إضافة/استبدال حرف) لتحويل نص إلى آخر.</summary>
        public static int EditDistance(string a, string b)
        {
            if (a.Length == 0) return b.Length;
            if (b.Length == 0) return a.Length;

            var prev = new int[b.Length + 1];
            var cur = new int[b.Length + 1];
            for (var j = 0; j <= b.Length; j++) prev[j] = j;

            for (var i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                }
                (prev, cur) = (cur, prev);
            }
            return prev[b.Length];
        }

        /// <summary>
        /// هل الاسمان متقاربان بما يكفي لاقتراح أحدهما بدل الآخر؟ (بعد التوحيد)
        /// فرق حرف أو حرفين، أو أحدهما جزء من الآخر (مثل «مركز الماسة» و«الماسة»).
        /// </summary>
        public static bool IsClose(string normalizedA, string normalizedB)
        {
            if (normalizedA.Length == 0 || normalizedB.Length == 0)
                return false;
            if (normalizedA == normalizedB)
                return true;

            var shorter = Math.Min(normalizedA.Length, normalizedB.Length);
            if (shorter >= 4 && (normalizedA.Contains(normalizedB) || normalizedB.Contains(normalizedA)))
                return true;

            var allowed = shorter <= 5 ? 1 : 2;
            return EditDistance(normalizedA, normalizedB) <= allowed;
        }
    }
}
