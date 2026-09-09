using System.Text;
using UnityEditor;
using UnityEditor.XR.ARSubsystems;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace AncorRA.AR.EditorTools
{
    /// <summary>
    /// Rebuilds the reference image library from the PACE UCN sign photos.
    ///
    /// The library stores each texture as a pair of serialized GUID halves, so hand-editing the
    /// .asset YAML silently produces a library that points at nothing. Everything here goes
    /// through the official Editor-only extension methods instead.
    /// </summary>
    public static class AncoraReferenceLibrarySetup
    {
        const string k_LibraryPath = "Assets/Scenes/HouseReferenceLibrary.asset";

        /// <summary>
        /// Real-world size of a reference image, expressed as the width of the whole photo.
        ///
        /// The declared size must cover the entire image, not just the sign inside it. If the photo
        /// includes wall or sky around the panel, that margin counts too: the tracking providers
        /// derive distance from apparent size divided by declared size, so a declared size that only
        /// measures the panel reports the sign closer than it really is, and content placed relative
        /// to it drifts along the camera ray.
        ///
        /// Height is always derived from the texture's aspect ratio so the two can never disagree.
        /// </summary>
        readonly struct Target
        {
            public readonly string name;
            public readonly string texturePath;
            public readonly float widthMeters;

            public Target(string name, string texturePath, float widthMeters)
            {
                this.name = name;
                this.texturePath = texturePath;
                this.widthMeters = widthMeters;
            }
        }

        static readonly Target[] k_Targets =
        {
            // IMG_0719 cropped to its top 46% and resampled to 2048 px.
            //
            // The full sign scores 0 with `arcoreimg eval-img` and the earlier IMG_0702 failed
            // outright with "Failed to get enough keypoints": the lower two thirds are flat magenta
            // with mirror reflections, which carry no stable features. The top of the panel - the UCN
            // crest, the PACE UCN wordmark and the large "2" - scores 100. A sweep showed a stable
            // plateau at 45-47% of the photo height, so 46% sits as far as possible from the cliffs
            // at 44% (score 30) and 48% (score 20). Full-resolution crops score lower than resampled
            // ones, hence the 2048 px cap.
            //
            // The panel is 1 m wide and the crop keeps the full width, so the width stays 1 m; the
            // height follows from the crop's aspect ratio (~1.088 m).
            //
            // Note the tracked pose origin is the center of THIS image, not of the whole sign, so it
            // sits roughly 0.6 m above the panel's midpoint. Calibration offsets are measured from
            // there.
            new Target("PaceUcnVertical", "Assets/AR/ReferenceImages/PaceUcnVertical.png", 1.0f),
        };

        [MenuItem("AncoRA/Reconstruir librería de imágenes de referencia")]
        public static void Rebuild()
        {
            var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(k_LibraryPath);
            if (library == null)
            {
                Debug.LogError($"No encontré la librería en {k_LibraryPath}.");
                return;
            }

            for (var i = library.count - 1; i >= 0; i--)
                library.RemoveAt(i);

            var report = new StringBuilder();
            report.AppendLine($"Librería reconstruida: {k_LibraryPath}");

            foreach (var target in k_Targets)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(target.texturePath);
                if (texture == null)
                {
                    Debug.LogError($"Falta la textura {target.texturePath}. La imagen '{target.name}' no se agregó.");
                    continue;
                }

                // Derive the aspect from the source file, never from the imported texture: import
                // settings can rescale a texture (nPOTScale, maxTextureSize) and a distorted
                // reference image both mis-sizes the target and degrades detection.
                if (!TryGetSourceSize(target.texturePath, out var sourceWidth, out var sourceHeight))
                {
                    sourceWidth = texture.width;
                    sourceHeight = texture.height;
                    Debug.LogWarning(
                        $"No pude leer el tamaño original de {target.texturePath}; " +
                        "usando el de la textura importada, que puede estar reescalada.");
                }

                var sourceAspect = (float)sourceHeight / sourceWidth;
                var importedAspect = (float)texture.height / texture.width;
                if (Mathf.Abs(sourceAspect - importedAspect) > 0.01f)
                {
                    Debug.LogError(
                        $"'{target.name}' se importa deformada: origen {sourceWidth}x{sourceHeight} " +
                        $"(1:{sourceAspect:0.###}) pero la textura queda {texture.width}x{texture.height} " +
                        $"(1:{importedAspect:0.###}). Pon nPOTScale en None y sube Max Size en el " +
                        "importador, o ARCore buscará un cartel con proporciones equivocadas.");
                }

                var heightMeters = target.widthMeters * sourceAspect;
                var index = library.count;

                library.Add();
                library.SetTexture(index, texture, false);
                library.SetName(index, target.name);
                library.SetSpecifySize(index, true);
                library.SetSize(index, new Vector2(target.widthMeters, heightMeters));

                report.AppendLine(
                    $"  [{index}] {target.name}: origen {sourceWidth}x{sourceHeight} px, " +
                    $"textura {texture.width}x{texture.height} px " +
                    $"-> {target.widthMeters:0.###} x {heightMeters:0.###} m (1:{sourceAspect:0.###})");
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.AppendLine("Recuerda: si mides la distancia real al cartel y el HUD reporta otra, " +
                              "corrige el ancho declarado en AncoraReferenceLibrarySetup y vuelve a ejecutar esto.");
            Debug.Log(report.ToString());
        }

        /// <summary>
        /// Reads the pixel dimensions of the file on disk, before any import setting touches them.
        /// </summary>
        static bool TryGetSourceSize(string assetPath, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
                return false;

            importer.GetSourceTextureWidthAndHeight(out width, out height);
            return width > 0 && height > 0;
        }

        [MenuItem("AncoRA/Mostrar contenido de la librería")]
        public static void Report()
        {
            var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(k_LibraryPath);
            if (library == null)
            {
                Debug.LogError($"No encontré la librería en {k_LibraryPath}.");
                return;
            }

            var report = new StringBuilder();
            report.AppendLine($"{k_LibraryPath} contiene {library.count} imagen(es):");
            for (var i = 0; i < library.count; i++)
            {
                var image = library[i];
                report.AppendLine(
                    $"  [{i}] '{image.name}' specifySize={image.specifySize} size={image.size.x:0.###} x {image.size.y:0.###} m");
            }

            Debug.Log(report.ToString());
        }
    }
}
