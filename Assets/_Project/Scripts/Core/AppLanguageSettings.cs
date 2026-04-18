using System;
using TMPro;
using UnityEngine;

public enum AppLanguage
{
    English = 0,
    Arabic = 1
}

public static class AppLanguageSettings
{
    public static event Action<AppLanguage> LanguageChanged;

    public static AppLanguage CurrentLanguage => AppLanguage.English;

    public static bool IsArabic => false;

    public static void ToggleLanguage() { }

    public static void SetLanguage(AppLanguage language) { }

    public static string Localize(string english, string arabic) => english;

    public static void ApplyText(
        TMP_Text text,
        string english,
        string arabic,
        TextAlignmentOptions englishAlignment = TextAlignmentOptions.Center,
        TextAlignmentOptions arabicAlignment = TextAlignmentOptions.Center)
    {
        if (text == null)
            return;

        text.isRightToLeftText = false;
        text.alignment = englishAlignment;
        text.text = english;
    }

    public static string ShapeArabic(string input) => input ?? string.Empty;
}
