// ============================================================
//  GameData.cs
//  Felinaria: El último presagio
//  Fase 4 – Persistencia y Pulido
//
//  RESPONSABILIDAD:
//    - Modelo de datos puro (POCO) que representa el estado completo
//      de una partida guardada.
//    - Todas las clases son [Serializable] para que JsonUtility
//      pueda convertirlas a JSON y viceversa.
//    - Este es el "contrato" entre el juego local y la futura
//      sincronización con Firebase Firestore.
//
//  ¿QUÉ ES UN POCO? (para principiantes):
//    "Plain Old C# Object" — una clase simple sin herencia de Unity
//    (no es MonoBehaviour ni ScriptableObject). Solo almacena datos.
//    JsonUtility de Unity puede serializar/deserializar estas clases
//    siempre que estén marcadas con [System.Serializable].
//
//  ESTRUCTURA DEL JSON RESULTANTE:
//    {
//      "IdJugador": "jugador_001",
//      "NombreJugador": "CapitánRastrojo",
//      "NivelCampania": 3,
//      "FechaGuardado": "2026-09-24T19:30:00",
//      "TiempoJugadoSegundos": 1200.5,
//      "Unidades": [ { ... }, { ... } ],
//      "ConfiguracionMapa": { ... }
//    }
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Felinaria.Data
{
    // ─── Datos de una unidad guardada ─────────────────────────────────────────
    /// <summary>
    /// Snapshot del estado de una unidad en el momento del guardado.
    /// Contiene todo lo necesario para reconstruir la unidad al cargar.
    /// </summary>
    [Serializable]
    public class DatosUnidadGuardada
    {
        /// <summary>Nombre del personaje (ej: "Capitán Rastrojo").</summary>
        public string NombreUnidad;

        /// <summary>Bando al que pertenece (0 = Jugador, 1 = Enemigo).</summary>
        public int Bando;

        /// <summary>Nombre del archivo ScriptableObject UnitStats asociado.
        /// Sirve para re-vincular los stats base al cargar.</summary>
        public string NombreFichaStats;

        // ── Estado de combate ──────────────────────────────────────────────────
        /// <summary>Vida actual de la unidad al momento de guardar.</summary>
        public int VidaActual;

        /// <summary>Vida máxima de la unidad.</summary>
        public int VidaMaxima;

        /// <summary>Maná actual de la unidad al momento de guardar.</summary>
        public int ManaActual;

        /// <summary>Maná máximo de la unidad.</summary>
        public int ManaMaximo;

        /// <summary>Ataque actual (puede haber sido modificado por buffs).</summary>
        public int Ataque;

        /// <summary>Defensa actual.</summary>
        public int Defensa;

        /// <summary>Rango de movimiento actual.</summary>
        public int RangoMovimiento;

        // ── Posición en el tablero ─────────────────────────────────────────────
        /// <summary>Columna (eje X) en la cuadrícula.</summary>
        public int Columna;

        /// <summary>Fila (eje Y) en la cuadrícula.</summary>
        public int Fila;

        // ── Estado del turno ───────────────────────────────────────────────────
        /// <summary>True si la unidad ya actuó en el turno guardado.</summary>
        public bool YaActuoEsteTurno;

        /// <summary>True si la unidad está viva.</summary>
        public bool EstaViva;
    }

    // ─── Datos de configuración del mapa ──────────────────────────────────────
    /// <summary>
    /// Información del mapa activo al momento de guardar.
    /// Permite reconstruir la cuadrícula con las mismas dimensiones y terrenos.
    /// </summary>
    [Serializable]
    public class DatosMapaGuardado
    {
        /// <summary>Nombre identificador del mapa (ej: "Batalla_01").</summary>
        public string NombreMapa;

        /// <summary>Número de columnas del tablero.</summary>
        public int Columnas;

        /// <summary>Número de filas del tablero.</summary>
        public int Filas;

        /// <summary>Tamaño de cada celda en unidades de Unity.</summary>
        public float TamanioCelda;

        /// <summary>Lista de terrenos especiales asignados a celdas específicas.</summary>
        public List<DatosTerrenoGuardado> TerrenosEspeciales;

        public DatosMapaGuardado()
        {
            TerrenosEspeciales = new List<DatosTerrenoGuardado>();
        }
    }

    // ─── Datos de un terreno asignado a una celda ────────────────────────────
    /// <summary>
    /// Par coordenada-terreno para reconstruir las asignaciones del mapa.
    /// </summary>
    [Serializable]
    public class DatosTerrenoGuardado
    {
        /// <summary>Columna de la celda.</summary>
        public int Columna;

        /// <summary>Fila de la celda.</summary>
        public int Fila;

        /// <summary>Nombre del ScriptableObject TileData asignado.</summary>
        public string NombreTerreno;
    }

    // ─── Datos de configuración de audio ──────────────────────────────────────
    /// <summary>
    /// Preferencias de audio del jugador.
    /// </summary>
    [Serializable]
    public class DatosAudioGuardado
    {
        /// <summary>Volumen de la música de fondo (0 a 1).</summary>
        public float VolumenMusica;

        /// <summary>Volumen de los efectos de sonido (0 a 1).</summary>
        public float VolumenSFX;

        /// <summary>True si la música está silenciada.</summary>
        public bool MusicaSilenciada;

        /// <summary>True si los SFX están silenciados.</summary>
        public bool SFXSilenciados;

        public DatosAudioGuardado()
        {
            VolumenMusica = 0.7f;
            VolumenSFX = 1f;
            MusicaSilenciada = false;
            SFXSilenciados = false;
        }
    }

    // ─── GameData: modelo raíz ───────────────────────────────────────────────
    /// <summary>
    /// Modelo raíz de datos de una partida guardada.
    /// JsonUtility serializa esta clase a JSON y viceversa.
    /// Este es el "contrato de datos" para la sincronización con Firebase.
    /// </summary>
    [Serializable]
    public class GameData
    {
        // ── Identificación ─────────────────────────────────────────────────────
        /// <summary>ID único del jugador (generado automáticamente la primera vez).</summary>
        public string IdJugador;

        /// <summary>Nombre elegido por el jugador.</summary>
        public string NombreJugador;

        /// <summary>Versión del esquema de datos (para migraciones futuras).</summary>
        public int VersionEsquema;

        // ── Progreso de campaña ────────────────────────────────────────────────
        /// <summary>Nivel de campaña actual (1 = primer mapa, 2 = segundo, etc.).</summary>
        public int NivelCampania;

        /// <summary>Nombre de la escena de Unity activa al guardar.</summary>
        public string NombreEscena;

        /// <summary>Número de ronda actual dentro del combate.</summary>
        public int RondaActual;

        /// <summary>Estado del turno al guardar (0=Inactivo, 1=Jugador, 2=Enemigo, etc.).</summary>
        public int EstadoTurno;

        // ── Tiempo ─────────────────────────────────────────────────────────────
        /// <summary>Fecha y hora del guardado en formato ISO 8601.</summary>
        public string FechaGuardado;

        /// <summary>Tiempo total jugado en segundos (acumulativo).</summary>
        public float TiempoJugadoSegundos;

        // ── Unidades ───────────────────────────────────────────────────────────
        /// <summary>Lista de todas las unidades (vivas y muertas) en el tablero.</summary>
        public List<DatosUnidadGuardada> Unidades;

        // ── Mapa ───────────────────────────────────────────────────────────────
        /// <summary>Configuración del mapa activo.</summary>
        public DatosMapaGuardado ConfiguracionMapa;

        // ── Audio ──────────────────────────────────────────────────────────────
        /// <summary>Preferencias de audio del jugador.</summary>
        public DatosAudioGuardado ConfiguracionAudio;

        // ── Constructor por defecto ────────────────────────────────────────────
        public GameData()
        {
            IdJugador           = System.Guid.NewGuid().ToString("N").Substring(0, 12);
            NombreJugador       = "Jugador";
            VersionEsquema      = 1;
            NivelCampania       = 1;
            NombreEscena        = "";
            RondaActual         = 1;
            EstadoTurno         = 0;
            FechaGuardado       = DateTime.Now.ToString("o");
            TiempoJugadoSegundos = 0f;
            Unidades            = new List<DatosUnidadGuardada>();
            ConfiguracionMapa   = new DatosMapaGuardado();
            ConfiguracionAudio  = new DatosAudioGuardado();
        }

        /// <summary>
        /// Devuelve una representación legible para debugging.
        /// </summary>
        public override string ToString()
        {
            return $"[GameData] Jugador: {NombreJugador} (ID: {IdJugador}) | " +
                   $"Nivel: {NivelCampania} | Ronda: {RondaActual} | " +
                   $"Unidades: {Unidades.Count} | Guardado: {FechaGuardado}";
        }
    }
}
