#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using AncorRA.AR;
using Immersal;
using UnityEditor;
using UnityEngine;

namespace AncorRA.Editor
{
    /// <summary>
    /// Writes the sparse point cloud of every map .bytes of the tour to Escenas/&lt;Edificio&gt;/&lt;id&gt;-&lt;nombre&gt;-sparse.ply
    /// so Tools/RegistroDron can register each map against the drone cloud of its building. The file has the layout and
    /// the native axes of the -sparse.ply the Immersal portal hands out (Unity shows a point as (-x, y, z)), read straight
    /// from the Editor plugin: no portal access and no token.
    /// </summary>
    public static class PaseoMapClouds
    {
        const string Tag = "[AncoRA Paseo]";
        const string OutputRoot = "Escenas";

        [MenuItem("AncoRA/Paseo/Exportar nubes de los mapas")]
        public static void Export()
        {
            var paths = new PaseoPaths();
            int exported = 0;
            var folders = Directory.GetDirectories(paths.DataRoot)
                .Where(d => !Path.GetFileName(d).StartsWith("_"))
                .OrderBy(d => d, StringComparer.Ordinal);
            foreach (string folder in folders)
            {
                string building = Path.GetFileName(folder);
                foreach (string file in Directory.GetFiles(folder, "*.bytes").OrderBy(f => f, StringComparer.Ordinal))
                {
                    string name = Path.GetFileName(file);
                    int? id = PaseoConfig.IdFromFileName(name);
                    if (id == null)
                        throw new InvalidOperationException($"{file}: el nombre no tiene la forma <id>-<nombre>.bytes.");
                    Vector3[] points = ReadPoints(id.Value, File.ReadAllBytes(file));
                    string outDir = Path.Combine(OutputRoot, building);
                    Directory.CreateDirectory(outDir);
                    string outFile = Path.Combine(outDir, Path.GetFileNameWithoutExtension(name) + "-sparse.ply");
                    WritePly(outFile, points);
                    Debug.Log($"{Tag} Nube del mapa {id}: {points.Length} puntos -> {outFile}");
                    exported++;
                }
            }
            if (exported == 0)
                throw new InvalidOperationException($"No hay archivos .bytes en {paths.DataRoot}.");
            Debug.Log($"{Tag} Nubes exportadas: {exported}.");
        }

        static Vector3[] ReadPoints(int mapId, byte[] bytes)
        {
            int handle = Core.LoadMap(mapId, bytes);
            try
            {
                int count = Core.GetPointCloudSize(mapId);
                if (handle < 0 || count <= 0)
                    throw new InvalidOperationException($"El plugin no cargó el mapa {mapId}: handle={handle}, puntos={count}.");
                var points = new Vector3[count];
                int read = Core.GetPointCloud(mapId, points);
                if (read <= 0)
                    throw new InvalidOperationException($"El plugin no entregó los puntos del mapa {mapId} ({read}).");
                Array.Resize(ref points, Math.Min(read, count));
                return points;
            }
            finally
            {
                Core.FreeMap(mapId);
            }
        }

        // Binary little endian, float x y z + uchar r g b: the portal's -sparse.ply layout. The plugin gives no colour.
        static void WritePly(string path, Vector3[] points)
        {
            string header = "ply\nformat binary_little_endian 1.0\n" +
                            "comment exported from the map .bytes by PaseoMapClouds (native Immersal axes)\n" +
                            $"element vertex {points.Length}\n" +
                            "property float x\nproperty float y\nproperty float z\n" +
                            "property uchar red\nproperty uchar green\nproperty uchar blue\nend_header\n";
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(Encoding.ASCII.GetBytes(header));
            foreach (Vector3 p in points)
            {
                writer.Write(p.x);
                writer.Write(p.y);
                writer.Write(p.z);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
            }
        }
    }
}
#endif
