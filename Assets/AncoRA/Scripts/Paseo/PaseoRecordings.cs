using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

        public static string CopyKey(string file)
        {
            string key = $"{Path.GetFileName(file)}:{new FileInfo(file).Length}";
            if (file.EndsWith(".png", StringComparison.Ordinal) || file.EndsWith(".mp4", StringComparison.Ordinal)) return key;
            using var sha = SHA256.Create();
            using var input = File.OpenRead(file);
            return key + ":" + BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
        }

        /// <summary>The files of a session to copy: images and the metadata, not the list of what was copied.</summary>
        public static IEnumerable<string> FilesToCopy(string session, ISet<string> alreadyCopied) =>
            Directory.GetFiles(session).Where(f => Path.GetFileName(f) != CopiedFile && !f.EndsWith(".tmp", StringComparison.Ordinal) && !alreadyCopied.Contains(CopyKey(f)));

        /// <summary>Logs travel with the same export as the captures, including logs from previous app launches.</summary>
        public static Task AttachLogsAsync(string root, string logs, string stamp, string phone) => Task.Run(() =>
        {
            if (string.IsNullOrEmpty(root)) throw new IOException("No se encontró la carpeta de grabaciones.");
            string folder = Path.Combine(root, "registros-" + stamp);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "telefono.txt"), phone);
            File.WriteAllText(Path.Combine(folder, "LEEME.txt"),
                "Paquete de AncoRA. Envía la carpeta grabaciones completa, conservando todas las subcarpetas.\n" +
                "Cada toma: sesion.json (rol, luz, notas y estado), intentos.jsonl, PNG originales, registro.log y sesion-arcore.mp4 si se grabó video.\n" +
                "Los videos ARCore contienen cámara y sensores, no la pantalla ni cajas. Conservar el MP4 ORIGINAL sin editar/recomprimir.\n" +
                "Consulta, reserva y negativa no se incorporan al mapa. La prueba en casa solo comprueba captura/exportación/reproducción.\n" +
                "sha256.txt permite comprobar la integridad al recibir los archivos. Copiar no borra los originales del teléfono.\n");
            if (Directory.Exists(logs))
                foreach (string log in Directory.GetFiles(logs, "paseo-*.log"))
                {
                    using var input = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var output = File.Create(Path.Combine(folder, Path.GetFileName(log)));
                    input.CopyTo(output);
                }
        });

        public static void WriteChecksums(string session)
        {
            var text = new StringBuilder();
            using var sha = SHA256.Create();
            foreach (string file in Directory.GetFiles(session).OrderBy(Path.GetFileName, StringComparer.Ordinal))
            {
                string name = Path.GetFileName(file);
                if (name == CopiedFile || name == "sha256.txt" || name.EndsWith(".tmp", StringComparison.Ordinal)) continue;
                using var input = File.OpenRead(file);
                text.Append(BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant()).Append("  ").Append(name).Append('\n');
            }
            File.WriteAllText(Path.Combine(session, "sha256.txt"), text.ToString());
        }

        /// <summary>Deletes every recording on the phone (the copies in Descargas stay). Returns the bytes freed.</summary>
        public static long DeleteAll(string root)
        {
            long bytes = TotalBytes(root);
            foreach (string session in Sessions(root))
                Directory.Delete(session, true);
            return bytes;
        }

        public static string SizeText(long bytes) => bytes >= 1024L * 1024 * 1024 ? (bytes / (1024.0 * 1024 * 1024)).ToString("F1", CultureInfo.InvariantCulture) + " GB" : $"{bytes / (1024 * 1024)} MB";

        /// <summary>
        /// Compares each folder with its manifest.json off the main thread (it hashes every image). A folder in
        /// <paramref name="notCopied"/> counts as incomplete whatever its manifest says.
        /// </summary>
        public static Task<PaseoCopyReport> CompareWithManifestsAsync(IEnumerable<string> folders, ISet<string> notCopied = null)
        {
            var list = folders.ToList();
            return Task.Run(() => PaseoRecordManifest.CompareFolders(list, notCopied));
        }

        /// <summary>Copies what is not yet in Descargas/AncoRA/grabaciones/&lt;session&gt;, then checks every folder against its manifest. Returns what to tell the team.</summary>
        public static async Task<string> CopyToDownloadsAsync(string root, Action<string> progress)
        {
            var sessions = Sessions(root);
            if (sessions.Count == 0)
                return "No hay grabaciones en este teléfono.";
#if UNITY_ANDROID && !UNITY_EDITOR
            int copied = 0, failed = 0;
            long bytes = 0;
            bool stopped = false;
            string stopMessage = "";
            var notCopied = new HashSet<string>(sessions);
            foreach (string session in sessions)
            {
                if (stopped) break;
                bool sessionFailed = false;
                progress?.Invoke("Comprobando integridad de " + Path.GetFileName(session) + "…");
                await Task.Run(() => WriteChecksums(session));
                string stamp = Path.GetFileName(session);
                string listPath = Path.Combine(session, CopiedFile);
                var done = new HashSet<string>(File.Exists(listPath) ? File.ReadAllLines(listPath) : Array.Empty<string>());
                foreach (string file in FilesToCopy(session, new HashSet<string>()).ToList())
                {
                    try
                    {
                        bool wrote = await Task.Run(() =>
                        {
                            AndroidJNI.AttachCurrentThread();
                            try
                            {
                                string relative = $"Download/AncoRA/grabaciones/{stamp}";
                                if (done.Contains(CopyKey(file)) && AndroidDownloads.Exists(file, relative)) return false;
                                AndroidDownloads.Copy(file, relative);
                                return true;
                            }
                            finally { AndroidJNI.DetachCurrentThread(); }
                        });
                        if (!wrote) continue;
                        File.AppendAllText(listPath, CopyKey(file) + "\n");
                        copied++;
                        bytes += new FileInfo(file).Length;
                    }
                    catch (Exception e)
                    {
                        failed++;
                        sessionFailed = true;
                        Debug.LogWarning($"{Tag} No se pudo copiar {file} a Descargas: {e.Message}");
                        if (failed >= 3)
                        {
                            stopped = true;
                            stopMessage = $"Copia INCOMPLETA: se detuvo la copia ({copied} archivos copiados): {e.Message}";
                            break;
                        }
                    }
                    progress?.Invoke($"Copiando a Descargas… {copied} archivos ({SizeText(bytes)})");
                    // A frame between files: the tour keeps running while the copy goes.
                    await Task.Yield();
                }
                if (!sessionFailed && !stopped)
                    notCopied.Remove(session);
            }
            Debug.Log($"{Tag} Grabaciones copiadas a Descargas/AncoRA/grabaciones: {copied} archivos, {SizeText(bytes)}, {failed} fallos.");
            progress?.Invoke("Comprobando las carpetas con sus manifiestos…");
            var report = await CompareWithManifestsAsync(sessions, notCopied);
            Debug.Log($"{Tag} {report.LogLine()}");
            string head = stopped ? stopMessage
                : copied == 0 && failed == 0 ? "Todo estaba copiado ya en Descargas/AncoRA/grabaciones."
                : $"{(failed == 0 ? "Copia terminada" : "Copia INCOMPLETA")}: {copied} archivos ({SizeText(bytes)}) en Descargas/AncoRA/grabaciones.{(failed > 0 ? $" Fallaron {failed}." : "")}";
            return head + " " + report.Notice();
#else
            await Task.Yield();
            var report = await CompareWithManifestsAsync(sessions);
            Debug.Log($"{Tag} {report.LogLine()}");
            return $"La copia a Descargas solo funciona en el teléfono; las grabaciones están en {root}. {report.Notice()}";
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static class AndroidDownloads
        {
            public static bool Exists(string path, string relativeDirectory)
            {
                var activity = UnityEngine.Android.AndroidApplication.currentActivity;
                using var resolver = activity.Call<AndroidJavaObject>("getContentResolver");
                using var downloads = new AndroidJavaClass("android.provider.MediaStore$Downloads");
                using var collection = downloads.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");
                using var cursor = resolver.Call<AndroidJavaObject>("query", collection, new[] { "_size", "is_pending" },
                    "_display_name=? AND relative_path=?", new[] { Path.GetFileName(path), relativeDirectory.TrimEnd('/') + "/" }, null);
                return cursor != null && cursor.Call<bool>("moveToFirst") && cursor.Call<int>("getInt", 1) == 0 &&
                    cursor.Call<long>("getLong", 0) == new FileInfo(path).Length;
            }

            // MediaStore.Downloads (API 29+), as PaseoShare: the app adds files of its own without a storage permission.
            public static void Copy(string path, string relativeDirectory)
            {
                string name = Path.GetFileName(path);
                // MediaStore appends the extension of the mime type when the name lacks it; octet-stream leaves the name alone.
                string mime = name.EndsWith(".png", StringComparison.Ordinal) ? "image/png" :
                    name.EndsWith(".mp4", StringComparison.Ordinal) ? "video/mp4" : "application/octet-stream";
                // Unity owns currentActivity: it must not be disposed.
                var activity = UnityEngine.Android.AndroidApplication.currentActivity;
                using var resolver = activity.Call<AndroidJavaObject>("getContentResolver");
                using var values = new AndroidJavaObject("android.content.ContentValues");
                values.Call("put", "_display_name", name);
                values.Call("put", "mime_type", mime);
                values.Call("put", "relative_path", relativeDirectory);
                using var pending = new AndroidJavaObject("java.lang.Integer", 1);
                values.Call("put", "is_pending", pending);
                using var downloads = new AndroidJavaClass("android.provider.MediaStore$Downloads");
                using var collection = downloads.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");
                // Reuse the app-owned row on retry; leave no duplicate or partial file in Downloads.
                string[] args = { name, relativeDirectory.TrimEnd('/') + "/" };
                AndroidJavaObject existing = null;
                using (var cursor = resolver.Call<AndroidJavaObject>("query", collection, new[] { "_id" }, "_display_name=? AND relative_path=?", args, null))
                    if (cursor != null && cursor.Call<bool>("moveToFirst"))
                    {
                        using var contentUris = new AndroidJavaClass("android.content.ContentUris");
                        existing = contentUris.CallStatic<AndroidJavaObject>("withAppendedId", collection, cursor.Call<long>("getLong", 0));
                    }
                using var uri = existing ?? resolver.Call<AndroidJavaObject>("insert", collection, values)
                                ?? throw new IOException("MediaStore no creó el archivo en Descargas.");
                try
                {
                    resolver.Call<int>("update", uri, values, null, null);
                    using (var output = resolver.Call<AndroidJavaObject>("openOutputStream", uri, "wt")
                        ?? throw new IOException("No se pudo abrir el archivo de Descargas."))
                    using (var input = File.OpenRead(path))
                    {
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
                    using var finished = new AndroidJavaObject("android.content.ContentValues");
                    using var published = new AndroidJavaObject("java.lang.Integer", 0);
                    finished.Call("put", "is_pending", published);
                    resolver.Call<int>("update", uri, finished, null, null);
                }
                catch
                {
                    resolver.Call<int>("delete", uri, null, null);
                    throw;
                }
            }
        }
#endif
    }
}
