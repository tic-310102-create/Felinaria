// ============================================================
//  SaveSystem.cs
//  Felinaria: El último presagio
//  Fase 4 – Persistencia y Pulido
//
//  RESPONSABILIDAD:
//    - Serializa GameData a JSON usando JsonUtility.
//    - Guarda el JSON en Application.persistentDataPath.
//    - Carga el JSON y lo deserializa de vuelta a GameData.
//    - Recopila el estado actual del juego (unidades, mapa, turno)
//      para construir un GameData actualizado antes de guardar.
//    - Expone API estática: SaveSystem.GuardarPartida(), SaveSystem.CargarPartida().
//
//  ¿DÓNDE SE GUARDA? (para principiantes):
//    Application.persistentDataPath es una carpeta especial que Unity
//    asigna para cada juego en cada plataforma:
//      Windows: C:/Users/<usuario>/AppData/LocalLow/<company>/<product>/
//      Android: /data/data/<package>/files/
//    Los archivos en esta carpeta persisten entre sesiones de juego.
// ============================================================

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Felinaria.Data;
using Felinaria.Units;
using Felinaria.Grid;
using Felinaria.Managers;

namespace Felinaria.Data
{
    /// <summary>
    /// Sistema estático de guardado/carga de partidas.
    /// No necesita ser MonoBehaviour. Se llama directamente:
    ///   SaveSystem.GuardarPartida();
    ///   GameData datos = SaveSystem.CargarPartida();
    /// </summary>
    public static class SaveSystem
    {
        // ── Configuración ──────────────────────────────────────────────────────
        /// <summary>Nombre del archivo de guardado principal.</summary>
        private const string NOMBRE_ARCHIVO = "felinaria_save.json";

        /// <summary>Nombre del archivo de guardado de respaldo.</summary>
        private const string NOMBRE_ARCHIVO_BACKUP = "felinaria_save_backup.json";

        /// <summary>Versión actual del esquema de datos.</summary>
        private const int VERSION_ESQUEMA = 1;

        // ── Tiempo de juego acumulado ──────────────────────────────────────────
        // Se actualiza en cada guardado para llevar el tiempo total jugado.
        private static float _tiempoInicioSesion = -1f;
        private static float _tiempoAcumuladoPrevio = 0f;

        // ── API pública ────────────────────────────────────────────────────────

        /// <summary>
        /// Ruta completa del archivo de guardado.
        /// </summary>
        public static string RutaArchivo
            => Path.Combine(Application.persistentDataPath, NOMBRE_ARCHIVO);

        /// <summary>
        /// Ruta completa del archivo de respaldo.
        /// </summary>
        public static string RutaArchivoBackup
            => Path.Combine(Application.persistentDataPath, NOMBRE_ARCHIVO_BACKUP);

