#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using AncorRA.AR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AncorRA.Editor
{
    /// <summary>
    /// Renders each building that has a replacement as the phone would see it from in front, before (its box) and after
    /// (the building painted over and the model in its place), into Logs/reemplazos/. Dark grey stands in for the camera
    /// image. Needs a graphics device: run batch mode without -nographics. The scene is not saved.
    /// </summary>
    public static class PaseoReplacementPreview
    {
        const string Tag = "[AncoRA Paseo]";
        const string Scene = "Assets/Scenes/PaseoIngenieria.unity";
        const string OutFolder = "Logs/reemplazos";
        const int Width = 720;
        const int Height = 1280;
        const float EyeHeightMeters = 1.6f;
        const float SideDegrees = 25f;

        [MenuItem("AncoRA/Paseo/Vista previa de los reemplazos")]
        public static void Render()
        {
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var contents = Object.FindObjectsByType<PaseoMapContent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var withReplacement = contents.Where(c => c.Replacement != null).ToArray();
            if (withReplacement.Length == 0)
                throw new InvalidOperationException("Ningún edificio tiene reemplazo; activarlo en su edificio.json y correr Prepare.");
            Directory.CreateDirectory(OutFolder);

            var cameraObject = new GameObject("Cámara de la vista previa");
            var cam = cameraObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;
            var target = new RenderTexture(Width, Height, 24);
            cam.targetTexture = target;
            try
            {
                foreach (var content in withReplacement)
                {
                    foreach (var other in contents)
                        other.SetVisible(false);
                    var size = content.SizeMeters;
                    // From in front of the face with the X (+Z), a little to the side, at eye height, far enough for the whole width.
                    float distance = Mathf.Max(size.x, size.y) * 1.6f + size.z * 0.5f;
                    var local = Quaternion.Euler(0f, SideDegrees, 0f) * new Vector3(0f, 0f, distance);
                    local.y = -size.y * 0.5f + EyeHeightMeters;
                    cam.transform.position = content.transform.TransformPoint(local);
                    cam.transform.LookAt(content.transform.position);

                    content.Box.SetVisible(true);
                    Save(cam, target, $"{content.BuildingId}-{content.MapId}-antes.png");
                    content.Box.SetVisible(false);
                    content.SetVisible(true);
                    Save(cam, target, $"{content.BuildingId}-{content.MapId}-despues.png");
                }
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(cameraObject);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        static void Save(Camera cam, RenderTexture target, string file)
        {
            cam.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            string path = Path.Combine(OutFolder, file);
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            Debug.Log($"{Tag} Vista previa del reemplazo: {path}");
        }
    }
}
#endif
