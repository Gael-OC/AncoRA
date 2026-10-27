using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// Housekeeping of the recordings (PaseoRecorder) on the phone: how much they weigh, copying them to Download/AncoRA/
    /// grabaciones (MediaStore, no permission needed, folder per session, so Android's Files app, Drive or a USB cable can
    /// take them) and deleting them. «Compartir registro» does not carry them: a recording is hundreds of MB.
    /// </summary>
    public static class PaseoRecordings
    {
        const string Tag = "[AncoRA Paseo]";
        /// <summary>«name:bytes» of what was already copied to Descargas, one per line, in each session folder: a second copy skips them (the metadata grows, so it is copied again).</summary>
        public const string CopiedFile = "copiado.txt";

        public static IReadOnlyList<string> Sessions(string root) =>
            Directory.Exists(root) ? Directory.GetDirectories(root).OrderBy(d => d, StringComparer.Ordinal).ToList() : new List<string>();

        public static long TotalBytes(string root) =>
            Sessions(root).SelectMany(d => Directory.GetFiles(d)).Sum(f => new FileInfo(f).Length);

        public static string CopyKey(string file) => $"{Path.GetFileName(file)}:{new FileInfo(file).Length}";

        /// <summary>The files of a session to copy: images and the metadata, not the list of what was copied.</summary>
        public static IEnumerable<string> FilesToCopy(string session, ISet<string> alreadyCopied) =>
            Directory.GetFiles(session).Where(f => Path.GetFileName(f) != CopiedFile && !alreadyCopied.Contains(CopyKey(f)));

        /// <summary>Deletes every recording on the phone (the copies in Descargas stay). Returns the bytes freed.</summary>
        public static long DeleteAll(string root)
        {
            long bytes = TotalBytes(root);
            foreach (string session in Sessions(root))
                Directory.Delete(session, true);
            return bytes;
        }

        public static string SizeText(long bytes) => bytes >= 1024L * 1024 * 1024 ? (bytes / (1024.0 * 1024 * 1024)).ToString("F1", CultureInfo.InvariantCulture) + " GB" : $"{bytes / (1024 * 1024)} MB";

        /// <summary>Copies what is not yet in Descargas/AncoRA/grabaciones/&lt;session&gt;. Returns what to tell the team.</summary>
        public static async Task<string> CopyToDownloadsAsync(string root, Action<string> progress)
        {
            var sessions = Sessions(root);
            if (sessions.Count == 0)
                return "No hay grabaciones en este teléfono.";
#if UNITY_ANDROID && !UNITY_EDITOR
            int copied = 0, failed = 0;
            long bytes = 0;
            foreach (string session in sessions)
            {
                string stamp = Path.GetFileName(session);
                string listPath = Path.Combine(session, CopiedFile);
                var done = new HashSet<string>(File.Exists(listPath) ? File.ReadAllLines(listPath) : Array.Empty<string>());
                foreach (string file in FilesToCopy(session, done).ToList())
                {
                    try
                    {
                        AndroidDownloads.Copy(file, $"Download/AncoRA/grabaciones/{stamp}");
                        File.AppendAllText(listPath, CopyKey(file) + "\n");
                        copied++;
                        bytes += new FileInfo(file).Length;
                    }
                    catch (Exception e)
                    {
                        failed++;
                        Debug.LogWarning($"{Tag} No se pudo copiar {file} a Descargas: {e.Message}");
                        if (failed >= 3)
                            return $"Se detuvo la copia ({copied} archivos copiados): {e.Message}";
                    }
                    progress?.Invoke($"Copiando a Descargas… {copied} archivos ({SizeText(bytes)})");
                    // A frame between files: the tour keeps running while the copy goes.
                    await Task.Yield();
                }
            }
            Debug.Log($"{Tag} Grabaciones copiadas a Descargas/AncoRA/grabaciones: {copied} archivos, {SizeText(bytes)}, {failed} fallos.");
            return copied == 0 && failed == 0 ? "Todo estaba copiado ya en Descargas/AncoRA/grabaciones."
                : $"Copiados {copied} archivos ({SizeText(bytes)}) a Descargas/AncoRA/grabaciones.{(failed > 0 ? $" Fallaron {failed}." : "")}";
#else
            await Task.Yield();
            return $"La copia a Descargas solo funciona en el teléfono; las grabaciones están en {root}.";
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static class AndroidDownloads
        {
            // MediaStore.Downloads (API 29+), as PaseoShare: the app adds files of its own without a storage permission.
            public static void Copy(string path, string relativeDirectory)
            {
                string name = Path.GetFileName(path);
                // MediaStore appends the extension of the mime type when the name lacks it; octet-stream leaves the name alone.
                string mime = name.EndsWith(".png", StringComparison.Ordinal) ? "image/png" : "application/octet-stream";
                // Unity owns currentActivity: it must not be disposed.
                var activity = UnityEngine.Android.AndroidApplication.currentActivity;
                using var resolver = activity.Call<AndroidJavaObject>("getContentResolver");
                using var values = new AndroidJavaObject("android.content.ContentValues");
                values.Call("put", "_display_name", name);
                values.Call("put", "mime_type", mime);
                values.Call("put", "relative_path", relativeDirectory);
                using var downloads = new AndroidJavaClass("android.provider.MediaStore$Downloads");
                using var collection = downloads.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");
                using var uri = resolver.Call<AndroidJavaObject>("insert", collection, values)
                                ?? throw new IOException("MediaStore no creó el archivo en Descargas.");
                using var output = resolver.Call<AndroidJavaObject>("openOutputStream", uri);
                using var input = File.OpenRead(path);
                var chunk = new byte[256 * 1024];
                var signed = new sbyte[chunk.Length];
                int read;
                while ((read = input.Read(chunk, 0, chunk.Length)) > 0)
                {
                    Buffer.BlockCopy(chunk, 0, signed, 0, read);
                    output.Call("write", signed, 0, read);
                }
                output.Call("close");
            }
        }
#endif
    }
}
