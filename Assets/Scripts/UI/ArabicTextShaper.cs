using System;
using System.Collections.Generic;
using System.Text;

namespace Nageel.UI
{
    /// <summary>
    /// Robust Arabic Unicode Text Shaper and Right-to-Left bidirectional layout converter.
    /// Converts isolated Arabic characters into connected cursive forms (Initial, Medial, Final, Isolated)
    /// so Arabic displays correctly in Unity UI and TextMeshPro without letter reversal.
    /// </summary>
    public static class ArabicTextShaper
    {
        private struct ArabicGlyph
        {
            public char Isolated;
            public char Initial;
            public char Medial;
            public char Final;

            public ArabicGlyph(char isolated, char initial, char medial, char final)
            {
                Isolated = isolated;
                Initial = initial;
                Medial = medial;
                Final = final;
            }
        }

        private static readonly Dictionary<char, ArabicGlyph> glyphMap = new Dictionary<char, ArabicGlyph>()
        {
            // Hamza & Alef forms
            { '\u0621', new ArabicGlyph('\uFE80', '\uFE80', '\uFE80', '\uFE80') }, // ء
            { '\u0622', new ArabicGlyph('\uFE81', '\uFE81', '\uFE82', '\uFE82') }, // آ
            { '\u0623', new ArabicGlyph('\uFE83', '\uFE83', '\uFE84', '\uFE84') }, // أ
            { '\u0624', new ArabicGlyph('\uFE85', '\uFE85', '\uFE86', '\uFE86') }, // ؤ
            { '\u0625', new ArabicGlyph('\uFE87', '\uFE87', '\uFE88', '\uFE88') }, // إ
            { '\u0626', new ArabicGlyph('\uFE89', '\uFE8B', '\uFE8C', '\uFE8A') }, // ئ
            { '\u0627', new ArabicGlyph('\uFE8D', '\uFE8D', '\uFE8E', '\uFE8E') }, // ا
            // Baa, Taa, Thaa
            { '\u0628', new ArabicGlyph('\uFE8F', '\uFE91', '\uFE92', '\uFE90') }, // ب
            { '\u0629', new ArabicGlyph('\uFE93', '\uFE93', '\uFE94', '\uFE94') }, // ة
            { '\u062A', new ArabicGlyph('\uFE95', '\uFE97', '\uFE98', '\uFE96') }, // ت
            { '\u062B', new ArabicGlyph('\uFE99', '\uFE9B', '\uFE9C', '\uFE9A') }, // ث
            // Jeem, Haa, Khaa
            { '\u062C', new ArabicGlyph('\uFE9D', '\uFE9F', '\uFEA0', '\uFE9E') }, // ج
            { '\u062D', new ArabicGlyph('\uFEA1', '\uFEA3', '\uFEA4', '\uFEA2') }, // ح
            { '\u062E', new ArabicGlyph('\uFEA5', '\uFEA7', '\uFEA8', '\uFEA6') }, // خ
            // Dal, Thal, Raa, Zaay
            { '\u062F', new ArabicGlyph('\uFEA9', '\uFEA9', '\uFEAA', '\uFEAA') }, // د
            { '\u0630', new ArabicGlyph('\uFEAB', '\uFEAB', '\uFEAC', '\uFEAC') }, // ذ
            { '\u0631', new ArabicGlyph('\uFEAD', '\uFEAD', '\uFEAE', '\uFEAE') }, // ر
            { '\u0632', new ArabicGlyph('\uFEAF', '\uFEAF', '\uFEB0', '\uFEB0') }, // ز
            // Seen, Sheen, Saad, Daad
            { '\u0633', new ArabicGlyph('\uFEB1', '\uFEB3', '\uFEB4', '\uFEB2') }, // س
            { '\u0634', new ArabicGlyph('\uFEB5', '\uFEB7', '\uFEB8', '\uFEB6') }, // ش
            { '\u0635', new ArabicGlyph('\uFEB9', '\uFEBB', '\uFEBC', '\uFEBA') }, // ص
            { '\u0636', new ArabicGlyph('\uFEBD', '\uFEBF', '\uFEC0', '\uFEBE') }, // ض
            // Taa, Zaa, Ayn, Ghayn
            { '\u0637', new ArabicGlyph('\uFEC1', '\uFEC3', '\uFEC4', '\uFEC2') }, // ط
            { '\u0638', new ArabicGlyph('\uFEC5', '\uFEC7', '\uFEC8', '\uFEC6') }, // ظ
            { '\u0639', new ArabicGlyph('\uFEC9', '\uFECB', '\uFECC', '\uFECA') }, // ع
            { '\u063A', new ArabicGlyph('\uFECD', '\uFECF', '\uFED0', '\uFECE') }, // غ
            // Faa, Qaaf, Kaaf, Laam, Meem, Noon, Haa, Waaw, Yaa
            { '\u0641', new ArabicGlyph('\uFED1', '\uFED3', '\uFED4', '\uFED2') }, // ف
            { '\u0642', new ArabicGlyph('\uFED5', '\uFED7', '\uFED8', '\uFED6') }, // ق
            { '\u0643', new ArabicGlyph('\uFED9', '\uFEDB', '\uFEDC', '\uFEDA') }, // ك
            { '\u0644', new ArabicGlyph('\uFEDD', '\uFEDF', '\uFEE0', '\uFEDE') }, // ل
            { '\u0645', new ArabicGlyph('\uFEE1', '\uFEE3', '\uFEE4', '\uFEE2') }, // م
            { '\u0646', new ArabicGlyph('\uFEE5', '\uFEE7', '\uFEE8', '\uFEE6') }, // ن
            { '\u0647', new ArabicGlyph('\uFEE9', '\uFEEB', '\uFEEC', '\uFEEA') }, // ه
            { '\u0648', new ArabicGlyph('\uFEED', '\uFEED', '\uFEEE', '\uFEEE') }, // و
            { '\u0649', new ArabicGlyph('\uFEEF', '\uFEEF', '\uFEF0', '\uFEF0') }, // ى
            { '\u064A', new ArabicGlyph('\uFEF1', '\uFEF3', '\uFEF4', '\uFEF2') }, // ي
            // Tatweel
            { '\u0640', new ArabicGlyph('\u0640', '\u0640', '\u0640', '\u0640') }
        };

