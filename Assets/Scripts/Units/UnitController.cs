// ============================================================
//  UnitController.cs
//  Felinaria: El último presagio
//  Fase 3 – IA Enemiga y Mapa (actualizado)
//
//  RESPONSABILIDAD:
//    - Representa a cualquier unidad en el tablero (héroe o enemigo).
//    - Lee sus estadísticas desde un ScriptableObject UnitStats (Fase 2).
//    - Mueve a la unidad entre celdas con interpolación suave (sin teletransporte).
//    - Comunica su posición al GridManager para actualizar el mapa de ocupación.
//    - Respeta el turno activo (pregunta al TurnManager antes de actuar).
//    - Expone API para CombatSystem y ActionMenu.
//
//  CAMBIOS FASE 2:
//    - Se lee de UnitStats (ScriptableObject) en vez de valores hardcodeados.
//    - Se expone MarcarComoUsadaPublico() para CombatSystem.
//    - Se agregan propiedades de rango de ataque.
//    - Se agrega Collider2D automático para detección de clics.
// ============================================================

using System.Collections;
using UnityEngine;
using Felinaria.Grid;
using Felinaria.Managers;
using Felinaria.Data;

namespace Felinaria.Units
{
    // ─── Enum: bando al que pertenece la unidad ──────────────────────────────
    public enum Bando
    {
        Jugador,
        Enemigo
    }

    // ─── UnitController ──────────────────────────────────────────────────────
    /// <summary>
    /// Script que va en cada unidad (héroe o enemigo) del tablero.
    /// </summary>
    public class UnitController : MonoBehaviour
    {
        // ── Inspector: ScriptableObject de stats (Fase 2) ─────────────────────
        [Header("Ficha de Estadísticas (ScriptableObject)")]
        [Tooltip("Arrastra aquí el archivo UnitStats del personaje. " +
                 "Si está asignado, sobreescribe los valores manuales de abajo.")]
        public UnitStats FichaStats;

        // ── Inspector: Identidad ───────────────────────────────────────────────
        [Header("Identidad de la Unidad (manual si no hay FichaStats)")]
        [Tooltip("Nombre del personaje (p.ej. 'Sera', 'Guardia Enemigo').")]
        public string NombreUnidad = "Unidad";

        [Tooltip("¿A qué bando pertenece esta unidad?")]
        public Bando BandoUnidad = Bando.Jugador;

        // ── Inspector: Posición inicial ────────────────────────────────────────
        [Header("Posición inicial en la cuadrícula")]
        [Tooltip("Columna de inicio (eje X del tablero).")]
        public int ColInicial = 0;

        [Tooltip("Fila de inicio (eje Y del tablero).")]
        public int FilaInicial = 0;

        // ── Inspector: Estadísticas de combate (fallback manual) ──────────────
        [Header("Estadísticas de Combate (fallback si no hay FichaStats)")]
        [Tooltip("Puntos de Vida máximos.")]
        [Range(1, 999)]
        public int VidaMaxima = 30;

        [Tooltip("Puntos de ataque físico.")]
        [Range(0, 99)]
        public int Ataque = 8;

        [Tooltip("Puntos de defensa física.")]
        [Range(0, 99)]
        public int Defensa = 3;

        [Tooltip("Rango de movimiento en celdas por turno.")]
        [Range(1, 10)]
        public int RangoMovimiento = 3;

        [Tooltip("Rango mínimo de ataque (celdas Manhattan).")]
        [Range(1, 5)]
        public int RangoAtaqueMinimo = 1;

        [Tooltip("Rango máximo de ataque (celdas Manhattan).")]
        [Range(1, 5)]
        public int RangoAtaqueMaximo = 1;

        // ── Inspector: Movimiento ──────────────────────────────────────────────
        [Header("Configuración de Movimiento")]
        [Tooltip("Velocidad de desplazamiento visual entre celdas (unidades/segundo).")]
        [Range(1f, 20f)]
        public float VelocidadMovimiento = 6f;

        [Tooltip("Color del sprite cuando la unidad ya actuó este turno.")]
        public Color ColorUsado = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Tooltip("Color normal del sprite.")]
        public Color ColorNormal = Color.white;

