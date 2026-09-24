using System;

namespace _1RM.Service
{
    // Independent of the classic palette and its saved view selection.
    public sealed class FluentPreferences
    {
        public bool Enabled { get; set; }
        public string Theme { get; set; } = "System";
        public string View { get; set; } = "List";

        public string ResolveView(string classicView) => Enabled ? NormalizeView(View) : classicView;

        public void RememberView(string view, Action<string> saveClassic)
        {
            if (Enabled) View = NormalizeView(view);
            else saveClassic(view);
        }

        public static string NormalizeTheme(string? theme) =>
            theme is "Light" or "Dark" ? theme : "System";

        public static bool UseDark(string? theme, bool systemUsesLight) =>
            NormalizeTheme(theme) == "Dark" || (NormalizeTheme(theme) == "System" && !systemUsesLight);

        public static string NormalizeView(string? view) =>
            view is "Card" or "Tree" ? view : "List";
    }
}
