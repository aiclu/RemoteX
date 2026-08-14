using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace _1RM.Utils
{
    /// <summary>
    /// Lists the code pages offered to the user for terminal byte-stream decoding
    /// (used by SSH/Telnet/Serial). Mirrors the encoding choices previously offered
    /// by the PuTTY runner; code pages map directly to <c>Encoding.GetEncoding(int)</c>.
    /// </summary>
    public static class EnumEncodingHelper
    {
        /// <summary>
        /// Common code pages worth exposing: UTF-8, Latin-1, Western Europe,
        /// a few CJK code pages, and Unicode (as supported by the OS).
        /// Key = code page id, Value = friendly display name.
        /// </summary>
        public static Dictionary<int, string> SupportedCodePages
        {
            get
            {
                int[] ids =
                [
                    65001, // UTF-8
                    1200,  // UTF-16 LE
                    20127, // US-ASCII
                    28591, // ISO-8859-1 (Latin-1)
                    28592, // ISO-8859-2 (Central Europe)
                    28597, // ISO-8859-7 (Greek)
                    28599, // ISO-8859-15 (Latin-9)
                    936,   // GBK (Simplified Chinese)
                    950,   // Big5 (Traditional Chinese)
                    932,   // Shift-JIS (Japanese)
                    949,   // EUC-KR (Korean)
                    1250,  // Windows-1250 (Central Europe)
                    1251,  // Windows-1251 (Cyrillic)
                    1252,  // Windows-1252 (Western Europe)
                    1253,  // Windows-1253 (Greek)
                    1254,  // Windows-1254 (Turkish)
                    1256,  // Windows-1256 (Arabic)
                    1257,  // Windows-1257 (Baltic)
                    1258,  // Windows-1258 (Vietnamese)
                ];
                var dict = new Dictionary<int, string>();
                foreach (var id in ids)
                    dict[id] = GetDisplayName(id);
                return dict;
            }
        }

        /// <summary>Friendly display name for a code page, falling back to "cpNNN".</summary>
        public static string GetDisplayName(int codePage)
        {
            try
            {
                var enc = Encoding.GetEncoding(codePage);
                var name = enc.WebName;
                var pretty = name switch
                {
                    "utf-8" => "UTF-8",
                    "utf-16" => "Unicode (UTF-16 LE)",
                    "us-ascii" => "US-ASCII",
                    "iso-8859-1" => "ISO-8859-1 (Latin-1)",
                    "iso-8859-2" => "ISO-8859-2 (Central Europe)",
                    "iso-8859-7" => "ISO-8859-7 (Greek)",
                    "iso-8859-15" => "ISO-8859-15 (Latin-9)",
                    "gb2312" => "GBK (Simplified Chinese)",
                    "big5" => "Big5 (Traditional Chinese)",
                    "shift_jis" => "Shift-JIS (Japanese)",
                    "ks_c_5601-1987" => "EUC-KR (Korean)",
                    "windows-1250" => "Windows-1250 (Central Europe)",
                    "windows-1251" => "Windows-1251 (Cyrillic)",
                    "windows-1252" => "Windows-1252 (Western Europe)",
                    "windows-1253" => "Windows-1253 (Greek)",
                    "windows-1254" => "Windows-1254 (Turkish)",
                    "windows-1256" => "Windows-1256 (Arabic)",
                    "windows-1257" => "Windows-1257 (Baltic)",
                    "windows-1258" => "Windows-1258 (Vietnamese)",
                    _ => name,
                };
                return $"{pretty} ({codePage})";
            }
            catch (Exception)
            {
                return $"cp{codePage}";
            }
        }
    }
}
