#if UNITY_EDITOR
using System.IO;
using System.Linq;
using AncorRA.AR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AncorRA.Editor
{
    /// <summary>Renders the actual baked meshes without starting AR or changing the saved demo scene.</summary>
    public static class PaseoDemoPreview
    {
        [MenuItem("AncoRA/Paseo/Exportar vista previa de edificios demo")]
        public static void Export()
        {
            const string folder = "Builds/Demo";
            Directory.CreateDirectory(folder);
            var source = EditorSceneManager.OpenScene("Assets/Scenes/PaseoIngenieria.unity");
            var contents = Object.FindObjectsByType<PaseoMapContent>(FindObjectsInactive.Include)
                .Where(c => c.BuildingModel != null).OrderBy(c => c.BuildingId).ToArray();
            var preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(preview);
            var groundMaterial = new Material(Shader.Find(PaseoParkBuilder.ShaderName)) { renderQueue = (int)RenderQueue.Geometry };
            groundMaterial.SetColor("_BaseColor", new Color(0.75f, 0.80f, 0.73f));
            try
            {
                var camera = new GameObject("Cámara de vista previa").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.88f, 0.91f, 0.90f);
                camera.orthographic = true;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 200f;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.cullingMask = 1 << 31;
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Suelo de vista previa";
                ground.layer = 31;
                ground.transform.position = new Vector3(0f, -0.2f, 0f);
                ground.transform.localScale = new Vector3(150f, 0.3f, 150f);
                ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
                for (int i = 0; i < contents.Length; i++)
                {
                    var c = contents[i];
                    camera.orthographicSize = c.SizeMeters.x * 0.45f;
                    camera.transform.position = new Vector3(25f, 19f, 32f);
                    camera.transform.LookAt(new Vector3(0f, c.SizeMeters.y / 2f, 0f));
                    RenderModel(camera, c, c.BuildingModel, $"{folder}/{c.BuildingId}.png", 1f);
                    // One view per era, and the 1950 model half built, as the era change shows it.
                    foreach (var epoch in new[] { PaseoEpoch.Y1950, PaseoEpoch.Y1980 })
                        if (c.OwnEpochModel(epoch) != null)
                            RenderModel(camera, c, c.OwnEpochModel(epoch), $"{folder}/{c.BuildingId}-{PaseoEpochs.Key(epoch)}.png", 1f);
                    if (c.OwnEpochModel(PaseoEpoch.Y1950) != null)
                        RenderModel(camera, c, c.OwnEpochModel(PaseoEpoch.Y1950), $"{folder}/{c.BuildingId}-1950-a-medias.png", 0.5f);
                }
                foreach (var c in contents)
                {
                    var clone = Object.Instantiate(c.gameObject).GetComponent<PaseoMapContent>();
                    foreach (var child in clone.GetComponentsInChildren<Transform>(true))
                        child.gameObject.layer = 31;
                    clone.gameObject.SetActive(true);
                    clone.SetVisible(true);
                    if (clone.Park != null)
                        clone.Park.SendMessage("Update");
                }
                camera.orthographicSize = 35f;
                camera.transform.position = new Vector3(65f, 57f, 65f);
                camera.transform.LookAt(new Vector3(5f, 1f, 7f));
                Render(camera, $"{folder}/Campus.png", 1600, 1100);
                Debug.Log($"[AncoRA Paseo] Vistas previas guardadas en {Path.GetFullPath(folder)}.");
            }
            finally
            {
                Object.DestroyImmediate(groundMaterial);
                EditorSceneManager.CloseScene(preview, true);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(source);
            }
        }

        static void RenderModel(Camera camera, PaseoMapContent c, Transform model, string file, float reveal)
        {
            var clone = Object.Instantiate(model.gameObject);
            clone.SetActive(true);
            clone.layer = 31;
            clone.transform.position = new Vector3(0f, c.SizeMeters.y / 2f, 0f);
            clone.transform.rotation = Quaternion.identity;
            clone.transform.localScale = c.SizeMeters;
            if (reveal < 1f)
            {
                var block = new MaterialPropertyBlock();
                var bounds = clone.GetComponent<MeshFilter>().sharedMesh.bounds;
                block.SetVector("_RevealRange", new Vector4(bounds.min.y - 0.05f, bounds.max.y + 0.05f, 0f, 0f));
                block.SetFloat("_Reveal", reveal);
                clone.GetComponent<Renderer>().SetPropertyBlock(block);
            }
            Render(camera, file, 1280, 900);
            Object.DestroyImmediate(clone);
        }

        static void Render(Camera camera, string file, int width, int height)
        {
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(file, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(texture);
            }
        }
    }
}
#endif
