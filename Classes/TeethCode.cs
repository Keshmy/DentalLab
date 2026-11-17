using System.Text.RegularExpressions;

namespace DentalLab.Classes
{
    /// <summary>
    /// الأسنان المختارة لسطر الحالة بترقيم FDI (الدولي):
    ///   الدائمة: 11–18، 21–28، 31–38، 41–48   (الربع + رقم السن 1–8)
    ///   اللبنية: 51–55، 61–65، 71–75، 81–85   (A–E تُعرض بدل 1–5)
    ///   الأرباع: 1 علوي يمين، 2 علوي يسار، 3 سفلي يسار، 4 سفلي يمين (5–8 نفسها للّبنية).
    ///
    /// الصيغة المخزّنة نصية وبسيطة: أسنان مفردة تفصلها فاصلة، والجسر أسنانه بينها «-»
    /// والسن الوهمي (دمية) يُلحق به حرف p. مثال: "13-14p-15,21,36".
    /// </summary>
    public static class TeethCode
    {
        // ترتيب الأسنان على كل فك من يمين المريض إلى يساره — يُستخدم للتحقق من تجاور أسنان الجسر.
        private static readonly int[] UpperPermanent = [18, 17, 16, 15, 14, 13, 12, 11, 21, 22, 23, 24, 25, 26, 27, 28];
        private static readonly int[] LowerPermanent = [48, 47, 46, 45, 44, 43, 42, 41, 31, 32, 33, 34, 35, 36, 37, 38];
        private static readonly int[] UpperPrimary = [55, 54, 53, 52, 51, 61, 62, 63, 64, 65];
        private static readonly int[] LowerPrimary = [85, 84, 83, 82, 81, 71, 72, 73, 74, 75];
        private static readonly int[][] Arches = [UpperPermanent, LowerPermanent, UpperPrimary, LowerPrimary];

        public static bool IsValidTooth(int fdi) => Arches.Any(a => a.Contains(fdi));

        public static bool IsPrimary(int fdi) => fdi / 10 >= 5;

        /// <summary>الربع 1–4 (اللبنية تُحوَّل إلى ربعها المقابل).</summary>
        public static int Quadrant(int fdi) => IsPrimary(fdi) ? fdi / 10 - 4 : fdi / 10;

        /// <summary>رقم السن كما يُكتب في صليب Palmer: 1–8 للدائمة، A–E للّبنية.</summary>
        public static string PalmerLabel(int fdi) => IsPrimary(fdi) ? ((char)('A' + fdi % 10 - 1)).ToString() : (fdi % 10).ToString();

        private static (int Arch, int Pos) Locate(int fdi)
        {
            for (var a = 0; a < Arches.Length; a++)
            {
                var i = Array.IndexOf(Arches[a], fdi);
                if (i >= 0) return (a, i);
            }
            return (-1, -1);
        }

        /// <summary>
        /// يقرأ النص ويتحقق منه. يقبل أيضاً الفاصلة العربية والمسافات، والدمية بالحرف p أو بين قوسين: 13-(14)-15.
        /// </summary>
        public static TeethSelection Parse(string? text)
        {
            var selection = new TeethSelection();
            if (string.IsNullOrWhiteSpace(text))
                return selection;

            var normalized = ToLatinDigits(text).Replace('،', ',').Replace(';', ',').Replace('–', '-').Replace('—', '-');
            // "13 - 14p - 15" ← "13-14p-15"
            normalized = Regex.Replace(normalized, @"\s*-\s*", "-");
            var tokens = Regex.Split(normalized, @"[,\s]+").Where(t => t.Length > 0);

            var seen = new HashSet<int>();
            foreach (var token in tokens)
            {
                var group = new TeethGroup();
                foreach (var part in token.Split('-', StringSplitOptions.RemoveEmptyEntries))
                {
                    var m = Regex.Match(part, @"^\(?(\d{2})\)?([pP])?$");
                    if (!m.Success)
                    {
                        selection.Errors.Add($"«{part}» ليس رقم سن صحيحاً (مثال: 14 أو 14p للدمية).");
                        continue;
                    }

                    var fdi = int.Parse(m.Groups[1].Value);
                    var pontic = m.Groups[2].Success || part.StartsWith('(');
                    if (!IsValidTooth(fdi))
                    {
                        selection.Errors.Add($"{fdi} ليس رقم سن في الترقيم الدولي (11–48 للدائمة، 51–85 للّبنية).");
                        continue;
                    }
                    if (!seen.Add(fdi))
                    {
                        selection.Errors.Add($"السن {fdi} مكرر.");
                        continue;
                    }
                    group.Teeth.Add(new ToothRef(fdi, pontic));
                }

                if (group.Teeth.Count == 0)
                    continue;

                if (group.Teeth.Count == 1)
                {
                    if (group.Teeth[0].Pontic)
                        selection.Errors.Add($"السن {group.Teeth[0].Fdi} مُعلَّم دمية لكنه ليس ضمن جسر.");
                    group.Teeth[0] = group.Teeth[0] with { Pontic = false };
                }
                else
                {
                    ValidateBridge(group, selection.Errors);
                }
                selection.Groups.Add(group);
            }

            selection.Groups.Sort((a, b) => Locate(a.Teeth[0].Fdi).CompareTo(Locate(b.Teeth[0].Fdi)));
            return selection;
        }

