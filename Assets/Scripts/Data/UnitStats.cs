// ============================================================
//  UnitStats.cs
//  Felinaria: El último presagio
//  Fase 2 – Combate y Stats
//
//  RESPONSABILIDAD:
//    - ScriptableObject que define las estadísticas base de un tipo
//      de unidad (héroe, guardia, capitán, etc.).
//    - Los diseñadores crean "fichas" de personaje desde el Inspector
//      sin tocar código: clic derecho → Create → Felinaria → UnitStats.
//    - UnitController lee estos datos al inicializarse.
//
//  ¿QUÉ ES UN ScriptableObject? (para principiantes):
//    Es un archivo de datos que vive en la carpeta Assets.
//    Puedes crear múltiples instancias (una por personaje)
//    y asignarlas en el Inspector como si fueran "fichas de rol".
//    Si cambias un valor en la ficha, TODAS las unidades que la usen
//    se actualizan automáticamente. Ideal para balanceo.
// ============================================================

using UnityEngine;

namespace Felinaria.Data
{
    /// <summary>
    /// Ficha de estadísticas base de una unidad.
    /// Los diseñadores crean instancias desde:
    ///   Assets → Create → Felinaria → Unit Stats
    /// </summary>
    [CreateAssetMenu(
        fileName = "NuevoPersonaje_Stats",
        menuName = "Felinaria/Unit Stats",
        order    = 0
    )]
    public class UnitStats : ScriptableObject
    {
        // ── Identidad ──────────────────────────────────────────────────────────
        [Header("Identidad del Personaje")]
        [Tooltip("Nombre que aparecerá en la interfaz del juego.")]
        public string NombrePersonaje = "Unidad";

        [Tooltip("Descripción breve para el menú de unidades. " +
                 "Ejemplo: 'Líder gatuno del pelotón de reconocimiento'.")]
        [TextArea(2, 4)]
        public string Descripcion = "";

        [Tooltip("Ícono del personaje para UI (dejar vacío en Graybox).")]
        public Sprite Icono;

        // ── Estadísticas de Combate ────────────────────────────────────────────
        [Header("Puntos de Vida")]
        [Tooltip("Puntos de Vida máximos en nivel 1.")]
        [Range(1, 999)]
        public int VidaMaxima = 30;

        [Header("Ofensiva")]
        [Tooltip("Daño base de ataque físico.")]
        [Range(0, 99)]
        public int Ataque = 8;

        [Tooltip("Daño base de ataque mágico (para futuras habilidades).")]
        [Range(0, 99)]
        public int AtaqueMagico = 0;

        [Header("Defensiva")]
        [Tooltip("Reducción de daño físico recibido.")]
        [Range(0, 99)]
        public int Defensa = 3;

        [Tooltip("Reducción de daño mágico recibido.")]
        [Range(0, 99)]
        public int DefensaMagica = 0;

        // ── Movilidad ──────────────────────────────────────────────────────────
        [Header("Movilidad")]
        [Tooltip("Celdas que puede recorrer por turno.")]
        [Range(1, 10)]
        public int RangoMovimiento = 3;

        [Tooltip("Velocidad visual del desplazamiento (unidades/segundo).")]
        [Range(1f, 20f)]
        public float VelocidadMovimiento = 6f;

        // ── Combate a distancia ────────────────────────────────────────────────
        [Header("Rango de Ataque")]
        [Tooltip("Distancia mínima (en celdas Manhattan) para atacar. " +
                 "1 = cuerpo a cuerpo.")]
        [Range(1, 5)]
        public int RangoAtaqueMinimo = 1;

        [Tooltip("Distancia máxima (en celdas Manhattan) para atacar. " +
                 "1 = cuerpo a cuerpo, 2+ = distancia.")]
        [Range(1, 5)]
        public int RangoAtaqueMaximo = 1;

        // ── Precisión y Críticos (preparado para Fase 3+) ─────────────────────
        [Header("Precisión y Críticos (futuro)")]
        [Tooltip("Probabilidad base de acertar (0-100).")]
        [Range(0, 100)]
        public int Precision = 90;

        [Tooltip("Probabilidad base de golpe crítico (0-100).")]
        [Range(0, 100)]
        public int CriticoChance = 5;

        [Tooltip("Multiplicador de daño en golpe crítico.")]
        [Range(1f, 4f)]
        public float CriticoMultiplicador = 1.5f;

        // ── Visualización Graybox ──────────────────────────────────────────────
        [Header("Graybox Visual")]
        [Tooltip("Color del sprite Graybox para esta unidad.")]
        public Color ColorGraybox = Color.white;

        [Tooltip("Color del sprite cuando la unidad ya actuó este turno.")]
        public Color ColorUsado = new Color(0.5f, 0.5f, 0.5f, 1f);
    }
}
