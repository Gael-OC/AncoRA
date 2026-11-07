using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace AncorRA.AR
{
    /// <summary>One file of a recording folder as the manifest lists it.</summary>
    public readonly struct PaseoManifestFile
    {
        public PaseoManifestFile(string name, long bytes, string sha256)
        {
            Name = name;
            Bytes = bytes;
            Sha256 = sha256;
        }

        public string Name { get; }
        public long Bytes { get; }
        public string Sha256 { get; }
    }

    /// <summary>What manifiesto.json says about a folder; PaseoRecorder fills it when the folder is complete.</summary>
    public sealed class PaseoManifestData
    {
        public string Folder = "";
        public string CaptureKind = "";
        public DateTime Time = DateTime.Now;
        public List<PaseoManifestFile> Images = new();
        public PaseoManifestFile Rows;
        public int RowCount;
        public int Dropped;
        public bool Complete;
        /// <summary>Why the capture stopped before its end (empty when it ended normally).</summary>
        public string StopReason = "";
        /// <summary>Distance walked by the camera; NaN when the capture does not measure it.</summary>
        public double TravelMeters = double.NaN;
        public bool ShortTravel;
    }

    public enum PaseoManifestVerdict { Complete, Incomplete, NoManifest }

    public readonly struct PaseoManifestCheck
    {
        public PaseoManifestCheck(PaseoManifestVerdict verdict, string problems)
        {
            Verdict = verdict;
            Problems = problems ?? "";
        }

        public PaseoManifestVerdict Verdict { get; }
        /// <summary>What does not match, in Spanish and short; empty when complete.</summary>
        public string Problems { get; }
    }

    /// <summary>The result of comparing every recording folder with its manifest after copying them.</summary>
    public sealed class PaseoCopyReport
    {
        public int Folders;
        public int Complete;
        public int Incomplete;
        public int NoManifest;
        public readonly List<string> IncompleteFolders = new();

        public string LogLine() => $"COPIA_GRABACIONES carpetas={Folders} completas={Complete} incompletas={Incomplete} sin_manifiesto={NoManifest}";

        /// <summary>The sentence for the team's notice.</summary>
        public string Notice()
        {
            string text = $"{Folders} carpetas: {Complete} completas, {Incomplete} incompletas, {NoManifest} sin manifiesto.";
            if (IncompleteFolders.Count > 0)
                text += " Revisa: " + string.Join(", ", IncompleteFolders.Take(3)) + (IncompleteFolders.Count > 3 ? "…" : "") + ".";
            return text;
        }
    }

    /// <summary>
    /// manifiesto.json of a recording folder (a sweep, a walk or a recording session): the folder, the kind of capture, how many
    /// images, each file with its size and SHA-256, the rows of intentos.jsonl, the dropped frames and whether the capture
    /// ended normally. It is written once the last image is on disk, so a folder that has one was saved to the end; copying
    /// the folder to Descargas compares what arrived with it. Written to a temporary file and renamed: it is whole or absent.
    /// </summary>
    public static class PaseoRecordManifest
    {
        public const string FileName = "manifiesto.json";
        public const string TemporarySuffix = ".tmp";
        public const int Format = 1;

        // JsonUtility reads it back; the field names are the keys of the file.
        [Serializable]
        class FileDto
        {
            public string archivo;
            public long bytes;
            public string sha256;
        }

        [Serializable]
        class ManifestDto
        {
            public int formato;
            public string carpeta;
            public string tipo_captura;
            public string hora;
            public int imagenes;
            public FileDto[] archivos;
            public FileDto intentos;
            public int filas_intentos;
            public int omitidas;
            public bool completo;
            public string motivo;
            public double recorrido_m = -1;
            public bool recorrido_corto;
        }

        public static string Sha256Hex(byte[] data)
        {
            using var sha = SHA256.Create();
            return ToHex(sha.ComputeHash(data));
        }

        public static string Sha256HexOfFile(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return ToHex(sha.ComputeHash(stream));
        }

        static string ToHex(byte[] hash)
        {
            var s = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
                s.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return s.ToString();
        }

        /// <summary>Lines of the metadata file that hold something (a row per image).</summary>
        public static int CountRows(string path) =>
            File.Exists(path) ? File.ReadLines(path).Count(l => l.Trim().Length > 0) : 0;

        /// <summary>A file of the folder as it is on disk now (size and hash); size 0 and no hash when it is missing.</summary>
        public static PaseoManifestFile DescribeFile(string path) =>
            File.Exists(path)
                ? new PaseoManifestFile(Path.GetFileName(path), new FileInfo(path).Length, Sha256HexOfFile(path))
                : new PaseoManifestFile(Path.GetFileName(path), 0, "");

        public static string ToJson(PaseoManifestData d)
        {
            var s = new StringBuilder(512 + d.Images.Count * 120);
            s.Append("{\n");
            s.Append("  \"formato\": ").Append(Format).Append(",\n");
            s.Append("  \"carpeta\": ").Append(PaseoRecorder.Quote(d.Folder)).Append(",\n");
            s.Append("  \"tipo_captura\": ").Append(PaseoRecorder.Quote(d.CaptureKind)).Append(",\n");
            s.Append("  \"hora\": ").Append(PaseoRecorder.Quote(d.Time.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture))).Append(",\n");
            s.Append("  \"imagenes\": ").Append(d.Images.Count).Append(",\n");
            s.Append("  \"archivos\": [");
            for (int i = 0; i < d.Images.Count; i++)
                s.Append(i == 0 ? "\n    " : ",\n    ").Append(FileJson(d.Images[i]));
            s.Append(d.Images.Count > 0 ? "\n  ],\n" : "],\n");
            s.Append("  \"intentos\": ").Append(FileJson(d.Rows)).Append(",\n");
            s.Append("  \"filas_intentos\": ").Append(d.RowCount).Append(",\n");
            s.Append("  \"omitidas\": ").Append(d.Dropped).Append(",\n");
            s.Append("  \"completo\": ").Append(d.Complete ? "true" : "false").Append(",\n");
            s.Append("  \"motivo\": ").Append(PaseoRecorder.Quote(d.StopReason ?? ""));
            if (!double.IsNaN(d.TravelMeters))
            {
                s.Append(",\n  \"recorrido_m\": ").Append(d.TravelMeters.ToString("0.###", CultureInfo.InvariantCulture));
                s.Append(",\n  \"recorrido_corto\": ").Append(d.ShortTravel ? "true" : "false");
            }
            s.Append("\n}\n");
            return s.ToString();
        }

        static string FileJson(PaseoManifestFile f) =>
            "{\"archivo\": " + PaseoRecorder.Quote(f.Name) + ", \"bytes\": " + f.Bytes.ToString(CultureInfo.InvariantCulture) +
            ", \"sha256\": " + PaseoRecorder.Quote(f.Sha256 ?? "") + "}";

        /// <summary>Writes manifiesto.json next to the images: temporary file first, then the rename.</summary>
        public static void Write(string folder, PaseoManifestData data)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, FileName);
            string temporary = path + TemporarySuffix;
            File.WriteAllText(temporary, ToJson(data), new UTF8Encoding(false));
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temporary, path, null);
                    return;
                }
                catch (Exception)
                {
                    File.Delete(path);
                }
            }
            File.Move(temporary, path);
        }

        /// <summary>
        /// Compares the folder with its manifest: every listed file there with the same size (and SHA-256 when
        /// <paramref name="hash"/>), no unlisted files, the rows of intentos.jsonl as many as the images, and a capture
        /// that ended normally. No manifest is its own verdict: the folder was saved by an older version, or the app died first.
        /// </summary>
        public static PaseoManifestCheck Verify(string folder, bool hash = true)
        {
            string path = Path.Combine(folder, FileName);
            if (!File.Exists(path))
                return new PaseoManifestCheck(PaseoManifestVerdict.NoManifest, "sin manifiesto");
            ManifestDto dto;
            try
            {
                dto = JsonUtility.FromJson<ManifestDto>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception)
            {
                dto = null;
            }
            if (dto == null || dto.archivos == null || dto.formato <= 0)
                return new PaseoManifestCheck(PaseoManifestVerdict.Incomplete, "manifiesto ilegible");

            var problems = new List<string>();
            if (!dto.completo)
                problems.Add("captura detenida" + (string.IsNullOrEmpty(dto.motivo) ? "" : ": " + dto.motivo));
            if (dto.archivos.Length != dto.imagenes)
                problems.Add($"el manifiesto lista {dto.archivos.Length} de {dto.imagenes} imágenes");
            var listed = new HashSet<string>(StringComparer.Ordinal) { FileName, PaseoRecordings.CopiedFile };
            // The image manifest and the ARCore session bundle have separate inventories.
            // Keep rejecting unlisted images; the bundle hashes these known auxiliary files in sha256.txt.
            if (File.Exists(Path.Combine(folder, "sesion.json")))
                listed.UnionWith(new[] { "sesion.json", "registro.log", PaseoSessionVideo.FileName, "sha256.txt" });
            foreach (var entry in dto.archivos)
            {
                listed.Add(entry.archivo ?? "");
                CheckFile(folder, entry, hash, problems);
            }
            if (dto.intentos != null && !string.IsNullOrEmpty(dto.intentos.archivo))
            {
                listed.Add(dto.intentos.archivo);
                CheckFile(folder, dto.intentos, hash, problems);
                int rows = CountRows(Path.Combine(folder, dto.intentos.archivo));
                if (rows != dto.filas_intentos)
                    problems.Add($"{rows} filas en {dto.intentos.archivo}, el manifiesto dice {dto.filas_intentos}");
            }
            else if (dto.imagenes > 0)
                problems.Add("falta intentos.jsonl en el manifiesto");
            if (dto.filas_intentos != dto.imagenes)
                problems.Add($"{dto.filas_intentos} filas para {dto.imagenes} imágenes");
            foreach (string file in Directory.GetFiles(folder))
            {
                string name = Path.GetFileName(file);
                if (!listed.Contains(name) && !name.EndsWith(TemporarySuffix, StringComparison.Ordinal))
                    problems.Add($"{name} no está en el manifiesto");
            }
            return problems.Count == 0
                ? new PaseoManifestCheck(PaseoManifestVerdict.Complete, "")
                : new PaseoManifestCheck(PaseoManifestVerdict.Incomplete, string.Join("; ", problems.Take(4)));
        }

        static void CheckFile(string folder, FileDto entry, bool hash, List<string> problems)
        {
            string path = Path.Combine(folder, entry.archivo ?? "");
            if (!File.Exists(path))
            {
                problems.Add($"falta {entry.archivo}");
                return;
            }
            long size = new FileInfo(path).Length;
            if (size != entry.bytes)
                problems.Add($"{entry.archivo} pesa {size} y debía pesar {entry.bytes}");
            else if (hash && !string.Equals(Sha256HexOfFile(path), entry.sha256, StringComparison.OrdinalIgnoreCase))
                problems.Add($"{entry.archivo} no coincide con su SHA-256");
        }

        /// <summary>
        /// Verifies each folder. A folder in <paramref name="copyFailed"/> (a file did not reach Descargas) counts as incomplete
        /// whatever its manifest says.
        /// </summary>
        public static PaseoCopyReport CompareFolders(IEnumerable<string> folders, ISet<string> copyFailed = null, bool hash = true)
        {
            var report = new PaseoCopyReport();
            foreach (string folder in folders)
            {
                report.Folders++;
                var check = Verify(folder, hash);
                bool failed = copyFailed != null && copyFailed.Contains(folder);
                if (check.Verdict == PaseoManifestVerdict.NoManifest)
                    report.NoManifest++;
                else if (check.Verdict == PaseoManifestVerdict.Complete && !failed)
                    report.Complete++;
                else
                {
                    report.Incomplete++;
                    report.IncompleteFolders.Add(Path.GetFileName(folder));
                }
            }
            return report;
        }
    }
}
