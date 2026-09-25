// ============================================================
//  GridManager.cs
//  Felinaria: El último presagio
//  Fase 3 – IA Enemiga y Mapa (actualizado)
//
//  RESPONSABILIDAD:
//    - Genera la cuadrícula táctica 2D en tiempo de ejecución.
//    - Convierte coordenadas de mundo <-> coordenadas de celda (col, fila).
//    - Registra qué celdas están ocupadas por unidades.
//    - Almacena TileData (tipo de terreno) por celda (Fase 3).
//    - Expone helpers para consultar celdas vecinas (movimiento).
//
//  CAMBIOS FASE 3:
//    - Cell ahora tiene un campo TileData para terreno.
//    - API ObtenerTileData() y AsignarTileData() para Pathfinding/CombatSystem.
//    - Lista de obstáculos configurables desde el Inspector.
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace Felinaria.Grid
{
    // ─── Datos de una celda individual ───────────────────────────────────────
    /// <summary>
    /// Almacena el estado lógico de una celda del tablero.
    /// </summary>
    [System.Serializable]
    public class Cell
    {
        /// <summary>Columna (eje X) en la cuadrícula.</summary>
        public int Col;

        /// <summary>Fila (eje Y) en la cuadrícula.</summary>
        public int Row;

        /// <summary>Posición en el mundo Unity (centro de la celda).</summary>
        public Vector3 WorldPosition;

        /// <summary>True si hay una unidad sobre esta celda.</summary>
        public bool EstaOcupada;

        /// <summary>Referencia al GameObject visual de la celda.</summary>
        public GameObject Objeto;

        /// <summary>Tipo de terreno asignado a esta celda (Fase 3). Puede ser null (llanura por defecto).</summary>
        public TileData Terreno;

        public Cell(int col, int row, Vector3 worldPos, GameObject obj)
        {
            Col           = col;
            Row           = row;
            WorldPosition = worldPos;
            EstaOcupada   = false;
            Objeto        = obj;
            Terreno       = null;
        }
    }

    // ─── GridManager ─────────────────────────────────────────────────────────
    /// <summary>
    /// Singleton que gestiona la cuadrícula táctica del juego.
    /// Se coloca en un GameObject vacío llamado "GridManager".
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Dimensiones de la cuadrícula")]
        [Tooltip("Número de columnas (ancho del tablero).")]
        [Range(2, 30)]
        public int Columnas = 10;

        [Tooltip("Número de filas (alto del tablero).")]
        [Range(2, 30)]
        public int Filas = 8;

        [Tooltip("Tamaño de cada celda en unidades de Unity.")]
        public float TamanioCelda = 1f;

        [Header("Visualización Graybox")]
        [Tooltip("Prefab de la celda (un cuadro 2D genérico). " +
                 "Si está vacío, se genera con primitivas.")]
        public GameObject PrefabCelda;

        [Tooltip("Color del fondo de celdas pares.")]
        public Color ColorCeldaPar   = new Color(0.85f, 0.85f, 0.85f, 1f);

        [Tooltip("Color del fondo de celdas impares (tablero de ajedrez).")]
        public Color ColorCeldaImpar = new Color(0.55f, 0.55f, 0.55f, 1f);

        [Tooltip("Posición en el mundo donde comienza la esquina inferior-izquierda.")]
        public Vector3 OrigenMundo = Vector3.zero;

        [Header("Terreno y Obstáculos (Fase 3)")]
        [Tooltip("TileData por defecto para todas las celdas que no tengan " +
                 "un terreno específico asignado. Dejar vacío = llanura.")]
        public TileData TerrenoDefault;

        [Tooltip("Lista de asignaciones de terreno. Cada elemento define " +
                 "una coordenada y qué TileData tiene esa celda.")]
        public List<AsignacionTerreno> AsignacionesTerreno = new List<AsignacionTerreno>();

        // ── Singleton ──────────────────────────────────────────────────────────
        // ── Singleton y Estado de Inicialización ───────────────────────────────
        private static GridManager _instancia;
        private static bool _aplicacionCerrando = false;

        /// <summary>Indica si existe una instancia activa sin forzar su creación.</summary>
        public static bool InstanciaExiste => _instancia != null && !_aplicacionCerrando;

        /// <summary>Acceso global al GridManager con auto-instanciación segura.</summary>
        public static GridManager Instancia
        {
            get
            {
                if (_aplicacionCerrando) return null;
                if (_instancia == null)
                {
                    _instancia = FindFirstObjectByType<GridManager>();
                    if (_instancia == null && Application.isPlaying)
                    {
                        var go = new GameObject("GridManager_Auto");
                        _instancia = go.AddComponent<GridManager>();
                    }
                }
                return _instancia;
            }
            private set => _instancia = value;
        }

        /// <summary>Indica si el tablero ya fue generado y sus celdas están listas para consultarse.</summary>
        public bool EstaInicializado { get; private set; } = false;

        // ── Estado interno ─────────────────────────────────────────────────────
        // Diccionario principal: clave = (col, fila), valor = datos de la celda.
        private Dictionary<Vector2Int, Cell> _celdas = new Dictionary<Vector2Int, Cell>();

        // Contenedor padre para mantener la jerarquía ordenada en el Editor.
        private Transform _contenedorCeldas;

        // ── Unity Lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            _aplicacionCerrando = false;
            if (_instancia != null && _instancia != this)
            {
                Debug.LogWarning("[GridManager] Ya existe una instancia. Destruyendo duplicado.");
                Destroy(gameObject);
                return;
            }
            _instancia = this;

            // Generar la cuadrícula inmediatamente en Awake para asegurar
            // que todas las celdas existan antes de que las unidades ejecuten Start().
            GenerarCuadricula();
            ConfigurarCamara2D();
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

        private void Start()
        {
            // Verificación de respaldo si no fue generado en Awake
            if (!EstaInicializado)
            {
                GenerarCuadricula();
            }
            ConfigurarCamara2D();
        }

        private bool _camaraConfigurada = false;

        private void LateUpdate()
        {
            // Asegurar que la cámara quede configurada en el primer frame en Playmode
            if (!_camaraConfigurada)
            {
                ConfigurarCamara2D();
                _camaraConfigurada = true;
            }
        }

        // ── Configuración Cámara 2D ───────────────────────────────────────────
        /// <summary>
        /// Configura la cámara principal en modo Ortográfico 2D (sin perspectiva ni inclinación)
        /// y la centra perfectamente sobre el tablero.
        /// </summary>
        public void ConfigurarCamara2D()
        {
            Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (cam == null) return;

            // Modo 2D puro: proyección ortográfica y rotación frontal
            cam.orthographic = true;
            cam.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            // Centrar cámara en el medio de la cuadrícula
            float centroX = OrigenMundo.x + (Columnas - 1) * TamanioCelda * 0.5f;
            float centroY = OrigenMundo.y + (Filas - 1) * TamanioCelda * 0.5f;
            cam.transform.position = new Vector3(centroX, centroY, -10f);

            // Ajustar tamaño ortográfico con margen
            float margen = 1.2f;
            float altoRequerido = (Filas * TamanioCelda * 0.5f) + margen;
            float aspect = (cam.aspect > 0.05f) ? cam.aspect : (16f / 9f);
            float anchoRequerido = ((Columnas * TamanioCelda * 0.5f) / aspect) + margen;
            cam.orthographicSize = Mathf.Max(altoRequerido, anchoRequerido, 5f);
        }

        // ── Generación ─────────────────────────────────────────────────────────
        /// <summary>
        /// Instancia todos los objetos de celda y rellena el diccionario.
        /// Llamado automáticamente en Awake, pero también puede invocarse
        /// manualmente para regenerar el tablero en runtime.
        /// </summary>
        public void GenerarCuadricula()
        {
            // Limpiar generación previa si existe.
            LimpiarCuadricula();

            // Crear contenedor vacío para organizar la escena.
            var contenedorObj = new GameObject("Celdas");
            contenedorObj.transform.SetParent(transform);
            _contenedorCeldas = contenedorObj.transform;

            for (int col = 0; col < Columnas; col++)
            {
                for (int row = 0; row < Filas; row++)
                {
                    CrearCelda(col, row);
                }
            }

            // Aplicar terrenos configurados desde el Inspector (Fase 3).
            AplicarTerrenosDesdeInspector();

            EstaInicializado = true;
            ConfigurarCamara2D();

            Debug.Log($"[GridManager] Cuadrícula generada: {Columnas}x{Filas} = {Columnas * Filas} celdas en 2D.");
        }

        /// <summary>
        /// Crea y registra una única celda en la posición (col, row).
        /// </summary>
        private void CrearCelda(int col, int row)
        {
            // Calcular posición en el mundo 2D (Z = 0).
            Vector3 posicion = OrigenMundo + new Vector3(
                col * TamanioCelda,
                row * TamanioCelda,
                0f
            );

            // Instanciar el prefab o crear una primitiva de respaldo.
            GameObject objeto;
            if (PrefabCelda != null)
            {
                objeto = Instantiate(PrefabCelda, posicion, Quaternion.identity, _contenedorCeldas);
            }
            else
            {
                // Fallback Graybox: cuadrado blanco 2D con SpriteRenderer y BoxCollider2D.
                objeto = CrearCuadradoGraybox(posicion);
            }

            objeto.name = $"Celda_{col}_{row}";

            // 1. Destruir inmediatamente colisionadores 3D si existieran en el prefab
            var colliders3D = objeto.GetComponentsInChildren<Collider>(true);
            foreach (var col3D in colliders3D)
            {
                DestroyImmediate(col3D);
            }

            // 2. Destruir MeshFilter y MeshRenderer 3D si existieran
            var meshFilters = objeto.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in meshFilters) DestroyImmediate(mf);
            var meshRenderers = objeto.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in meshRenderers) DestroyImmediate(mr);

            // 3. Asegurar BoxCollider2D en la celda para detección de clics 2D
            var col2D = objeto.GetComponent<BoxCollider2D>();
            if (col2D == null)
            {
                col2D = objeto.AddComponent<BoxCollider2D>();
            }
            if (col2D != null)
            {
                col2D.size = Vector2.one * TamanioCelda;
                col2D.isTrigger = true;
            }

            // 4. Asegurar SpriteRenderer 2D
            var sr = objeto.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = objeto.AddComponent<SpriteRenderer>();
                sr.sprite = ObtenerSpriteBlanco();
                sr.sortingLayerName = "Default";
                sr.sortingOrder = 0;
            }

            // Aplicar color tipo tablero de ajedrez.
            sr.color = ((col + row) % 2 == 0) ? ColorCeldaPar : ColorCeldaImpar;

            // Registrar en el diccionario.
            var clave = new Vector2Int(col, row);
            var celda = new Cell(col, row, posicion, objeto);
            _celdas[clave] = celda;
        }

        /// <summary>
        /// Crea un cuadrado 2D básico con SpriteRenderer y BoxCollider2D.
        /// </summary>
        private GameObject CrearCuadradoGraybox(Vector3 posicion)
        {
            var obj = new GameObject("CeldaGraybox");
            obj.transform.SetParent(_contenedorCeldas);
            obj.transform.position = new Vector3(posicion.x, posicion.y, 0f);

            // Escalar ligeramente por debajo del tamaño de celda para ver las juntas.
            obj.transform.localScale = Vector3.one * (TamanioCelda * 0.95f);

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = ObtenerSpriteBlanco();
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 0;

            var col2D = obj.AddComponent<BoxCollider2D>();
            col2D.size = Vector2.one;
            col2D.isTrigger = true;

            return obj;
        }

        private static Sprite _spriteBlancoCache;

        /// <summary>
        /// Devuelve un sprite blanco cuadrado 2D estático reutilizable.
        /// </summary>
        public static Sprite ObtenerSpriteBlanco()
        {
            if (_spriteBlancoCache != null) return _spriteBlancoCache;

            Texture2D tex = new Texture2D(2, 2);
            Color[] pixels = new Color[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(pixels);
            tex.Apply();
            _spriteBlancoCache = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            return _spriteBlancoCache;
        }

        /// <summary>
        /// Wrapper de instancia para compatibilidad hacia atrás.
        /// </summary>
        public Sprite GetSpriteBlanco() => ObtenerSpriteBlanco();

        /// <summary>
        /// Destruye todos los objetos de celda y limpia el diccionario.
        /// </summary>
        public void LimpiarCuadricula()
        {
            if (_contenedorCeldas != null)
            {
                Destroy(_contenedorCeldas.gameObject);
            }
            _celdas.Clear();
        }

        // ── API pública ────────────────────────────────────────────────────────

        /// <summary>
        /// Devuelve la celda en las coordenadas indicadas, o null si no existe.
        /// </summary>
        public Cell ObtenerCelda(int col, int row)
        {
            _celdas.TryGetValue(new Vector2Int(col, row), out Cell celda);
            return celda;
        }

        /// <summary>
        /// Devuelve la celda en las coordenadas indicadas, o null si no existe.
        /// </summary>
        public Cell ObtenerCelda(Vector2Int coordenada)
            => ObtenerCelda(coordenada.x, coordenada.y);

        /// <summary>
        /// Convierte una posición de mundo a coordenadas de cuadrícula.
        /// Útil para detectar en qué celda está el cursor del jugador.
        /// </summary>
        public Vector2Int MundoACoordenada(Vector3 posicionMundo)
        {
            int col = Mathf.RoundToInt((posicionMundo.x - OrigenMundo.x) / TamanioCelda);
            int row = Mathf.RoundToInt((posicionMundo.y - OrigenMundo.y) / TamanioCelda);
            return new Vector2Int(col, row);
        }

        /// <summary>
        /// Convierte coordenadas de cuadrícula a posición central en el mundo.
        /// </summary>
        public Vector3 CoordenadaAMundo(int col, int row)
            => OrigenMundo + new Vector3(col * TamanioCelda, row * TamanioCelda, 0f);

        /// <summary>
        /// Convierte coordenadas de cuadrícula a posición central en el mundo.
        /// </summary>
        public Vector3 CoordenadaAMundo(Vector2Int coordenada)
            => CoordenadaAMundo(coordenada.x, coordenada.y);

        /// <summary>
        /// Indica si las coordenadas están dentro de los límites del tablero.
        /// </summary>
        public bool EsCoordenadaValida(int col, int row)
            => col >= 0 && col < Columnas && row >= 0 && row < Filas;

        /// <summary>
        /// Indica si las coordenadas están dentro de los límites del tablero.
        /// </summary>
        public bool EsCoordenadaValida(Vector2Int coord)
            => EsCoordenadaValida(coord.x, coord.y);

        /// <summary>
        /// Indica si una celda está disponible (existe y no está ocupada).
        /// </summary>
        public bool EstaCeldaLibre(int col, int row)
        {
            var celda = ObtenerCelda(col, row);
            return celda != null && !celda.EstaOcupada;
        }

        /// <summary>
        /// Marca o desmarca una celda como ocupada.
        /// Llamado por UnitController al moverse.
        /// </summary>
        public void SetOcupacion(int col, int row, bool ocupada)
        {
            var celda = ObtenerCelda(col, row);
            if (celda == null)
            {
                Debug.LogWarning($"[GridManager] SetOcupacion: celda ({col},{row}) no existe.");
                return;
            }
            celda.EstaOcupada = ocupada;
        }

        /// <summary>
        /// Devuelve las celdas adyacentes en las 4 direcciones cardinales
        /// que sean válidas y estén libres. Útil para calcular rango de movimiento.
        /// </summary>
        public List<Cell> ObtenerVecinosLibres(int col, int row)
        {
            var vecinos = new List<Cell>();
            var direcciones = new Vector2Int[]
            {
                new Vector2Int( 1,  0),   // Derecha
                new Vector2Int(-1,  0),   // Izquierda
                new Vector2Int( 0,  1),   // Arriba
                new Vector2Int( 0, -1),   // Abajo
            };

            foreach (var dir in direcciones)
            {
                int nc = col + dir.x;
                int nr = row + dir.y;
                if (EstaCeldaLibre(nc, nr))
                {
                    vecinos.Add(ObtenerCelda(nc, nr));
                }
            }

            return vecinos;
        }

        // ── API de Terreno (Fase 3) ────────────────────────────────────────────

        /// <summary>
        /// Devuelve el TileData (tipo de terreno) de una celda.
        /// Retorna TerrenoDefault si la celda no tiene terreno específico.
        /// Retorna null si no hay ni específico ni default.
        /// </summary>
        public TileData ObtenerTileData(int col, int row)
        {
            var celda = ObtenerCelda(col, row);
            if (celda == null) return null;
            return celda.Terreno ?? TerrenoDefault;
        }

        /// <summary>
        /// Devuelve el TileData (tipo de terreno) de una celda.
        /// </summary>
        public TileData ObtenerTileData(Vector2Int coord)
            => ObtenerTileData(coord.x, coord.y);

        /// <summary>
        /// Asigna un TileData a una celda específica.
        /// Puede llamarse en runtime para modificar el terreno dinámicamente.
        /// </summary>
        public void AsignarTileData(int col, int row, TileData terreno)
        {
            var celda = ObtenerCelda(col, row);
            if (celda == null)
            {
                Debug.LogWarning($"[GridManager] AsignarTileData: celda ({col},{row}) no existe.");
                return;
            }
            celda.Terreno = terreno;

            // Actualizar visual si el terreno tiene color propio.
            if (terreno != null && terreno.AplicarColor && celda.Objeto != null)
            {
                var sr = celda.Objeto.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.color = terreno.ColorTerreno;
            }

            Debug.Log($"[GridManager] Terreno '{terreno?.NombreTerreno}' asignado a ({col},{row}).");
        }

        /// <summary>
        /// Aplica las asignaciones de terreno configuradas desde el Inspector.
        /// Llamado automáticamente al generar la cuadrícula.
        /// </summary>
        private void AplicarTerrenosDesdeInspector()
        {
            foreach (var asignacion in AsignacionesTerreno)
            {
                if (asignacion.Terreno == null) continue;
                AsignarTileData(asignacion.Coordenada.x, asignacion.Coordenada.y, asignacion.Terreno);
            }
        }

        /// <summary>
        /// Indica si una celda es transitable considerando su TileData.
        /// Una celda es transitable si: existe, su TileData lo permite
        /// (o no tiene TileData asignado) y no está ocupada.
        /// </summary>
        public bool EsCeldaTransitable(int col, int row)
        {
            var celda = ObtenerCelda(col, row);
            if (celda == null) return false;

            var terreno = ObtenerTileData(col, row);
            if (terreno != null && !terreno.EsTransitable) return false;

            return true;
        }

        // ── Depuración visual ──────────────────────────────────────────────────
        // OnDrawGizmos se ejecuta en el Editor aunque el juego no esté en Play.
        private void OnDrawGizmos()
        {
            // Dibuja la cuadrícula como líneas naranjas para previsualizar
            // sin necesidad de ejecutar el juego.
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);

            for (int col = 0; col <= Columnas; col++)
            {
                Vector3 inicio = OrigenMundo + new Vector3(col * TamanioCelda, 0f, 0f);
                Vector3 fin    = OrigenMundo + new Vector3(col * TamanioCelda, Filas * TamanioCelda, 0f);
                Gizmos.DrawLine(inicio, fin);
            }

            for (int row = 0; row <= Filas; row++)
            {
                Vector3 inicio = OrigenMundo + new Vector3(0f, row * TamanioCelda, 0f);
                Vector3 fin    = OrigenMundo + new Vector3(Columnas * TamanioCelda, row * TamanioCelda, 0f);
                Gizmos.DrawLine(inicio, fin);
            }
        }
    }

    // ─── Estructura para asignar terreno desde el Inspector ──────────────────
    /// <summary>
    /// Par coordenada-terreno para configurar el mapa desde el Inspector.
    /// </summary>
    [System.Serializable]
    public struct AsignacionTerreno
    {
        [Tooltip("Coordenada (col, fila) de la celda.")]
        public Vector2Int Coordenada;

        [Tooltip("TileData que se aplica a esta celda.")]
        public TileData Terreno;
    }
}
