// ============================================================
//  MapLoader.cs
//  Felinaria: El último presagio
//  Fase 3 – Entorno y Carga de Niveles
//
//  RESPONSABILIDAD:
//    - Define estructuras serializables para batallas y mapas en JSON.
//    - Carga batallas en tiempo de ejecución (dimensiones, obstáculos, coberturas, unidades).
//    - Soporta Batalla 1 (misión inicial) y Batalla 2 (mapa ampliado con río/obstáculos y 2 enemigos).
//    - Se integra con SceneTransition para transiciones fluidas de fundido a negro.
//    - Expone API pública para BattleResultUI, BattleManager y SaveSystem.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Felinaria.Units;
using Felinaria.Managers;
using Felinaria.UI;

namespace Felinaria.Grid
{
    // ─── Estructuras serializables de configuración de Batalla ────────────────
    [Serializable]
    public class DatosTerrenoMapa
    {
        public int Col;
        public int Row;
        public TipoTerreno Tipo;

        public DatosTerrenoMapa() { }
        public DatosTerrenoMapa(int col, int row, TipoTerreno tipo)
        {
            Col = col;
            Row = row;
            Tipo = tipo;
        }
    }

    [Serializable]
    public class DatosSpawnUnidad
    {
        public string Nombre;
        public Bando Bando;
        public int Col;
        public int Row;
        public int VidaMax = 30;
        public int ManaMax = 20;
        public int Ataque = 8;
        public int Defensa = 3;
        public int Movimiento = 3;
        public int RangoAtaqueMin = 1;
        public int RangoAtaqueMax = 1;
        public Color ColorVisual = Color.white;

        public DatosSpawnUnidad() { }
        public DatosSpawnUnidad(string nombre, Bando bando, int col, int row, int hp, int mp, int atk, int def, int mov, Color color)
        {
            Nombre = nombre;
            Bando = bando;
            Col = col;
            Row = row;
            VidaMax = hp;
            ManaMax = mp;
            Ataque = atk;
            Defensa = def;
            Movimiento = mov;
            RangoAtaqueMin = 1;
            RangoAtaqueMax = 1;
            ColorVisual = color;
        }
    }

    [Serializable]
    public class DatosBatalla
    {
        public int IdBatalla;
        public string NombreBatalla;
        public int Columnas = 10;
        public int Filas = 8;
        public float TamanioCelda = 1f;
        public List<DatosTerrenoMapa> Terrenos = new List<DatosTerrenoMapa>();
        public List<DatosSpawnUnidad> Spawns = new List<DatosSpawnUnidad>();
    }

    /// <summary>
    /// Singleton que gestiona la carga y armado dinámico de niveles y batallas.
    /// </summary>
    public class MapLoader : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        private static MapLoader _instancia;
        private static bool _aplicacionCerrando = false;

        public static bool InstanciaExiste => _instancia != null && !_aplicacionCerrando;

        public static MapLoader Instancia
        {
            get
            {
                if (_aplicacionCerrando) return null;
                if (_instancia == null)
                {
                    _instancia = FindFirstObjectByType<MapLoader>();
                    if (_instancia == null && Application.isPlaying)
                    {
                        var go = new GameObject("MapLoader_Auto");
                        _instancia = go.AddComponent<MapLoader>();
                    }
                }
                return _instancia;
            }
            private set => _instancia = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInicializar()
        {
            if (!_aplicacionCerrando)
            {
                var _ = Instancia;
            }
        }

        // ── Estado ─────────────────────────────────────────────────────────────
        public int BatallaActualIndex { get; private set; } = 1;
        public DatosBatalla BatallaActiva { get; private set; }

        // Cache de plantillas de batalla
        private Dictionary<int, DatosBatalla> _batallasPredefinidas = new Dictionary<int, DatosBatalla>();

        [Header("Configuración")]
        [Tooltip("Carga automáticamente la Batalla 1 al iniciar el juego.")]
        public bool CargarAutomaticoAlIniciar = true;

        // ── Unity Lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            _aplicacionCerrando = false;
            if (_instancia != null && _instancia != this)
            {
                Destroy(gameObject);
                return;
            }
            _instancia = this;

            RegistrarBatallasPredefinidas();
        }

