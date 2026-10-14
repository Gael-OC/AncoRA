using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// What the session log on the phone keeps and how each line reads. Xiaomi's logcat buffer is 2 MB and MIUI prunes the
    /// chattiest app first, so a field test left no tour lines in logcat: the tour writes its own file instead
    /// (PaseoSessionLog). It keeps the tour's lines, Immersal's, and any warning or error; one line each.
    /// </summary>
    public static class PaseoLogFormat
    {
        const string ImmersalMark = "com.immersal.core";

        public static bool Keep(string message, LogType type)
        {
            if (string.IsNullOrEmpty(message))
                return false;
            return type != LogType.Log || message.StartsWith("[AncoRA", StringComparison.Ordinal) || message.Contains(ImmersalMark);
        }

        /// <summary>Local time, a level letter and the message's first line; errors add their first stack frame.</summary>
        public static string Line(DateTime time, LogType type, string message, string stackTrace)
        {
            string text = FirstLine(message);
            // Immersal prefixes the caller's compile-time path: "[.\Library\PackageCache\com.immersal.core@…\ImmersalSDK] …".
            if (text.StartsWith("[", StringComparison.Ordinal) && text.Contains(ImmersalMark))
            {
                int end = text.IndexOf(']');
                if (end > 0)
                {
                    string caller = text.Substring(1, end - 1);
                    caller = caller.Substring(caller.LastIndexOfAny(new[] { '\\', '/' }) + 1);
                    text = $"[Immersal {caller}]{text.Substring(end + 1)}";
                }
            }
            string frame = type is LogType.Error or LogType.Exception or LogType.Assert ? FirstLine(stackTrace) : "";
            return $"{time:HH:mm:ss.fff} {Level(type)} {text}{(frame.Length > 0 ? " @ " + frame : "")}";
        }

        /// <summary>The session files to delete so only the newest <paramref name="keep"/> remain (names sort by start time).</summary>
        public static List<string> FilesToDelete(IEnumerable<string> fileNames, int keep)
        {
            var sorted = fileNames.OrderBy(n => n, StringComparer.Ordinal).ToList();
            return sorted.Take(Math.Max(0, sorted.Count - keep)).ToList();
        }

        static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            int end = text.IndexOfAny(new[] { '\r', '\n' });
            return end < 0 ? text : text.Substring(0, end);
        }

        static char Level(LogType type) => type switch
        {
            LogType.Warning => 'W',
            LogType.Error => 'E',
            LogType.Exception => 'X',
            LogType.Assert => 'A',
            _ => 'I'
        };
    }
}
