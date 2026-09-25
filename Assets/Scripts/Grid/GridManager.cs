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
        /// <summary>Acceso global al GridManager desde cualquier script.</summary>
        public static GridManager Instancia { get; private set; }

        // ── Estado interno ─────────────────────────────────────────────────────
        // Diccionario principal: clave = (col, fila), valor = datos de la celda.
        private Dictionary<Vector2Int, Cell> _celdas = new Dictionary<Vector2Int, Cell>();

        // Contenedor padre para mantener la jerarquía ordenada en el Editor.
        private Transform _contenedorCeldas;

        // ── Unity Lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            // Patrón Singleton: solo puede existir una instancia.
            if (Instancia != null && Instancia != this)
            {
                Debug.LogWarning("[GridManager] Ya existe una instancia. Destruyendo duplicado.");
                Destroy(gameObject);
                return;
            }
            Instancia = this;
        }

        private void Start()
        {
            GenerarCuadricula();
        }

        // ── Generación ─────────────────────────────────────────────────────────
        /// <summary>
        /// Instancia todos los objetos de celda y rellena el diccionario.
        /// Llamado automáticamente en Start, pero también puede invocarse
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

            Debug.Log($"[GridManager] Cuadrícula generada: {Columnas}x{Filas} = {Columnas * Filas} celdas.");
        }

        /// <summary>
        /// Crea y registra una única celda en la posición (col, row).
        /// </summary>
        private void CrearCelda(int col, int row)
        {
            // Calcular posición en el mundo.
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
                // Fallback Graybox: cuadrado blanco con SpriteRenderer.
                objeto = CrearCuadradoGraybox(posicion);
            }

            objeto.name = $"Celda_{col}_{row}";

            // Aplicar color tipo tablero de ajedrez.
            var sr = objeto.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = ((col + row) % 2 == 0) ? ColorCeldaPar : ColorCeldaImpar;
            }

            // Registrar en el diccionario.
            var clave = new Vector2Int(col, row);
            var celda = new Cell(col, row, posicion, objeto);
            _celdas[clave] = celda;
        }

        /// <summary>
        /// Crea un cuadrado 2D básico con SpriteRenderer cuando no hay prefab.
        /// Solo se usa durante el Grayboxing.
        /// </summary>
        private GameObject CrearCuadradoGraybox(Vector3 posicion)
        {
            var obj = new GameObject("CeldaGraybox");
            obj.transform.SetParent(_contenedorCeldas);
            obj.transform.position = posicion;

            // Escalar ligeramente por debajo del tamaño de celda para ver las juntas.
            obj.transform.localScale = Vector3.one * (TamanioCelda * 0.95f);

            var sr = obj.AddComponent<SpriteRenderer>();
            // Sprite blanco cuadrado por defecto que viene con Unity.
            sr.sprite = GetSpriteBlanco();
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 0;

            return obj;
        }

        /// <summary>
        /// Devuelve el sprite blanco cuadrado que Unity incluye de serie.
        /// </summary>
        private Sprite GetSpriteBlanco()
        {
            // "UI/Skin/UISprite" es un sprite cuadrado que siempre está disponible.
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

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