        private void Start()
        {
            if (CargarAutomaticoAlIniciar)
            {
                // Disparar automáticamente la Batalla 1 (8x6) en el primer frame
                CargarBatalla(1, true);
            }
        }

        private void OnApplicationQuit()
        {
            _aplicacionCerrando = true;
        }

        private void OnDestroy()
        {
            if (_instancia == this)
            {
                _instancia = null;
            }
        }

        // ── Definición de Batallas ─────────────────────────────────────────────
        private void RegistrarBatallasPredefinidas()
        {
            // ── BATALLA 1: Emboscada en el Sendero (Tutorial / Inicio) ───────────
            var b1 = new DatosBatalla
            {
                IdBatalla = 1,
                NombreBatalla = "Batalla 1: Emboscada en el Sendero",
                Columnas = 8,
                Filas = 6,
                TamanioCelda = 1f
            };

            // Terrenos Batalla 1: Coberturas en puntos de choque
            b1.Terrenos.Add(new DatosTerrenoMapa(3, 2, TipoTerreno.CoberturaBosque));
            b1.Terrenos.Add(new DatosTerrenoMapa(3, 3, TipoTerreno.CoberturaBosque));
            b1.Terrenos.Add(new DatosTerrenoMapa(4, 2, TipoTerreno.CoberturaBosque));
            b1.Terrenos.Add(new DatosTerrenoMapa(2, 4, TipoTerreno.CoberturaBosque));

            // Obstáculos Batalla 1 (Poco agua/bloqueo en borde inferior)
            b1.Terrenos.Add(new DatosTerrenoMapa(4, 0, TipoTerreno.ObstaculoAgua));
            b1.Terrenos.Add(new DatosTerrenoMapa(5, 0, TipoTerreno.ObstaculoAgua));

            // Spawns Batalla 1: 1 Jugador, 1 Enemigo
            b1.Spawns.Add(new DatosSpawnUnidad("Capitán Rastrojo", Bando.Jugador, 1, 2, 30, 20, 8, 3, 3, Color.white));
            b1.Spawns.Add(new DatosSpawnUnidad("Rata de Sombra", Bando.Enemigo, 6, 3, 20, 10, 6, 2, 3, Color.white));

            _batallasPredefinidas[1] = b1;

            // ── BATALLA 2: El Vado del Pantano (Obstáculos + 2 Enemigos) ─────────
            var b2 = new DatosBatalla
            {
                IdBatalla = 2,
                NombreBatalla = "Batalla 2: El Vado del Pantano",
                Columnas = 10,
                Filas = 8,
                TamanioCelda = 1f
            };

            // Terrenos Batalla 2: Río central intransitable dividiendo el mapa, con vado transitable en filas 3 y 4
            // Columna 4 y 5: Agua intransitable
            b2.Terrenos.Add(new DatosTerrenoMapa(4, 0, TipoTerreno.ObstaculoAgua));
            b2.Terrenos.Add(new DatosTerrenoMapa(4, 1, TipoTerreno.ObstaculoAgua));
            b2.Terrenos.Add(new DatosTerrenoMapa(4, 2, TipoTerreno.ObstaculoAgua));
            b2.Terrenos.Add(new DatosTerrenoMapa(4, 5, TipoTerreno.ObstaculoAgua));
            b2.Terrenos.Add(new DatosTerrenoMapa(4, 6, TipoTerreno.ObstaculoAgua));
            b2.Terrenos.Add(new DatosTerrenoMapa(4, 7, TipoTerreno.ObstaculoAgua));

            b2.Terrenos.Add(new DatosTerrenoMapa(5, 0, TipoTerreno.ObstaculoAgua));
            b2.Terrenos.Add(new DatosTerrenoMapa(5, 1, TipoTerreno.ObstaculoAgua));
            b2.Terrenos.Add(new DatosTerrenoMapa(5, 6, TipoTerreno.ObstaculoAgua));
            b2.Terrenos.Add(new DatosTerrenoMapa(5, 7, TipoTerreno.ObstaculoAgua));

            // Coberturas estratégicas (Bosques)
            b2.Terrenos.Add(new DatosTerrenoMapa(2, 3, TipoTerreno.CoberturaBosque));
            b2.Terrenos.Add(new DatosTerrenoMapa(2, 4, TipoTerreno.CoberturaBosque));
            b2.Terrenos.Add(new DatosTerrenoMapa(4, 3, TipoTerreno.CoberturaBosque)); // Cobertura en el vado
            b2.Terrenos.Add(new DatosTerrenoMapa(4, 4, TipoTerreno.CoberturaBosque));
            b2.Terrenos.Add(new DatosTerrenoMapa(7, 4, TipoTerreno.CoberturaBosque));
            b2.Terrenos.Add(new DatosTerrenoMapa(6, 1, TipoTerreno.CoberturaBosque));
            b2.Terrenos.Add(new DatosTerrenoMapa(6, 6, TipoTerreno.CoberturaBosque));

            // Spawns Batalla 2: 1 Héroe y 2 Enemigos tácticos
            b2.Spawns.Add(new DatosSpawnUnidad("Capitán Rastrojo", Bando.Jugador, 1, 1, 30, 20, 8, 3, 3, Color.white));
            b2.Spawns.Add(new DatosSpawnUnidad("Rata de Sombra Alfa", Bando.Enemigo, 7, 6, 25, 15, 7, 3, 3, Color.white));
            b2.Spawns.Add(new DatosSpawnUnidad("Rata de Sombra Exploradora", Bando.Enemigo, 7, 2, 18, 10, 6, 1, 4, Color.white));

            _batallasPredefinidas[2] = b2;
        }