        // Letters that cannot connect to the following letter (only connect to the preceding letter)
        private static readonly HashSet<char> nonForwardConnecting = new HashSet<char>()
        {
            '\u0622', '\u0623', '\u0624', '\u0625', '\u0627', // Alef forms
            '\u062F', '\u0630', // Dal, Thal
            '\u0631', '\u0632', // Raa, Zaay
            '\u0648', '\u0629', '\u0649' // Waaw, Taa Marbuta, Alef Maqsura
        };

        public static string Shape(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            // Handle special Lam-Alef ligatures first
            string text = ProcessLamAlef(input);

            StringBuilder shaped = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];
                if (!glyphMap.ContainsKey(current))
                {
                    shaped.Append(current);
                    continue;
                }

                bool canConnectBehind = (i > 0) && CanConnectForward(text[i - 1]) && glyphMap.ContainsKey(text[i - 1]);
                bool canConnectAhead = (i < text.Length - 1) && CanConnectBackward(text[i + 1]) && glyphMap.ContainsKey(text[i + 1]);

                ArabicGlyph g = glyphMap[current];
                if (canConnectBehind && canConnectAhead)
                {
                    shaped.Append(g.Medial);
                }
                else if (canConnectBehind)
                {
                    shaped.Append(g.Final);
                }
                else if (canConnectAhead)
                {
                    shaped.Append(g.Initial);
                }
                else
                {
                    shaped.Append(g.Isolated);
                }
            }

            // Reverse for RTL display in Unity left-to-right text renderers
            return ReverseRTL(shaped.ToString());
        }

        private static bool CanConnectForward(char c)
        {
            return glyphMap.ContainsKey(c) && !nonForwardConnecting.Contains(c);
        }

        private static bool CanConnectBackward(char c)
        {
            return glyphMap.ContainsKey(c);
        }

        private static string ProcessLamAlef(string input)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '\u0644' && i < input.Length - 1) // Lam
                {
                    char next = input[i + 1];
                    char ligature = '\0';
                    if (next == '\u0622') ligature = '\uFEF5'; // Lam-Alef with Madda
                    else if (next == '\u0623') ligature = '\uFEF7'; // Lam-Alef with Hamza Above
                    else if (next == '\u0625') ligature = '\uFEF9'; // Lam-Alef with Hamza Below
                    else if (next == '\u0627') ligature = '\uFEFB'; // Lam-Alef plain

                    if (ligature != '\0')
                    {
                        sb.Append(ligature);
                        i++; // Skip next alef
                        continue;
                    }
                }
                sb.Append(input[i]);
            }
            return sb.ToString();
        }

        private static string ReverseRTL(string shaped)
        {
            char[] chars = shaped.ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }
    }
}
