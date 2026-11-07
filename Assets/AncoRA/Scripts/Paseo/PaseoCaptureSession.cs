using System;
using System.IO;
using UnityEngine;

namespace AncorRA.AR
{
    [Serializable]
    public sealed class PaseoCaptureSession
    {
        public int formato = 1;
        public string id, toma, titulo, rol, instrucciones, edificio, luz, notas, inicio, fin, estado, motivo;
        public string telefono, sistema, version, build, registro_origen;
        public int segundos_objetivo, distancia_referencia_aproximada_m, fotos_guardadas, fotos_omitidas;
        public bool video_solicitado, video_cerrado, pose_optica_validada = false, timestamp_pose_independiente = false;
        public string video = "", error_video = "";
        public long video_bytes;
        public string imagenes = "PNG R8 originales sin giro; intrínsecos y poses Unity en intentos.jsonl";
        public string limites = "Distancia de contexto, no control métrico. Pose Unity sin sincronía óptica validada. Reproducción ARCore no determinista. Consulta/reserva/negativa fuera del mapa.";

        public bool Complete => estado == "completa" && fotos_guardadas > 0 && (!video_solicitado || video_cerrado);

        public void Save(string folder)
        {
            // Replace only our own manifest; keep the previous complete file if a write fails.
            string path = Path.Combine(folder, "sesion.json");
            string pending = path + ".tmp";
            File.WriteAllText(pending, JsonUtility.ToJson(this, true));
            if (File.Exists(path)) File.Replace(pending, path, null);
            else File.Move(pending, path);
        }

        public static void AttachLog(string folder, string source)
        {
            if (string.IsNullOrEmpty(source) || !File.Exists(source)) return;
            using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var output = File.Create(Path.Combine(folder, "registro.log"));
            input.CopyTo(output);
        }
    }
}