        /// <summary>
        /// Guarda la partida actual recopilando el estado del juego.
        /// Crea un backup del archivo anterior antes de sobreescribir.
        /// </summary>
        /// <returns>True si el guardado fue exitoso.</returns>
        public static bool GuardarPartida()
        {
            try
            {
                // Recopilar datos del estado actual del juego.
                GameData datos = RecopilarEstadoActual();

                // Serializar a JSON con formato legible.
                string json = JsonUtility.ToJson(datos, true);

                // Crear backup del archivo anterior (si existe).
                CrearBackup();

                // Escribir el archivo.
                File.WriteAllText(RutaArchivo, json);

                Debug.Log($"[SaveSystem] ✅ Partida guardada exitosamente.");
                Debug.Log($"[SaveSystem] Ruta: {RutaArchivo}");
                Debug.Log($"[SaveSystem] Tamaño: {json.Length} bytes | {datos}");

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] ❌ Error al guardar: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Carga la partida guardada desde el archivo JSON.
        /// Si el archivo principal está corrupto, intenta cargar el backup.
        /// </summary>
        /// <returns>GameData cargado, o null si no existe archivo.</returns>
        public static GameData CargarPartida()
        {
            // Intentar cargar el archivo principal.
            GameData datos = CargarDesdeArchivo(RutaArchivo);

            // Si falló, intentar el backup.
            if (datos == null && File.Exists(RutaArchivoBackup))
            {
                Debug.LogWarning("[SaveSystem] Archivo principal corrupto. Cargando backup...");
                datos = CargarDesdeArchivo(RutaArchivoBackup);
            }

            if (datos != null)
            {
                // Guardar el tiempo acumulado para continuar contando.
                _tiempoAcumuladoPrevio = datos.TiempoJugadoSegundos;
                _tiempoInicioSesion = Time.realtimeSinceStartup;

                Debug.Log($"[SaveSystem] ✅ Partida cargada exitosamente.");
                Debug.Log($"[SaveSystem] {datos}");
            }
            else
            {
                Debug.Log("[SaveSystem] No se encontró archivo de guardado.");
            }

            return datos;
        }

        /// <summary>
        /// Indica si existe un archivo de guardado.
        /// </summary>
        public static bool ExistePartidaGuardada()
            => File.Exists(RutaArchivo);

        /// <summary>
        /// Elimina los archivos de guardado (para "nueva partida").
        /// </summary>
        public static void EliminarPartidaGuardada()
        {
            try
            {
                if (File.Exists(RutaArchivo))
                    File.Delete(RutaArchivo);
                if (File.Exists(RutaArchivoBackup))
                    File.Delete(RutaArchivoBackup);

                _tiempoAcumuladoPrevio = 0f;
                _tiempoInicioSesion = Time.realtimeSinceStartup;

                Debug.Log("[SaveSystem] Archivos de guardado eliminados.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Error al eliminar: {ex.Message}");
            }
        }

        /// <summary>
        /// Devuelve el JSON crudo del archivo de guardado (para debug o envío a la nube).
        /// </summary>
        public static string ObtenerJSONGuardado()
        {
            if (!File.Exists(RutaArchivo)) return null;
            return File.ReadAllText(RutaArchivo);
        }

        // ── Recopilación del estado del juego ──────────────────────────────────

        /// <summary>
        /// Construye un GameData con el estado actual del juego:
        /// unidades, mapa, turno, tiempo, etc.
        /// </summary>
        private static GameData RecopilarEstadoActual()
        {
            var datos = new GameData();

            // ── Tiempo de juego ────────────────────────────────────────────────
            if (_tiempoInicioSesion < 0)
                _tiempoInicioSesion = Time.realtimeSinceStartup;

            float tiempoSesion = Time.realtimeSinceStartup - _tiempoInicioSesion;
            datos.TiempoJugadoSegundos = _tiempoAcumuladoPrevio + tiempoSesion;
            datos.FechaGuardado = DateTime.Now.ToString("o");
            datos.VersionEsquema = VERSION_ESQUEMA;

            // ── Escena activa ──────────────────────────────────────────────────
            datos.NombreEscena = SceneManager.GetActiveScene().name;

            // ── TurnManager ────────────────────────────────────────────────────
            if (TurnManager.Instancia != null)
            {
                datos.RondaActual = TurnManager.Instancia.RondaActual;
                datos.EstadoTurno = (int)TurnManager.Instancia.EstadoActual;

                // Recopilar unidades del jugador.
                foreach (var unidad in TurnManager.Instancia.UnidadesJugador)
                {
                    if (unidad == null) continue;
                    datos.Unidades.Add(RecopilarDatosUnidad(unidad));
                }

                // Recopilar unidades enemigas.
                foreach (var unidad in TurnManager.Instancia.UnidadesEnemigo)
                {
                    if (unidad == null) continue;
                    datos.Unidades.Add(RecopilarDatosUnidad(unidad));
                }
            }

            // ── GridManager ────────────────────────────────────────────────────
            if (GridManager.Instancia != null)
            {
                datos.ConfiguracionMapa = new DatosMapaGuardado
                {
                    NombreMapa   = datos.NombreEscena,
                    Columnas     = GridManager.Instancia.Columnas,
                    Filas        = GridManager.Instancia.Filas,
                    TamanioCelda = GridManager.Instancia.TamanioCelda
                };

                // Guardar asignaciones de terreno.
                foreach (var asignacion in GridManager.Instancia.AsignacionesTerreno)
                {
                    if (asignacion.Terreno == null) continue;
                    datos.ConfiguracionMapa.TerrenosEspeciales.Add(new DatosTerrenoGuardado
                    {
                        Columna       = asignacion.Coordenada.x,
                        Fila          = asignacion.Coordenada.y,
                        NombreTerreno = asignacion.Terreno.name
                    });
                }
            }

            // ── AudioManager ───────────────────────────────────────────────────
            if (Felinaria.Audio.AudioManager.Instancia != null)
            {
                var audio = Felinaria.Audio.AudioManager.Instancia;
                datos.ConfiguracionAudio = new DatosAudioGuardado
                {
                    VolumenMusica    = audio.VolumenMusica,
                    VolumenSFX       = audio.VolumenSFX,
                    MusicaSilenciada = audio.MusicaSilenciada,
                    SFXSilenciados   = audio.SFXSilenciados
                };
            }

            return datos;
        }

        /// <summary>
        /// Extrae los datos relevantes de una unidad para el guardado.
        /// </summary>
        private static DatosUnidadGuardada RecopilarDatosUnidad(UnitController unidad)
        {
            return new DatosUnidadGuardada
            {
                NombreUnidad      = unidad.NombreUnidad,
                Bando             = (int)unidad.BandoUnidad,
                NombreFichaStats  = unidad.FichaStats != null ? unidad.FichaStats.name : "",
                VidaActual        = unidad.VidaActual,
                VidaMaxima        = unidad.VidaMaxima,
                Ataque            = unidad.Ataque,
                Defensa           = unidad.Defensa,
                RangoMovimiento   = unidad.RangoMovimiento,
                Columna           = unidad.Coordenada.x,
                Fila              = unidad.Coordenada.y,
                YaActuoEsteTurno  = unidad.YaActuoEsteTurno,
                EstaViva          = unidad.VidaActual > 0
            };
        }

        // ── Utilidades internas ────────────────────────────────────────────────

        /// <summary>
        /// Carga y deserializa un GameData desde un archivo específico.
        /// </summary>
        private static GameData CargarDesdeArchivo(string ruta)
        {
            try
            {
                if (!File.Exists(ruta)) return null;

                string json = File.ReadAllText(ruta);

                if (string.IsNullOrWhiteSpace(json))
                {
                    Debug.LogWarning($"[SaveSystem] Archivo vacío: {ruta}");
                    return null;
                }

                GameData datos = JsonUtility.FromJson<GameData>(json);
                return datos;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Error al cargar '{ruta}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Crea una copia de respaldo del archivo principal antes de sobreescribir.
        /// </summary>
        private static void CrearBackup()
        {
            try
            {
                if (File.Exists(RutaArchivo))
                {
                    File.Copy(RutaArchivo, RutaArchivoBackup, overwrite: true);
                    Debug.Log("[SaveSystem] Backup creado.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] No se pudo crear backup: {ex.Message}");
            }
        }
    }
}
