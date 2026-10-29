#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using AncorRA.AR;
using Immersal;
using Immersal.XR;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AncorRA.Editor
{
    /// <summary>
    /// Offline replay of the images the record mode saved (PaseoRecorder): every PNG goes through the same
    /// Core.LocalizeImage the phone used, with the intrinsics of its intentos.jsonl line, against the real campus maps, on
    /// the computer. Each image is run with four searches:
    ///   igual    the maps the phone searched (it should reproduce the phone's result),
    ///   edificio every map of the building the point belongs to,
    ///   todos    every map of every building (false positives: an image of one building that finds another),
    ///   completa the phone's maps with the plugin's reduction switched off (full 1920 × 1080).
    /// The first three use the size the phone used (max_pixeles of the line; 960 × 720 for lines from before it was recorded).
    /// The result is one JSON line per image and variant. It says whether a pose came out, not whether the box lands on the
    /// real building: that needs a measured reference.
    /// Run: Unity -batchmode -nographics -quit -projectPath … -executeMethod AncorRA.Editor.PaseoReplay.RunFromCommandLine
    /// -replayRoot &lt;folder with the session folders&gt; -replayOut &lt;result.jsonl&gt; [-replayMax N]
    /// </summary>
    public static class PaseoReplay
    {
        const string Tag = "[AncoRA Paseo][REPRODUCCION]";
        const string MapsRoot = "Assets/AncoRA/Paseo";
        // Lines recorded before 1.4.2 have no max_pixeles: the phone then always worked at the SDK's 960 × 720.
        const int ReducedPixels = PaseoResolution.Reduced;

        static readonly Dictionary<string, string> BuildingOfCode = new()
        {
            ["CB"] = "CienciasBasicas",
            ["EIC"] = "EIC",
            ["X1"] = "X1",
            ["TEO"] = "Teologia"
        };

        sealed class Row
        {
            public string Session;
            public string Folder;
            public string Image;
            public string Point;
            public string Light;
            public string Phone;
            public int Attempt;
            public int Width;
            public int Height;
            public int Channels;
            public Vector4 Intrinsics;
            public Quaternion Rotation;
            public int[] SearchedMaps;
            public string SearchedBuilding;
            public int PhoneMap;
            public int PhoneConfidence;
            public double PhoneSeconds;
            public int MaxPixels;
        }

        public static void RunFromCommandLine()
        {
            string root = Arg("-replayRoot");
            string output = Arg("-replayOut");
            int max = int.TryParse(Arg("-replayMax"), out var parsed) ? parsed : int.MaxValue;
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(output))
                throw new ArgumentException("Faltan -replayRoot y -replayOut.");
            Run(root, output, max);
        }

        public static void Run(string root, string output, int max)
        {
            var maps = LoadMaps(out var mapsOfBuilding);
            try
            {
                var rows = ReadRows(root).Take(max).ToList();
                Debug.Log($"{Tag} {rows.Count} imágenes de {root}; {maps.Count} mapas cargados.");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                using var writer = new StreamWriter(output, false);
                var clock = Stopwatch.StartNew();
                int done = 0;
                foreach (var row in rows)
                {
                    byte[] pixels = ReadImage(Path.Combine(row.Folder, row.Image), row.Width, row.Height);
                    if (pixels == null)
                    {
                        Debug.LogWarning($"{Tag} No se pudo leer {row.Image}.");
                        continue;
                    }
                    string code = row.Point.Split('-')[0];
                    mapsOfBuilding.TryGetValue(BuildingOfCode.GetValueOrDefault(code, ""), out var own);
                    own ??= new List<int>();

                    // The phone's search, at the size the phone used.
                    Core.SetInteger("LocalizationMaxPixels", row.MaxPixels);
                    Write(writer, row, "igual", row.SearchedMaps, pixels, row.MaxPixels);
                    Write(writer, row, "edificio", own.ToArray(), pixels, row.MaxPixels);
                    Write(writer, row, "todos", maps.Keys.ToArray(), pixels, row.MaxPixels);
                    Core.SetInteger("LocalizationMaxPixels", 0);
                    Write(writer, row, "completa", row.SearchedMaps, pixels, 0);
                    Core.SetInteger("LocalizationMaxPixels", ReducedPixels);

                    if (++done % 25 == 0)
                    {
                        writer.Flush();
                        Debug.Log($"{Tag} {done}/{rows.Count} imágenes, {clock.Elapsed.TotalMinutes:F1} min.");
                    }
                }
                Debug.Log($"{Tag} Listo: {done} imágenes en {clock.Elapsed.TotalMinutes:F1} min → {output}");
            }
            finally
            {
                foreach (int id in maps.Keys)
                    Core.FreeMap(id);
            }
        }

        static void Write(StreamWriter writer, Row row, string variant, int[] ids, byte[] pixels, int maxPixels)
        {
            var intrinsics = row.Intrinsics;
            var rotation = row.Rotation;
            LocalizeInfo info = default;
            info.mapId = -1;
            var clock = Stopwatch.StartNew();
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                // Same call as PaseoLocalization.Localize: n = number of ids, default solver.
                info = Core.LocalizeImage(ids.Length, ids.Length == 0 ? new int[1] : ids, row.Width, row.Height, ref intrinsics,
                    handle.AddrOfPinnedObject(), row.Channels, (int)SolverType.Default, ref rotation);
            }
            finally
            {
                handle.Free();
            }
            double seconds = clock.Elapsed.TotalSeconds;
            bool ok = info.mapId > 0;
            var inv = CultureInfo.InvariantCulture;
            writer.WriteLine(string.Join(",", new[]
            {
                $"\"sesion\":\"{row.Session}\"",
                $"\"imagen\":\"{row.Image}\"",
                $"\"n\":{row.Attempt}",
                $"\"punto\":\"{row.Point}\"",
                $"\"luz\":\"{row.Light}\"",
                $"\"telefono\":\"{row.Phone}\"",
                $"\"variante\":\"{variant}\"",
                $"\"max_pixeles\":{maxPixels}",
                $"\"mapas\":[{string.Join(",", ids)}]",
                $"\"telefono_mapa\":{row.PhoneMap}",
                $"\"telefono_conf\":{row.PhoneConfidence}",
                $"\"telefono_seg\":{row.PhoneSeconds.ToString("F3", inv)}",
                $"\"mapa\":{(ok ? info.mapId : 0)}",
                $"\"conf\":{(ok ? info.confidence : 0)}",
                $"\"rmse\":{(ok ? info.rmse.ToString("F4", inv) : "null")}",
                $"\"pos\":{(ok ? $"[{info.position.x.ToString("F3", inv)},{info.position.y.ToString("F3", inv)},{info.position.z.ToString("F3", inv)}]" : "null")}",
                $"\"seg\":{seconds.ToString("F3", inv)}"
            }).Insert(0, "{") + "}");
        }

        // ---------------------------------------------------------------- inputs

        static Dictionary<int, string> LoadMaps(out Dictionary<string, List<int>> mapsOfBuilding)
        {
            var loaded = new Dictionary<int, string>();
            mapsOfBuilding = new Dictionary<string, List<int>>();
            foreach (var dir in Directory.GetDirectories(MapsRoot))
            {
                string building = Path.GetFileName(dir);
                foreach (var file in Directory.GetFiles(dir, "*.bytes"))
                {
                    string name = Path.GetFileName(file);
                    int dash = name.IndexOf('-');
                    if (dash <= 0 || !int.TryParse(name.Substring(0, dash), out int id))
                        continue;
                    int handle = Core.LoadMap(id, File.ReadAllBytes(file));
                    if (handle < 0)
                        throw new InvalidOperationException($"El plugin no cargó el mapa {id} ({name}).");
                    loaded[id] = building;
                    if (!mapsOfBuilding.TryGetValue(building, out var list))
                        mapsOfBuilding[building] = list = new List<int>();
                    list.Add(id);
                }
            }
            return loaded;
        }

        static IEnumerable<Row> ReadRows(string root)
        {
            foreach (var folder in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.Ordinal))
            {
                // The same line can be in both intentos.jsonl and a later copy «intentos (1).jsonl»; one row per image.
                var seen = new HashSet<string>();
                foreach (var file in Directory.GetFiles(folder, "intentos*.jsonl").OrderBy(f => f, StringComparer.Ordinal))
                foreach (var line in File.ReadLines(file).Where(l => l.Length > 0))
                {
                    var raw = JsonUtility.FromJson<RawRow>(line);
                    if (raw?.resultado == null || raw.busca == null || string.IsNullOrEmpty(raw.archivo) ||
                        !File.Exists(Path.Combine(folder, raw.archivo)) || !seen.Add(raw.archivo))
                        continue;
                    var rotation = new Quaternion(raw.rot_camara[0], raw.rot_camara[1], raw.rot_camara[2], raw.rot_camara[3]) *
                                   new Quaternion(raw.rot_pantalla[0], raw.rot_pantalla[1], raw.rot_pantalla[2], raw.rot_pantalla[3]);
                    rotation.SwitchHandedness();
                    yield return new Row
                    {
                        Session = Path.GetFileName(folder),
                        Folder = folder,
                        Image = raw.archivo,
                        Point = raw.punto,
                        Light = raw.luz,
                        Phone = raw.telefono,
                        Attempt = raw.n,
                        Width = raw.ancho,
                        Height = raw.alto,
                        Channels = raw.canales,
                        Intrinsics = new Vector4(raw.intrinsecos.fx, raw.intrinsecos.fy, raw.intrinsecos.cx, raw.intrinsecos.cy),
                        Rotation = rotation,
                        SearchedMaps = raw.busca.mapas,
                        SearchedBuilding = raw.busca.edificio,
                        PhoneMap = raw.resultado.mapa,
                        PhoneConfidence = raw.resultado.conf,
                        PhoneSeconds = raw.resultado.seg,
                        MaxPixels = MaxPixelsOf(line, raw)
                    };
                }
            }
        }

        // 0 is a real value (the full image), so a line from before 1.4.2 is told apart by lacking the key, not by reading 0.
        static int MaxPixelsOf(string line, RawRow raw) => line.Contains("\"max_pixeles\":") ? raw.max_pixeles : ReducedPixels;

        // The recorder saved the buffer with its rows flipped (Unity encodes from the bottom), so they are flipped back here.
        static byte[] ReadImage(string path, int width, int height)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path)) || texture.width != width || texture.height != height)
                    return null;
                var colors = texture.GetPixels32();
                var pixels = new byte[width * height];
                for (int y = 0; y < height; y++)
                {
                    int from = (height - 1 - y) * width;
                    for (int x = 0; x < width; x++)
                        pixels[y * width + x] = colors[from + x].r;
                }
                return pixels;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        // Only what the replay reads from a line of intentos.jsonl (JsonUtility ignores the rest).
        [Serializable]
        sealed class RawRow
        {
            public int n;
            public string archivo;
            public string punto;
            public string luz;
            public string telefono;
            public int ancho;
            public int alto;
            public int canales;
            public RawIntrinsics intrinsecos;
            public float[] rot_camara;
            public float[] rot_pantalla;
            public RawSearch busca;
            public RawResult resultado;
            public int max_pixeles;
        }

        [Serializable]
        sealed class RawIntrinsics
        {
            public float fx, fy, cx, cy;
        }

        [Serializable]
        sealed class RawSearch
        {
            public string edificio;
            public int[] mapas;
        }

        [Serializable]
        sealed class RawResult
        {
            public int mapa;
            public int conf;
            public double seg;
        }
    }
}
#endif