        // ── Carga y Construcción de Batalla ────────────────────────────────────
        /// <summary>
        /// Carga y genera la batalla especificada construyendo el Grid, asignando terrenos y configurando unidades.
        /// </summary>
        public void CargarBatalla(int idBatalla, bool iniciarCombate = true)
        {
            if (!_batallasPredefinidas.ContainsKey(idBatalla))
            {
                Debug.LogWarning($"[MapLoader] Batalla {idBatalla} no encontrada. Reiniciando a Batalla 1.");
                idBatalla = 1;
            }

            BatallaActualIndex = idBatalla;
            DatosBatalla batalla = _batallasPredefinidas[idBatalla];
            BatallaActiva = batalla;

            Debug.Log($"[MapLoader] 🗺️ Cargando '{batalla.NombreBatalla}' ({batalla.Columnas}x{batalla.Filas})...");

            // 1. Obtener unidades preexistentes en la escena para reutilizar sus componentes visuales / sprites
            var unidadesPrevias = new List<UnitController>(FindObjectsByType<UnitController>(FindObjectsSortMode.None));
            var unidadesJugadorEscena = unidadesPrevias.FindAll(u => u.BandoUnidad == Bando.Jugador);
            var unidadesEnemigoEscena = unidadesPrevias.FindAll(u => u.BandoUnidad == Bando.Enemigo);

            // 2. Limpiar registros en TurnManager
            if (TurnManager.Instancia != null)
            {
                TurnManager.Instancia.UnidadesJugador.Clear();
                TurnManager.Instancia.UnidadesEnemigo.Clear();
            }

            // 3. Reconstruir cuadrícula del GridManager con las nuevas dimensiones
            if (GridManager.Instancia != null)
            {
                GridManager.Instancia.ReconstruirCuadricula(batalla.Columnas, batalla.Filas, batalla.TamanioCelda);

                // 4. Asignar terrenos especiales (Agua / Obstáculo y Bosque / Cobertura)
                if (batalla.Terrenos != null)
                {
                    foreach (var t in batalla.Terrenos)
                    {
                        var tileData = TileData.CrearInstancia(t.Tipo);
                        GridManager.Instancia.AsignarTileData(t.Col, t.Row, tileData);
                    }
                }
            }

            // 5. Configurar o instanciar unidades de la batalla
            var unidadesReutilizadas = new HashSet<UnitController>();
            if (batalla.Spawns != null)
            {
                foreach (var spawn in batalla.Spawns)
                {
                    UnitController unidadExistente = null;
                    if (spawn.Bando == Bando.Jugador && unidadesJugadorEscena.Count > 0)
                    {
                        unidadExistente = unidadesJugadorEscena.Find(u => !unidadesReutilizadas.Contains(u));
                    }
                    else if (spawn.Bando == Bando.Enemigo && unidadesEnemigoEscena.Count > 0)
                    {
                        unidadExistente = unidadesEnemigoEscena.Find(u => !unidadesReutilizadas.Contains(u));
                    }

                    if (unidadExistente != null)
                    {
                        unidadesReutilizadas.Add(unidadExistente);
                        ConfigurarUnidadExistente(unidadExistente, spawn);
                    }
                    else
                    {
                        CrearUnidadEnEscena(spawn);
                    }
                }
            }

            // Destruir unidades sobrantes que no formen parte de la nueva batalla
            foreach (var u in unidadesPrevias)
            {
                if (u != null && !unidadesReutilizadas.Contains(u) && u.gameObject != null)
                {
                    DestroyImmediate(u.gameObject);
                }
            }

            // 6. Centrar la cámara en el nuevo tablero
            GridManager.Instancia?.ConfigurarCamara2D();

            // 7. Reiniciar estados de gestores
            if (BattleManager.Instancia != null)
            {
                // Reset de estado y estadísticas
                var campoEstado = typeof(BattleManager).GetProperty("EstadoActual");
                if (campoEstado != null) campoEstado.SetValue(BattleManager.Instancia, EstadoBatalla.EnProgreso);

                typeof(BattleManager).GetProperty("BajasEnemigas")?.SetValue(BattleManager.Instancia, 0);
                typeof(BattleManager).GetProperty("BajasAliadas")?.SetValue(BattleManager.Instancia, 0);
                typeof(BattleManager).GetProperty("DanioTotalInfligido")?.SetValue(BattleManager.Instancia, 0);
                typeof(BattleManager).GetProperty("TiempoInicioBatalla")?.SetValue(BattleManager.Instancia, Time.time);
            }

            if (ActionMenu.InstanciaExiste)
            {
                ActionMenu.Instancia.CerrarMenu();
            }

            // 8. Iniciar combate si corresponde
            if (iniciarCombate && TurnManager.Instancia != null)
            {
                TurnManager.Instancia.IniciarCombate();
            }

            Debug.Log($"[MapLoader] ✅ '{batalla.NombreBatalla}' cargada exitosamente con {batalla.Spawns?.Count} unidades.");
        }

