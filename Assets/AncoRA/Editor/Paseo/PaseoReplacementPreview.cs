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
        // Opaque, before the mask's mark (1998): what the environment depth is on the phone.
        const int TableRenderQueue = 1990;

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
                    SaveWithTable(cam, target, content, $"{content.BuildingId}-{content.MapId}-oclusion.png");
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

        // A stand-in for a real table between the phone and the building. On the phone the AR background writes the ARCore
        // environment depth into the depth buffer before the opaques; here a plain opaque box in a queue before the mask's
        // mark does the same job. The mark must skip the pixels the table covers, the paint must not draw there, and the table
        // must hide the model: sky and ground around it, the table over the mask, as in front of a real one.
        static void SaveWithTable(Camera cam, RenderTexture target, PaseoMapContent content, string file)
        {
            var size = content.SizeMeters;
            var toBuilding = content.transform.position - cam.transform.position;
            float distance = toBuilding.magnitude;
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(table.GetComponent<Collider>());
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Mesa de la vista previa" };
            material.SetColor("_BaseColor", new Color(0.55f, 0.35f, 0.15f, 1f));
            material.renderQueue = TableRenderQueue;
            table.GetComponent<MeshRenderer>().sharedMaterial = material;
            try
            {
                // Beyond the mark's slack from the building, on the line of sight to its centre, standing on the ground.
                var ground = content.transform.position.y - size.y * 0.5f;
                var at = cam.transform.position + toBuilding * 0.4f;
                table.transform.localScale = new Vector3(size.x * 0.3f, 1.2f, 2f);
                table.transform.position = new Vector3(at.x, ground + 0.6f + 0.4f, at.z);
                table.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(toBuilding, Vector3.up));
                Debug.Log($"{Tag} Mesa de la vista previa a {distance * 0.4f:F1} m de la cámara; el edificio, a {distance:F1} m.");
                Save(cam, target, file);
            }
            finally
            {
                Object.DestroyImmediate(table);
                Object.DestroyImmediate(material);
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
