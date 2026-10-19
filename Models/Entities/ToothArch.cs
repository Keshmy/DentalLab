namespace DentalLab.Models.Entities
{
    /// <summary>مكان الأسنان على السطر: علوي / سفلي.</summary>
    public static class ToothArch
    {
        public const string Upper = "علوي";
        public const string Lower = "سفلي";
        public const string Both = "علوي وسفلي";

        public static readonly string[] All = [Upper, Lower, Both];
    }
}