        // ── Estado interno ─────────────────────────────────────────────────────

        /// <summary>Vida actual de la unidad.</summary>
        public int VidaActual { get; private set; }

        /// <summary>Coordenada actual en la cuadrícula.</summary>
        public Vector2Int Coordenada { get; private set; }

        /// <summary>True mientras la unidad está desplazándose visualmente.</summary>
        public bool EstaMoviendose { get; private set; }

        /// <summary>True si esta unidad ya actuó en el turno actual.</summary>
        public bool YaActuoEsteTurno { get; private set; }

        // Referencia al SpriteRenderer o MeshRenderer 3D para cambiar colores.
        private SpriteRenderer _spriteRenderer;
        private Renderer _meshRenderer;

        // Coroutine activa de movimiento (para poder cancelarla si fuera necesario).
        private Coroutine _corrutinaMover;

        // ── Unity Lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            // Cargar stats desde ScriptableObject si está asignado.
            CargarDesdeScriptableObject();

            VidaActual = VidaMaxima;

            // Asegurar componentes 2D puros (SpriteRenderer, BoxCollider2D, Z=0)
            AsegurarComponentes2D();

            // Auto-adjuntar HealthBar flotante si no existe
            if (GetComponent<Felinaria.UI.HealthBar>() == null)
            {
                gameObject.AddComponent<Felinaria.UI.HealthBar>();
            }
        }

        private void Start()
        {
            // Posicionar la unidad en su celda inicial al comenzar la escena.
            InicializarEnCuadricula();
        }

        // ── Carga desde ScriptableObject ──────────────────────────────────────
        /// <summary>
        /// Si FichaStats está asignada, sobreescribe los valores manuales
        /// con los del ScriptableObject. Esto permite a los diseñadores
        /// modificar stats sin tocar el Inspector de cada unidad individual.
        /// </summary>
        private void CargarDesdeScriptableObject()
        {
            if (FichaStats == null) return;

            NombreUnidad      = FichaStats.NombrePersonaje;
            VidaMaxima        = FichaStats.VidaMaxima;
            Ataque            = FichaStats.Ataque;
            Defensa           = FichaStats.Defensa;
            RangoMovimiento   = FichaStats.RangoMovimiento;
            VelocidadMovimiento = FichaStats.VelocidadMovimiento;
            RangoAtaqueMinimo = FichaStats.RangoAtaqueMinimo;
            RangoAtaqueMaximo = FichaStats.RangoAtaqueMaximo;
            ColorNormal       = FichaStats.ColorGraybox;
            ColorUsado        = FichaStats.ColorUsado;

            // Aplicar color graybox inmediatamente.
            AplicarColorVisual(ColorNormal);

            Debug.Log($"[UnitController] Stats cargados desde ScriptableObject: '{FichaStats.NombrePersonaje}'");
        }

        /// <summary>
        /// Asegura que la unidad utilice componentes 2D puros (BoxCollider2D y SpriteRenderer)
        /// y elimina colisionadores o mallas 3D para evitar interferencias.
        /// </summary>
        private void AsegurarComponentes2D()
        {
            // 1. Destruir de inmediato cualquier colisionador 3D (para evitar conflicto con BoxCollider2D)
            var colliders3D = GetComponents<Collider>();
            foreach (var col3D in colliders3D)
            {
                DestroyImmediate(col3D);
            }

            // 2. Destruir MeshFilter y MeshRenderer 3D si existieran
            var meshFilters = GetComponents<MeshFilter>();
            foreach (var mf in meshFilters)
            {
                DestroyImmediate(mf);
            }
            var meshRenderers = GetComponents<MeshRenderer>();
            foreach (var mr in meshRenderers)
            {
                DestroyImmediate(mr);
            }

            // 3. Asegurar BoxCollider2D para detección de clics 2D
            var col2D = GetComponent<BoxCollider2D>();
            if (col2D == null)
            {
                col2D = gameObject.AddComponent<BoxCollider2D>();
            }
            if (col2D != null)
            {
                col2D.size = new Vector2(0.85f, 0.85f);
                col2D.isTrigger = false;
            }

            // 4. Asegurar SpriteRenderer para renderizado 2D
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null)
            {
                _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                _spriteRenderer.sortingLayerName = "Default";
                _spriteRenderer.sortingOrder = 2; // Por encima del tablero (order 0)
            }

            if (_spriteRenderer != null && _spriteRenderer.sprite == null)
            {
                _spriteRenderer.sprite = GridManager.ObtenerSpriteBlanco();
            }

            // Aplicar color inicial del bando si no hay color previo
            Color colorBando = (BandoUnidad == Bando.Jugador)
                ? new Color(0.2f, 0.5f, 1f, 1f)
                : new Color(1f, 0.25f, 0.25f, 1f);

            AplicarColorVisual(ColorNormal != Color.white ? ColorNormal : colorBando);

            // Garantizar plano Z = 0
            transform.position = new Vector3(transform.position.x, transform.position.y, 0f);
        }

        // ── Inicialización ─────────────────────────────────────────────────────
        /// <summary>
        /// Coloca la unidad en su celda de inicio y la registra en GridManager y TurnManager.
        /// Garantiza que el GridManager esté listo antes de registrar ocupación.
        /// </summary>
        private void InicializarEnCuadricula()
        {
            if (GridManager.Instancia == null)
            {
                // Fallback por si la instancia estática aún no se asignó
                var gridEnEscena = FindFirstObjectByType<GridManager>();
                if (gridEnEscena == null)
                {
                    Debug.LogError($"[UnitController] '{NombreUnidad}': No se encontró GridManager en la escena.");
                    return;
                }
            }

            // Asegurar que el GridManager haya generado las celdas antes de interactuar
            if (!GridManager.Instancia.EstaInicializado)
            {
                GridManager.Instancia.GenerarCuadricula();
            }

            // Validar que las coordenadas iniciales estén dentro de los límites del tablero
            if (!GridManager.Instancia.EsCoordenadaValida(ColInicial, FilaInicial))
            {
                Debug.LogWarning($"[UnitController] '{NombreUnidad}': Coordenada inicial ({ColInicial},{FilaInicial}) " +
                                 "fuera de los límites del tablero. Reubicando en (0,0).");
                ColInicial  = 0;
                FilaInicial = 0;
            }

            // Verificar si la celda inicial está libre; si no, buscar la primera disponible
            if (!GridManager.Instancia.EstaCeldaLibre(ColInicial, FilaInicial))
            {
                Debug.LogWarning($"[UnitController] '{NombreUnidad}': Celda ({ColInicial},{FilaInicial}) " +
                                 "ya está ocupada. Buscando celda libre más cercana...");
                Vector2Int celdaLibre = EncontrarCeldaLibreMasCercana(ColInicial, FilaInicial);
                ColInicial  = celdaLibre.x;
                FilaInicial = celdaLibre.y;
            }

            // Posicionar el transform en el centro de la celda en el mundo
            Coordenada = new Vector2Int(ColInicial, FilaInicial);
            transform.position = GridManager.Instancia.CoordenadaAMundo(Coordenada);

            // Registrar ocupación en el GridManager
            GridManager.Instancia.SetOcupacion(Coordenada.x, Coordenada.y, true);

            // Auto-registro en TurnManager si no está en la lista
            if (TurnManager.Instancia != null)
            {
                if (BandoUnidad == Bando.Jugador && !TurnManager.Instancia.UnidadesJugador.Contains(this))
                {
                    TurnManager.Instancia.RegistrarUnidad(this);
                }
                else if (BandoUnidad == Bando.Enemigo && !TurnManager.Instancia.UnidadesEnemigo.Contains(this))
                {
                    TurnManager.Instancia.RegistrarUnidad(this);
                }
            }

            Debug.Log($"[UnitController] '{NombreUnidad}' ({BandoUnidad}) inicializado correctamente en ({Coordenada.x},{Coordenada.y}).");
        }

        /// <summary>
        /// Busca la celda libre más cercana a la coordenada deseada.
        /// </summary>
        private Vector2Int EncontrarCeldaLibreMasCercana(int colOrig, int filaOrig)
        {
            if (GridManager.Instancia == null) return new Vector2Int(colOrig, filaOrig);

            for (int r = 0; r < Mathf.Max(GridManager.Instancia.Columnas, GridManager.Instancia.Filas); r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        int c = colOrig + dx;
                        int f = filaOrig + dy;
                        if (GridManager.Instancia.EsCoordenadaValida(c, f) && GridManager.Instancia.EstaCeldaLibre(c, f))
                        {
                            return new Vector2Int(c, f);
                        }
                    }
                }
            }
            return new Vector2Int(colOrig, filaOrig);
        }

        // ── Movimiento ─────────────────────────────────────────────────────────
        /// <summary>
        /// Solicita mover la unidad a las coordenadas dadas.
        /// Valida turno, rango, límites y disponibilidad de celda antes de moverse.
        /// </summary>
        /// <param name="col">Columna destino.</param>
        /// <param name="fila">Fila destino.</param>
        /// <returns>True si el movimiento fue aceptado.</returns>
        public bool MoverACelda(int col, int fila)
        {
            // ── Validación 1: ¿Es el turno correcto para este bando? ───────────
            if (TurnManager.Instancia != null && !TurnManager.Instancia.EsTurnoDeUnidad(this))
            {
                Debug.Log($"[UnitController] '{NombreUnidad}': Movimiento bloqueado, no es tu turno.");
                return false;
            }

            // ── Validación 2: ¿Ya actuó esta unidad? ──────────────────────────
            if (YaActuoEsteTurno)
            {
                Debug.Log($"[UnitController] '{NombreUnidad}': Ya actuó este turno.");
                return false;
            }

            // ── Validación 3: ¿Está en movimiento? ────────────────────────────
            if (EstaMoviendose)
            {
                Debug.Log($"[UnitController] '{NombreUnidad}': Todavía en movimiento.");
                return false;
            }

            // ── Validación 4: ¿La celda destino existe? ───────────────────────
            if (!GridManager.Instancia.EsCoordenadaValida(col, fila))
            {
                Debug.Log($"[UnitController] '{NombreUnidad}': Destino ({col},{fila}) fuera del tablero.");
                return false;
            }

            // ── Validación 5: ¿La celda destino está libre? ───────────────────
            if (!GridManager.Instancia.EstaCeldaLibre(col, fila))
            {
                Debug.Log($"[UnitController] '{NombreUnidad}': Celda ({col},{fila}) está ocupada.");
                return false;
            }

            // ── Validación 6: ¿Está dentro del rango de movimiento? ───────────
            int distancia = CalcularDistanciaManhattan(Coordenada, new Vector2Int(col, fila));
            if (distancia > RangoMovimiento)
            {
                Debug.Log($"[UnitController] '{NombreUnidad}': Destino a distancia {distancia}, " +
                          $"rango máximo {RangoMovimiento}.");
                return false;
            }

            // ── Todo OK: iniciar movimiento ────────────────────────────────────
            _corrutinaMover = StartCoroutine(DesplazarACelda(col, fila));
            return true;
        }

        /// <summary>
        /// Coroutine que desplaza suavemente la unidad hasta la celda destino
        /// usando Vector3.MoveTowards (sin teletransporte).
        /// </summary>
        private IEnumerator DesplazarACelda(int colDestino, int filaDestino)
        {
            EstaMoviendose = true;

            // Liberar la celda actual en el GridManager.
            GridManager.Instancia.SetOcupacion(Coordenada.x, Coordenada.y, false);

            // Calcular posición de destino en el mundo.
            Vector3 posDestino = GridManager.Instancia.CoordenadaAMundo(colDestino, filaDestino);

            // Actualizar la coordenada lógica antes de moverse
            // (otras unidades sabrán que esta celda pronto estará ocupada).
            Coordenada = new Vector2Int(colDestino, filaDestino);
            GridManager.Instancia.SetOcupacion(colDestino, filaDestino, true);

            // ── Bucle de interpolación ──────────────────────────────────────────
            // MoveTowards avanza la posición actual hacia el destino un máximo
            // de (VelocidadMovimiento * deltaTime) unidades por frame.
            while (Vector3.Distance(transform.position, posDestino) > 0.001f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    posDestino,
                    VelocidadMovimiento * Time.deltaTime
                );
                yield return null;  // Espera al siguiente frame.
            }

            // Snappear al centro exacto de la celda al terminar.
            transform.position = posDestino;

            EstaMoviendose = false;
            MarcarComoUsada();

            Debug.Log($"[UnitController] '{NombreUnidad}' llegó a ({colDestino},{filaDestino}).");
        }

        // ── Estado de turno ────────────────────────────────────────────────────
        /// <summary>
        /// Aplica un color al sprite 2D o material 3D de la unidad.
        /// </summary>
        private void AplicarColorVisual(Color c)
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = c;
            }
            else if (_meshRenderer != null && _meshRenderer.material != null)
            {
                _meshRenderer.material.color = c;
            }
        }

        /// <summary>
        /// Marca la unidad como "ya actuó" y cambia su color visual.
        /// Llamado automáticamente al terminar de moverse.
        /// </summary>
        private void MarcarComoUsada()
        {
            YaActuoEsteTurno = true;
            AplicarColorVisual(ColorUsado);
        }

        /// <summary>
        /// Versión pública de MarcarComoUsada().
        /// Llamada por CombatSystem al atacar o por ActionMenu al "Esperar".
        /// </summary>
        public void MarcarComoUsadaPublico()
        {
            MarcarComoUsada();
        }

        /// <summary>
        /// Reinicia el estado de la unidad al comienzo de un nuevo turno.
        /// Llamado por TurnManager al cambiar de turno.
        /// </summary>
        public void ReiniciarTurno()
        {
            YaActuoEsteTurno = false;
            AplicarColorVisual(ColorNormal);
        }

        // ── Combate ────────────────────────────────────────────────────────────
        /// <summary>
        /// Aplica daño a la unidad. Si la vida llega a 0, la elimina.
        /// </summary>
        public void RecibirDanio(int cantidad)
        {
            VidaActual = Mathf.Max(0, VidaActual - cantidad);
            Debug.Log($"[UnitController] '{NombreUnidad}' recibió {cantidad} de daño. Vida: {VidaActual}/{VidaMaxima}");

            if (VidaActual <= 0)
            {
                Morir();
            }
        }

        /// <summary>
        /// Maneja la eliminación de la unidad del tablero.
        /// </summary>
        private void Morir()
        {
            Debug.Log($"[UnitController] '{NombreUnidad}' ha sido derrotado.");
            GridManager.Instancia.SetOcupacion(Coordenada.x, Coordenada.y, false);
            Destroy(gameObject);
        }

        // ── API para IA (Fase 3) ───────────────────────────────────────────────
        /// <summary>
        /// Actualiza la coordenada lógica de la unidad directamente.
        /// SOLO para uso de EnemyAI, que mueve las unidades celda a celda
        /// sin pasar por las validaciones de MoverACelda().
        /// </summary>
        public void SetCoordenadaDirecta(Vector2Int nuevaCoordenada)
        {
            Coordenada = nuevaCoordenada;
        }

        // ── Utilidades ─────────────────────────────────────────────────────────
        /// <summary>
        /// Distancia Manhattan = |Δcol| + |Δfila|. Usada en tableros sin diagonal.
        /// </summary>
        private int CalcularDistanciaManhattan(Vector2Int origen, Vector2Int destino)
            => Mathf.Abs(destino.x - origen.x) + Mathf.Abs(destino.y - origen.y);

        // ── Depuración ─────────────────────────────────────────────────────────
        private void OnDrawGizmos()
        {
            // Dibuja un pequeño cuadrado de color sobre la unidad en el editor.
            Gizmos.color = (BandoUnidad == Bando.Jugador)
                ? new Color(0.2f, 0.6f, 1f, 0.8f)
                : new Color(1f, 0.2f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}
