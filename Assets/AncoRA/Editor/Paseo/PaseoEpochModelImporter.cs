#if UNITY_EDITOR
using System;
using System.IO;
using AncorRA.AR;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AncorRA.Editor
{
    /// <summary>Portable, normalized meshes exported by Tools/ConvertirModelosEpocas.py from the supplied OBJ/MAX.</summary>
    internal static class PaseoEpochModelImporter
    {
        internal const string SourceRoot = "Assets/AncoRA/Paseo/_FuentesModelos";

        [Serializable] sealed class ModelData
        {
            public string source;
            public float[] vertices, normals, uv;
            public SubmeshData[] submeshes;
            public MaterialData[] materials;
        }

        [Serializable] sealed class SubmeshData { public int[] triangles; }
        [Serializable] sealed class MaterialData { public string name, texture; public float[] color; }

        static ModelData Load(PaseoEpoch epoch)
        {
            if (epoch != PaseoEpoch.Y1950 && epoch != PaseoEpoch.Y1980)
                throw new ArgumentException($"La época {epoch} no tiene modelo importado.");
            string path = $"{SourceRoot}/{PaseoEpochs.Key(epoch)}/modelo.json";
            var data = JsonUtility.FromJson<ModelData>(File.ReadAllText(path));
            if (data?.vertices == null || data.vertices.Length == 0 || data.vertices.Length % 3 != 0 ||
                data.normals == null || data.normals.Length != data.vertices.Length ||
                data.uv == null || data.uv.Length != data.vertices.Length / 3 * 2 ||
                data.submeshes == null || data.materials == null || data.submeshes.Length != data.materials.Length)
                throw new InvalidDataException($"Modelo de época incompleto: {path}");
            return data;
        }

        internal static Mesh CreateMesh(PaseoEpoch epoch)
        {
            var data = Load(epoch);
            int count = data.vertices.Length / 3;
            var vertices = new Vector3[count];
            var normals = new Vector3[count];
            var uv = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                int p = i * 3;
                vertices[i] = new Vector3(data.vertices[p], data.vertices[p + 1], data.vertices[p + 2]);
                normals[i] = new Vector3(data.normals[p], data.normals[p + 1], data.normals[p + 2]);
                uv[i] = new Vector2(data.uv[i * 2], data.uv[i * 2 + 1]);
            }
            var mesh = new Mesh
            {
                name = data.source,
                indexFormat = count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
                vertices = vertices, normals = normals, uv = uv, subMeshCount = data.submeshes.Length
            };
            for (int i = 0; i < data.submeshes.Length; i++)
                mesh.SetTriangles(data.submeshes[i].triangles, i);
            mesh.RecalculateBounds();
            return mesh;
        }

        internal static Material[] CreateMaterials(Shader shader, PaseoEpoch epoch)
        {
            var data = Load(epoch);
            var materials = new Material[data.materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                var source = data.materials[i];
                var material = new Material(shader) { name = source.name, renderQueue = (int)RenderQueue.Geometry };
                material.SetColor("_BaseColor", new Color(source.color[0], source.color[1], source.color[2], 1f));
                if (!string.IsNullOrEmpty(source.texture))
                {
                    string path = $"{SourceRoot}/{PaseoEpochs.Key(epoch)}/{source.texture}";
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture == null)
                        throw new InvalidDataException($"Falta la textura del modelo de {PaseoEpochs.Label(epoch)}: {path}");
                    material.SetTexture("_BaseMap", texture);
                }
                materials[i] = material;
            }
            return materials;
        }
    }
}
#endif
