using System;

namespace AncorRA.AR
{
    [Serializable]
    public sealed class PaseoCaptureShot
    {
        public string Id;
        public string Title;
        public string Role;
        public string Instructions;
        public int Seconds;
        public int Meters;
        public bool Construction => Role == "construccion";

        public PaseoCaptureShot(string id, string title, string role, int seconds, int meters, string instructions)
        {
            Id = id; Title = title; Role = role; Seconds = seconds; Meters = meters; Instructions = instructions;
        }
    }

    /// <summary>Development captures and reserved queries have explicit roles, independent of filename point labels.</summary>
    public static class PaseoCapturePlan
    {
        public static readonly PaseoCaptureShot[] Shots =
        {
            new("00-prueba-casa", "Prueba en casa", "ensayo", 10, 10,
                "Antes de salir: apunta a una habitación con detalles y camina unos pasos despacio. Después reproduce esta toma y copia todo a Descargas. Esta toma no construye el campus."),
            new("01-frente-ida", "Frente y esquina · ida", "construccion", 60, 15,
                "Camina por la vereda desde un extremo del frente al otro y continúa por la esquina accesible. Mantén fachada, puerta y ventanas en imagen; al girar, muestra ambas caras. Empieza cuando haya fotos guardadas."),
            new("02-frente-vuelta", "Frente y esquina · vuelta", "construccion", 60, 15,
                "Regresa por la vereda con una posición lateral algo distinta. Camina despacio, sin girar solo desde un punto. Si no cabe en 60 s, repite la toma desde el final anterior con solapamiento."),
            new("03-lateral-ranuras", "Lateral · dos franjas de ventanas", "construccion", 60, 15,
                "Graba el lateral de muro blanco con dos franjas horizontales de ventanas y el poste. Desplázate para dejar ambas franjas y sus extremos visibles sin el poste delante. Incluye la esquina para conectar con el frente."),
            new("04-ventana-puerta", "Medida · ventana junto a puerta", "detalle", 15, 3,
                "Ventana de 2,70 m junto a la puerta: muestra primero la ventana completa y luego ambos bordes interiores medidos, con desplazamiento suave. Si hay otra persona, que señale los extremos. No tapes los dos a la vez. La distancia es aproximada."),
            new("05-ventana-lateral", "Medida · primera ventana lateral", "detalle", 15, 3,
                "Primera ventana baja a la derecha de la esquina: muestra sus bordes interiores (2,70 m) y la ventana de 1,95 m al volver la esquina. Incluye contexto y vistas laterales; evita que el alero o un poste oculten los bordes."),
            new("05b-ventana-larga", "Medida · ventana larga de 6,20 m", "detalle", 15, 5,
                "Muestra la ventana larga baja de 6,20 m completa y después sus dos bordes interiores medidos. Mantén una referencia de contexto y desplázate lentamente. Anota segundas lecturas y altura de cinta en Notas, si las haces."),
            new("06-consulta-10", "Consulta separada · 10 m", "consulta", 5, 10,
                "Desde una posición nueva a unos 10 m: quédate en el lugar y barre suavemente la fachada. Esta sesión se conserva fuera de construcción. La etiqueta de distancia no es una medida de precisión."),
            new("07-consulta-15", "Consulta separada · 15 m", "consulta", 5, 15,
                "Desde otra posición a unos 15 m, con un ángulo distinto: barrido suave sin caminar. No esperar a que aparezca una caja. Mantén detalles del edificio en imagen."),
            new("08-consulta-25", "Consulta separada · 25 m", "consulta", 5, 25,
                "Desde una posición accesible a unos 25 m: barrido suave. Anota si no puedes alcanzar esa distancia y elige la aproximada real. No cruces barandas para completar la toma."),
            new("09-movimiento", "Movimiento y regreso", "desarrollo", 30, 15,
                "Apunta al edificio, camina lateralmente unos metros y vuelve. Incluye giros suaves y una pausa de 2 s al principio y al final. Sirve para diagnosticar estabilidad; no calibra por sí solo la sincronía."),
            new("10-negativa", "Otro edificio · consulta negativa", "negativa", 10, 15,
                "Apunta a otro edificio o una zona donde NO aparezca Teología. Barrido suave. Anota qué edificio se ve. Estas imágenes no se añaden al mapa de Teología."),
            new("11-reserva", "Consulta reservada · otro ángulo", "reserva", 10, 20,
                "Escoge otra posición y ángulo accesibles, distintos de los anteriores. Barrido suave. Se guarda como reserva, fuera del ajuste y la construcción. Anota la luz real; repetir con otra luz es opcional.")
        };
    }
}
