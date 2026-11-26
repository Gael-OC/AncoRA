#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Android;

namespace AncorRA.AR
{
    internal static class PaseoLegacyDownloads
    {
        internal static bool Required
        {
            get
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                return version.GetStatic<int>("SDK_INT") < 29;
            }
        }

        internal static async Task<string> GetRootAsync()
        {
            const string write = "android.permission.WRITE_EXTERNAL_STORAGE";
            if (!Permission.HasUserAuthorizedPermission(write))
            {
                var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => result.TrySetResult(true);
                callbacks.PermissionDenied += _ => result.TrySetResult(false);
                callbacks.PermissionRequestDismissed += _ => result.TrySetResult(false);
                Permission.RequestUserPermission(write, callbacks);
                if (!await result.Task)
                    throw new IOException("Para exportar a Descargas, permite el acceso al almacenamiento en Ajustes. Los originales siguen en la aplicación.");
            }
            using var env = new AndroidJavaClass("android.os.Environment");
            using var folder = env.CallStatic<AndroidJavaObject>("getExternalStoragePublicDirectory", env.GetStatic<string>("DIRECTORY_DOWNLOADS"));
            return Path.Combine(folder.Call<string>("getAbsolutePath"), "AncoRA");
        }

        // Publish on the same filesystem so a failed retry leaves the previous copy intact.
        internal static void Copy(string source, string destination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            string pending = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(source, pending);
                using var os = new AndroidJavaClass("android.system.Os");
                os.CallStatic("rename", pending, destination);
            }
            finally { File.Delete(pending); }
        }

        internal static AndroidJavaObject PublishZip(string zip, string destination)
        {
            Copy(zip, destination);
            var activity = AndroidApplication.currentActivity;
            using var downloads = activity.Call<AndroidJavaObject>("getSystemService", "download");
            string name = Path.GetFileName(destination);
            long id = downloads.Call<long>("addCompletedDownload", name, "Registro de AncoRA", false,
                "application/zip", destination, new FileInfo(destination).Length, false);
            return downloads.Call<AndroidJavaObject>("getUriForDownloadedFile", id)
                ?? throw new IOException("No se pudo compartir la copia guardada en Descargas/AncoRA.");
        }
    }
}
#endif