        private static void ValidateBridge(TeethGroup group, List<string> errors)
        {
            var located = group.Teeth.Select(t => (t, loc: Locate(t.Fdi))).OrderBy(x => x.loc.Pos).ToList();
            var label = string.Join("-", group.Teeth.Select(t => t.Fdi));

            if (located.Select(x => x.loc.Arch).Distinct().Count() > 1)
            {
                errors.Add($"الجسر {label} يجب أن يكون في فك واحد (علوي أو سفلي) ومن نوع أسنان واحد.");
                return;
            }
            for (var i = 1; i < located.Count; i++)
            {
                if (located[i].loc.Pos != located[i - 1].loc.Pos + 1)
                {
                    errors.Add($"أسنان الجسر {label} يجب أن تكون متجاورة بدون فراغ — أضف الأسنان الناقصة كدمية.");
                    return;
                }
            }

            group.Teeth.Clear();
            group.Teeth.AddRange(located.Select(x => x.t));
            if (group.Teeth[0].Pontic || group.Teeth[^1].Pontic)
                errors.Add($"طرفا الجسر {label} يجب أن يكونا سنّين حقيقيين (دعامتين) وليس دمية.");
            if (group.Teeth.All(t => t.Pontic))
                errors.Add($"الجسر {label} كله دمية.");
        }

        /// <summary>الصيغة المخزّنة الموحّدة، مثل "13-14p-15,21".</summary>
        public static string Serialize(TeethSelection selection) =>
            string.Join(",", selection.Groups.Select(g => string.Join("-", g.Teeth.Select(t => t.Fdi + (t.Pontic ? "p" : "")))));

        /// <summary>نص مقروء للبحث والقوائم والتصدير: "13-(14)-15، 21".</summary>
        public static string ToDisplay(TeethSelection selection) =>
            string.Join("، ", selection.Groups.Select(g => string.Join("-", g.Teeth.Select(t => t.Pontic ? $"({t.Fdi})" : t.Fdi.ToString()))));

        public static string? ToDisplay(string? stored)
        {
            var s = Parse(stored);
            return s.Count == 0 ? null : ToDisplay(s);
        }

        private static string ToLatinDigits(string s)
        {
            var chars = s.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (chars[i] is >= '٠' and <= '٩') chars[i] = (char)('0' + (chars[i] - '٠'));
                else if (chars[i] is >= '۰' and <= '۹') chars[i] = (char)('0' + (chars[i] - '۰'));
            }
            return new string(chars);
        }
    }

    public record ToothRef(int Fdi, bool Pontic);

    public class TeethGroup
    {
        public List<ToothRef> Teeth { get; } = [];
        public bool IsBridge => Teeth.Count > 1;
    }

    public class TeethSelection
    {
        public List<TeethGroup> Groups { get; } = [];
        public List<string> Errors { get; } = [];

        public bool IsValid => Errors.Count == 0;

        /// <summary>عدد الوحدات = كل الأسنان بما فيها الدمية (الجسر من 3 أسنان = 3 وحدات).</summary>
        public int Count => Groups.Sum(g => g.Teeth.Count);

        public IEnumerable<ToothRef> AllTeeth => Groups.SelectMany(g => g.Teeth);

        public bool IsInBridge(int fdi) => Groups.Any(g => g.IsBridge && g.Teeth.Any(t => t.Fdi == fdi));
    }
}
