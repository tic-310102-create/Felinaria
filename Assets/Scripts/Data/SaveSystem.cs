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

                // Escribir el archivo directamente en disco.
                File.WriteAllText(RutaArchivo, json);

                var posPlayer = datos.Unidades != null ? datos.Unidades.Find(u => u.Bando == 0) : null;
                if (posPlayer != null)
                {
                    Debug.Log($"[SaveSystem] Guardando archivo JSON en: {Application.persistentDataPath} | Posición jugador: ({posPlayer.Columna}, {posPlayer.Fila}) | MP: {posPlayer.ManaActual}");
                }

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
                var posPlayer = datos.Unidades != null ? datos.Unidades.Find(u => u.Bando == 0) : null;
                if (posPlayer != null)
                {
                    Debug.Log($"[SaveSystem] Cargando archivo JSON | Posición leída: ({posPlayer.Columna}, {posPlayer.Fila}) | MP leído: {posPlayer.ManaActual}");
                }

                // Guardar el tiempo acumulado para continuar contando.
                _tiempoAcumuladoPrevio = datos.TiempoJugadoSegundos;
                _tiempoInicioSesion = Time.realtimeSinceStartup;

                // Aplicar estado inmediatamente a la escena (unidades, HP, posiciones, ocupación)
                AplicarEstadoCargado(datos);

                Debug.Log($"[SaveSystem] ✅ Partida cargada y aplicada exitosamente.");
                Debug.Log($"[SaveSystem] {datos}");
            }
            else
            {
                Debug.Log("[SaveSystem] No se encontró archivo de guardado.");
            }

            return datos;
        }

        /// <summary>
        /// Aplica los datos cargados directamente a la escena en tiempo real:
        /// restaura posiciones de unidades, vida, ocupación del grid y estado de ronda.
        /// </summary>
        public static void AplicarEstadoCargado(GameData datos)
        {
            if (datos == null) return;

            // 1. Limpiar ocupación previa en el GridManager
            if (GridManager.Instancia != null)
            {
                for (int c = 0; c < GridManager.Instancia.Columnas; c++)
                {
                    for (int f = 0; f < GridManager.Instancia.Filas; f++)
                    {
                        GridManager.Instancia.SetOcupacion(c, f, false);
                    }
                }
            }

            // 2. Restaurar TurnManager
            if (TurnManager.Instancia != null)
            {
                TurnManager.Instancia.RondaActual = datos.RondaActual;
            }

            // 3. Buscar todas las unidades en la escena
            var unidadesEnEscena = UnityEngine.Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None);
            var unidadesNoEmparejadas = new List<UnitController>(unidadesEnEscena);

            if (datos.Unidades != null)
            {
                foreach (var datosU in datos.Unidades)
                {
                    if (datosU == null) continue;

                    UnitController unidadEncontrada = null;

                    // 1. Prioridad: Coincidir por BANDO y NOMBRE
                    for (int i = 0; i < unidadesNoEmparejadas.Count; i++)
                    {
                        if (unidadesNoEmparejadas[i] != null &&
                            (int)unidadesNoEmparejadas[i].BandoUnidad == datosU.Bando &&
                            unidadesNoEmparejadas[i].NombreUnidad == datosU.NombreUnidad)
                        {
                            unidadEncontrada = unidadesNoEmparejadas[i];
                            unidadesNoEmparejadas.RemoveAt(i);
                            break;
                        }
                    }

                    // 2. Fallback: Coincidir por BANDO
                    if (unidadEncontrada == null)
                    {
                        for (int i = 0; i < unidadesNoEmparejadas.Count; i++)
                        {
                            if (unidadesNoEmparejadas[i] != null && (int)unidadesNoEmparejadas[i].BandoUnidad == datosU.Bando)
                            {
                                unidadEncontrada = unidadesNoEmparejadas[i];
                                unidadesNoEmparejadas.RemoveAt(i);
                                break;
                            }
                        }
                    }

                    // 3. Fallback: Coincidir por NOMBRE
                    if (unidadEncontrada == null)
                    {
                        for (int i = 0; i < unidadesNoEmparejadas.Count; i++)
                        {
                            if (unidadesNoEmparejadas[i] != null && unidadesNoEmparejadas[i].NombreUnidad == datosU.NombreUnidad)
                            {
                                unidadEncontrada = unidadesNoEmparejadas[i];
                                unidadesNoEmparejadas.RemoveAt(i);
                                break;
                            }
                        }
                    }

                    if (unidadEncontrada != null)
                    {
                        unidadEncontrada.RestaurarEstado(
                            datosU.Columna,
                            datosU.Fila,
                            datosU.VidaActual,
                            datosU.VidaMaxima,
                            datosU.ManaActual,
                            datosU.ManaMaximo,
                            datosU.YaActuoEsteTurno,
                            datosU.EstaViva
                        );

                        // Marcar celda ocupada si la unidad sigue viva
                        if (datosU.EstaViva && datosU.VidaActual > 0 && GridManager.Instancia != null)
                        {
                            GridManager.Instancia.SetOcupacion(datosU.Columna, datosU.Fila, true);
                        }
                    }
                }
            }

            // 4. Cerrar cualquier ActionMenu abierto al cargar
            if (Felinaria.UI.ActionMenu.InstanciaExiste)
            {
                Felinaria.UI.ActionMenu.Instancia.CerrarMenu();
            }

            // 5. Restaurar configuración de audio si existe
            if (datos.ConfiguracionAudio != null && Felinaria.Audio.AudioManager.Instancia != null)
            {
                Felinaria.Audio.AudioManager.Instancia.SetVolumenMusica(datos.ConfiguracionAudio.VolumenMusica);
                Felinaria.Audio.AudioManager.Instancia.SetVolumenSFX(datos.ConfiguracionAudio.VolumenSFX);
            }

            Debug.Log($"[SaveSystem] ✅ Sincronización visual y lógica de carga completada.");
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

            // ── TurnManager y Unidades ──────────────────────────────────────────
            var unidadesRecolectadas = new HashSet<UnitController>();
            if (TurnManager.Instancia != null)
            {
                datos.RondaActual = TurnManager.Instancia.RondaActual;
                datos.EstadoTurno = (int)TurnManager.Instancia.EstadoActual;

                // Recopilar unidades del jugador.
                foreach (var unidad in TurnManager.Instancia.UnidadesJugador)
                {
                    if (unidad != null && unidadesRecolectadas.Add(unidad))
                        datos.Unidades.Add(RecopilarDatosUnidad(unidad));
                }

                // Recopilar unidades enemigas.
                foreach (var unidad in TurnManager.Instancia.UnidadesEnemigo)
                {
                    if (unidad != null && unidadesRecolectadas.Add(unidad))
                        datos.Unidades.Add(RecopilarDatosUnidad(unidad));
                }
            }

            // Fallback para asegurar que todas las unidades presentes en la escena se guarden
            var todasLasUnidades = UnityEngine.Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None);
            foreach (var unidad in todasLasUnidades)
            {
                if (unidad != null && unidadesRecolectadas.Add(unidad))
                {
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
                ManaActual        = unidad.ManaActual,
                ManaMaximo        = unidad.ManaMaximo,
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
