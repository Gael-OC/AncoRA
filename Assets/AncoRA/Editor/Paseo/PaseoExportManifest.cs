#if UNITY_EDITOR && UNITY_ANDROID
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace AncorRA.Editor
{
    // Exporting to public Downloads requires write permission only before MediaStore.Downloads.
    internal sealed class PaseoExportManifest : IPreprocessBuildWithReport, IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 10;
        static BuildOptions options;

        public void OnPreprocessBuild(BuildReport report) => options = report.summary.options;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string file = Path.Combine(path, "src/main/AndroidManifest.xml");
            var doc = XDocument.Load(file);
            ConfigurePermission(doc, options);
            doc.Save(file);
        }

        internal static void ConfigurePermission(XDocument doc, BuildOptions buildOptions)
        {
            XNamespace android = "http://schemas.android.com/apk/res/android";
            const string write = "android.permission.WRITE_EXTERNAL_STORAGE";
            var permission = doc.Root.Elements("uses-permission")
                .FirstOrDefault(e => (string)e.Attribute(android + "name") == write);
            if ((buildOptions & BuildOptions.Development) == 0)
            {
                permission?.Remove();
                return;
            }
            if (permission == null)
            {
                permission = new XElement("uses-permission", new XAttribute(android + "name", write));
                doc.Root.AddFirst(permission);
            }
            permission.SetAttributeValue(android + "maxSdkVersion", "28");
        }
    }
}
#endif