        /// <summary>
        /// Reconfigura una unidad que ya existía en la escena respetando su SpriteRenderer e Inspector.
        /// </summary>
        private void ConfigurarUnidadExistente(UnitController unit, DatosSpawnUnidad spawn)
        {
            unit.gameObject.SetActive(true);
            unit.ColInicial = spawn.Col;
            unit.FilaInicial = spawn.Row;
            unit.VidaMaxima = spawn.VidaMax;
            unit.ManaMaximo = spawn.ManaMax;
            unit.Ataque = spawn.Ataque;
            unit.Defensa = spawn.Defensa;
            unit.RangoMovimiento = spawn.Movimiento;
            unit.RangoAtaqueMinimo = spawn.RangoAtaqueMin;
            unit.RangoAtaqueMaximo = spawn.RangoAtaqueMax;

            unit.RestaurarEstado(spawn.Col, spawn.Row, spawn.VidaMax, spawn.VidaMax, spawn.ManaMax, spawn.ManaMax, false, true);

            if (GridManager.Instancia != null)
            {
                GridManager.Instancia.SetOcupacion(spawn.Col, spawn.Row, true);
            }

            if (TurnManager.Instancia != null)
            {
                TurnManager.Instancia.RegistrarUnidad(unit);
            }
        }

        /// <summary>
        /// Instancia y configura un GameObject con UnitController en tiempo de ejecución.
        /// </summary>
        private GameObject CrearUnidadEnEscena(DatosSpawnUnidad spawn)
        {
            var go = new GameObject(spawn.Nombre);

            // Componente principal de unidad
            var unit = go.AddComponent<UnitController>();
            unit.NombreUnidad = spawn.Nombre;
            unit.BandoUnidad = spawn.Bando;
            unit.ColInicial = spawn.Col;
            unit.FilaInicial = spawn.Row;
            unit.VidaMaxima = spawn.VidaMax;
            unit.ManaMaximo = spawn.ManaMax;
            unit.Ataque = spawn.Ataque;
            unit.Defensa = spawn.Defensa;
            unit.RangoMovimiento = spawn.Movimiento;
            unit.RangoAtaqueMinimo = spawn.RangoAtaqueMin;
            unit.RangoAtaqueMaximo = spawn.RangoAtaqueMax;
            unit.ColorNormal = spawn.ColorVisual;

            // Habilidades activas automáticas
            unit.Habilidades = Combat.SkillSystem.ObtenerHabilidadesPredeterminadas();

            // Sincronizar estado completo inicial
            unit.RestaurarEstado(spawn.Col, spawn.Row, spawn.VidaMax, spawn.VidaMax, spawn.ManaMax, spawn.ManaMax, false, true);

            // Asegurar ocupación en GridManager
            if (GridManager.Instancia != null)
            {
                GridManager.Instancia.SetOcupacion(spawn.Col, spawn.Row, true);
            }

            // Registrar en TurnManager
            if (TurnManager.Instancia != null)
            {
                TurnManager.Instancia.RegistrarUnidad(unit);
            }

            return go;
        }

