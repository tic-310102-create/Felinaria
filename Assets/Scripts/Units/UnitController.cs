// ============================================================
//  UnitController.cs
//  Felinaria: El último presagio
//  Fase 1 – Prototipo Graybox
//
//  RESPONSABILIDAD:
//    - Representa a cualquier unidad en el tablero (héroe o enemigo).
//    - Almacena estadísticas básicas configurables desde el Inspector.
//    - Mueve a la unidad entre celdas con interpolación suave (sin teletransporte).
//    - Comunica su posición al GridManager para actualizar el mapa de ocupación.
//    - Respeta el turno activo (pregunta al TurnManager antes de actuar).
//
//  CÓMO FUNCIONA (resumen para principiantes):
//    Cada personaje tiene este script. Cuando quieres moverlo,
//    llamas a MoverACelda(col, fila) y el script se desliza suavemente
//    hasta allí usando MoveTowards, sin saltar bruscamente.
// ============================================================

using System.Collections;
using UnityEngine;
using Felinaria.Grid;
using Felinaria.Managers;

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
        // ── Inspector: Identidad ───────────────────────────────────────────────
        [Header("Identidad de la Unidad")]
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

        // ── Inspector: Estadísticas de combate ────────────────────────────────
        [Header("Estadísticas de Combate")]
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

        // Referencia al SpriteRenderer para cambiar colores.
        private SpriteRenderer _spriteRenderer;

        // Coroutine activa de movimiento (para poder cancelarla si fuera necesario).
        private Coroutine _corrutinaMover;

        // ── Unity Lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            VidaActual = VidaMaxima;
        }

        private void Start()
        {
            // Posicionar la unidad en su celda inicial al comenzar la escena.
            InicializarEnCuadricula();
        }

        // ── Inicialización ─────────────────────────────────────────────────────
        /// <summary>
        /// Coloca la unidad en su celda de inicio y la registra en GridManager.
        /// </summary>
        private void InicializarEnCuadricula()
        {
            if (GridManager.Instancia == null)
            {
                Debug.LogError($"[UnitController] '{NombreUnidad}': No se encontró GridManager en la escena.");
                return;
            }

            // Verificar que la celda inicial existe y está libre.
            if (!GridManager.Instancia.EstaCeldaLibre(ColInicial, FilaInicial))
            {
                Debug.LogWarning($"[UnitController] '{NombreUnidad}': Celda inicial ({ColInicial},{FilaInicial}) " +
                                 "está ocupada o no existe. Usando (0,0).");
                ColInicial  = 0;
                FilaInicial = 0;
            }

            // Mover el transform directamente (solo al inicio, sin interpolación).
            Coordenada = new Vector2Int(ColInicial, FilaInicial);
            transform.position = GridManager.Instancia.CoordenadaAMundo(Coordenada);

            // Marcar la celda como ocupada en el GridManager.
            GridManager.Instancia.SetOcupacion(Coordenada.x, Coordenada.y, true);

            Debug.Log($"[UnitController] '{NombreUnidad}' inicializado en ({Coordenada.x},{Coordenada.y}).");
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
        /// Marca la unidad como "ya actuó" y cambia su color visual.
        /// Llamado automáticamente al terminar de moverse.
        /// </summary>
        private void MarcarComoUsada()
        {
            YaActuoEsteTurno = true;
            if (_spriteRenderer != null)
                _spriteRenderer.color = ColorUsado;
        }

        /// <summary>
        /// Reinicia el estado de la unidad al comienzo de un nuevo turno.
        /// Llamado por TurnManager al cambiar de turno.
        /// </summary>
        public void ReiniciarTurno()
        {
            YaActuoEsteTurno = false;
            if (_spriteRenderer != null)
                _spriteRenderer.color = ColorNormal;
        }

        // ── Combate (stub para Fase 2) ─────────────────────────────────────────
        /// <summary>
        /// Aplica daño a la unidad. Si la vida llega a 0, la elimina.
        /// (Implementación completa en Fase 2.)
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
            // TODO Fase 2: animación de muerte, eventos, drops.
            Destroy(gameObject);
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
