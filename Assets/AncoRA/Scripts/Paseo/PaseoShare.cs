using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>
    /// «Compartir registro» of the team sheet: zips the newest session logs, their debug photos and a description of the
    /// phone, saves the zip in Downloads/AncoRA (MediaStore, no permission needed) and opens Android's share sheet, so the
    /// team sends it by WhatsApp from any phone. The zip name says which phone and when. Tools/BajarRegistro.ps1 -Archivo
    /// opens it on the computer.
    /// </summary>
    public static class PaseoShare
    {
        const string Tag = "[AncoRA Paseo]";
        public const int Sessions = 3;
        static readonly Regex Unsafe = new("[^A-Za-z0-9]+");

        public static string ZipName(string deviceModel, DateTime when)
        {
            string model = Unsafe.Replace(deviceModel ?? "", "-").Trim('-');
            if (model.Length == 0)
                model = "telefono";
            if (model.Length > 30)
                model = model.Substring(0, 30).TrimEnd('-');
            return $"AncoRA-registro-{model}-{when:yyyyMMdd-HHmmss}.zip";
        }

        /// <summary>The newest <paramref name="keep"/> session logs; names sort by start time (paseo-AAAAMMDD-HHMMSS.log).</summary>
        public static List<string> NewestSessions(IEnumerable<string> logNames, int keep) =>
            logNames.OrderBy(n => n, StringComparer.Ordinal).Reverse().Take(keep).Reverse().ToList();

        /// <summary>«20261021-170512» for paseo-20261021-170512.log: the name of the session's photo folder.</summary>
        public static string SessionStamp(string logName) => Path.GetFileNameWithoutExtension(logName).Replace("paseo-", "");

        /// <summary>Builds the zip, then saves and shares it. Returns what to tell the team.</summary>
        public static async Task<string> ShareAsync()
        {
            PaseoSessionLog.FlushCurrent();
            string folder = PaseoSessionLog.Folder;
            string name = ZipName(SystemInfo.deviceModel, DateTime.Now);
            string zip = Path.Combine(Application.temporaryCachePath, name);
            string phone = DescribePhone();
            int files;
            try
            {
                files = await Task.Run(() => BuildZip(folder, zip, phone));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} No se pudo armar el zip del registro: {e.Message}");
                return "No se pudo armar el registro: " + e.Message;
            }
            long bytes = new FileInfo(zip).Length;
            Debug.Log($"{Tag} Registro para compartir: {name} ({files} archivos, {bytes / 1024} KB).");
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                SaveToDownloadsAndShare(zip, name);
                return $"Guardado en Descargas/AncoRA: {name}. Elige WhatsApp para enviarlo.";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} No se pudo compartir el registro: {e.Message}");
                return $"No se pudo abrir «Compartir» ({e.Message}); el zip quedó en {zip}.";
            }
#else
            return $"Zip listo en {zip} (compartir solo funciona en el teléfono).";
#endif
        }

        static int BuildZip(string folder, string zip, string phone)
        {
            if (File.Exists(zip))
                File.Delete(zip);
            int count = 0;
            using var stream = new FileStream(zip, FileMode.Create, FileAccess.Write);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
            using (var writer = new StreamWriter(archive.CreateEntry("telefono.txt").Open(), new UTF8Encoding(false)))
                writer.Write(phone);
            count++;
            if (!Directory.Exists(folder))
                return count;
            var logs = NewestSessions(Directory.GetFiles(folder, "paseo-*.log").Select(Path.GetFileName), Sessions);
            foreach (string log in logs)
            {
                // The current log is still open for writing: read it shared.
                AddFile(archive, Path.Combine(folder, log), log, System.IO.Compression.CompressionLevel.Optimal);
                count++;
                string photos = Path.Combine(folder, "imagenes", SessionStamp(log));
                if (!Directory.Exists(photos))
                    continue;
                // JPEGs do not compress further.
                foreach (string photo in Directory.GetFiles(photos, "*.jpg"))
                {
                    AddFile(archive, photo, $"imagenes/{SessionStamp(log)}/{Path.GetFileName(photo)}", System.IO.Compression.CompressionLevel.NoCompression);
                    count++;
                }
            }
            return count;
        }

        static void AddFile(ZipArchive archive, string path, string entryName, System.IO.Compression.CompressionLevel level)
        {
            using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var target = archive.CreateEntry(entryName, level).Open();
            source.CopyTo(target);
        }

        static string DescribePhone()
        {
            var text = new StringBuilder();
            text.AppendLine($"Modelo: {SystemInfo.deviceModel}");
            text.AppendLine($"Sistema: {SystemInfo.operatingSystem}");
            text.AppendLine($"App: AncoRA Paseo {Application.version}, build {Application.buildGUID}, desarrollo {(Debug.isDebugBuild ? "sí" : "no")}");
            text.AppendLine($"Pantalla: {Screen.width}×{Screen.height}, {Screen.dpi:F0} dpi");
            text.AppendLine($"Procesador: {SystemInfo.processorType}, {SystemInfo.processorCount} núcleos; memoria {SystemInfo.systemMemorySize} MB");
            text.AppendLine($"Gráficos: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceVersion})");
            text.AppendLine($"Compartido: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            return text.ToString();
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // MediaStore.Downloads (API 29+): the app may add its own files without a storage permission, and the content URI it
        // gets back can be handed to WhatsApp with a read grant.
        static void SaveToDownloadsAndShare(string zip, string name)
        {
            // Unity owns currentActivity: it must not be disposed.
            var activity = UnityEngine.Android.AndroidApplication.currentActivity;
            using var resolver = activity.Call<AndroidJavaObject>("getContentResolver");
            using var values = new AndroidJavaObject("android.content.ContentValues");
            values.Call("put", "_display_name", name);
            values.Call("put", "mime_type", "application/zip");
            values.Call("put", "relative_path", "Download/AncoRA");
            using var downloads = new AndroidJavaClass("android.provider.MediaStore$Downloads");
            using var collection = downloads.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");
            using var uri = resolver.Call<AndroidJavaObject>("insert", collection, values)
                            ?? throw new IOException("MediaStore no creó el archivo en Descargas.");
            using (var output = resolver.Call<AndroidJavaObject>("openOutputStream", uri))
            using (var input = File.OpenRead(zip))
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

            using var intentClass = new AndroidJavaClass("android.content.Intent");
            using var send = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND");
            send.Call<AndroidJavaObject>("setType", "application/zip").Dispose();
            send.Call<AndroidJavaObject>("putExtra", "android.intent.extra.STREAM", uri).Dispose();
            send.Call<AndroidJavaObject>("putExtra", "android.intent.extra.SUBJECT", name).Dispose();
            send.Call<AndroidJavaObject>("addFlags", 1).Dispose(); // FLAG_GRANT_READ_URI_PERMISSION
            using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", send, "Compartir registro de AncoRA");
            chooser.Call<AndroidJavaObject>("addFlags", 1).Dispose();
            activity.Call("startActivity", chooser);
            Debug.Log($"{Tag} Registro guardado en Descargas/AncoRA/{name}; se abrió «Compartir».");
        }
#endif
    }
}