        // ── Navegación y Transiciones ──────────────────────────────────────────
        /// <summary>
        /// Avanza a la siguiente batalla con transición fluida a negro (SceneTransition).
        /// </summary>
        public void CargarSiguienteBatalla()
        {
            int siguiente = BatallaActualIndex + 1;
            if (!_batallasPredefinidas.ContainsKey(siguiente))
            {
                siguiente = 1; // Reinicia ciclo de batallas
            }

            Debug.Log($"[MapLoader] Avanzando de Batalla {BatallaActualIndex} a Batalla {siguiente} con SceneTransition...");

            if (SceneTransition.Instancia != null)
            {
                SceneTransition.Instancia.TransicionPersonalizada(() =>
                {
                    CargarBatalla(siguiente, true);
                }, 0.5f);
            }
            else
            {
                CargarBatalla(siguiente, true);
            }
        }

        /// <summary>
        /// Recarga la batalla actual (para Reintentar).
        /// </summary>
        public void RecargarBatallaActual()
        {
            if (SceneTransition.Instancia != null)
            {
                SceneTransition.Instancia.TransicionPersonalizada(() =>
                {
                    CargarBatalla(BatallaActualIndex, true);
                }, 0.5f);
            }
            else
            {
                CargarBatalla(BatallaActualIndex, true);
            }
        }

        // ── Serialización JSON ─────────────────────────────────────────────────
        /// <summary>
        /// Exporta la configuración de una batalla a formato JSON.
        /// </summary>
        public string ExportarBatallaAJSON(int idBatalla)
        {
            if (_batallasPredefinidas.TryGetValue(idBatalla, out var b))
            {
                return JsonUtility.ToJson(b, true);
            }
            return null;
        }

        /// <summary>
        /// Importa y registra una batalla dinámica desde JSON.
        /// </summary>
        public bool CargarBatallaDesdeJSON(string json)
        {
            try
            {
                var batalla = JsonUtility.FromJson<DatosBatalla>(json);
                if (batalla != null)
                {
                    _batallasPredefinidas[batalla.IdBatalla] = batalla;
                    Debug.Log($"[MapLoader] Batalla {batalla.IdBatalla} '{batalla.NombreBatalla}' registrada desde JSON.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MapLoader] Error al deserializar JSON de batalla: {ex.Message}");
            }
            return false;
        }
    }
}
