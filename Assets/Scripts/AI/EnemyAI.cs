// ============================================================
//  EnemyAI.cs
//  Felinaria: El último presagio
//  Fase 3 – IA Enemiga y Mapa
//
//  RESPONSABILIDAD:
//    - Controla la lógica de TODAS las unidades enemigas durante su turno.
//    - Se activa automáticamente cuando TurnManager cambia a TurnoEnemigo.
//    - Para cada enemigo: busca al héroe más cercano, calcula ruta con A*,
//      camina suavemente hasta el límite de su rango, ataca si puede,
//      y pasa al siguiente enemigo.
//    - Al terminar de mover todos los enemigos, devuelve el turno al TurnManager.
//
//  MÁQUINA DE ESTADOS por unidad:
//    Idle → BuscandoObjetivo → Moviendose → Atacando → Terminado
//
//  CÓMO FUNCIONA (para principiantes):
//    Este script se coloca en el GameMaster (junto a TurnManager).
//    Cuando le toca al enemigo, la IA toma el control, mueve cada unidad
//    una por una (para que el jugador pueda ver qué pasa) y luego
//    devuelve el turno. Todo es automático.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Felinaria.Units;
using Felinaria.Grid;
using Felinaria.Combat;
using Felinaria.Managers;

namespace Felinaria.AI
{
    // ─── Enum: estado de la IA por unidad ────────────────────────────────────
    /// <summary>
    /// Estado de procesamiento de cada unidad enemiga durante su turno.
    /// </summary>
    public enum EstadoIA
    {
        Idle,               // Esperando a que le toque.
        BuscandoObjetivo,   // Analizando cuál héroe atacar.
        Moviendose,         // Caminando por la ruta calculada.
        Atacando,           // Ejecutando ataque.
        Terminado           // Ya actuó esta ronda.
    }

    /// <summary>
    /// Singleton que gestiona la IA de todos los enemigos.
    /// Se coloca en el GameObject "GameMaster".
    /// </summary>
    public class EnemyAI : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        private static EnemyAI _instancia;
        private static bool _aplicacionCerrando = false;

        /// <summary>Indica si existe una instancia activa sin forzar su creación.</summary>
        public static bool InstanciaExiste => _instancia != null && !_aplicacionCerrando;

        public static EnemyAI Instancia
        {
            get
            {
                if (_aplicacionCerrando) return null;
                if (_instancia == null)
                {
                    _instancia = FindFirstObjectByType<EnemyAI>();
                    if (_instancia == null && Application.isPlaying)
                    {
                        var go = new GameObject("EnemyAI_Auto");
                        _instancia = go.AddComponent<EnemyAI>();
                    }
                }
                return _instancia;
            }
            private set => _instancia = value;
        }

        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Configuración de la IA")]
        [Tooltip("Segundos de pausa antes de que el primer enemigo actúe. " +
                 "Da tiempo al jugador de ver que cambió el turno.")]
        [Range(0.3f, 3f)]
        public float RetardoInicioTurno = 0.8f;

        [Tooltip("Segundos de pausa entre la acción de un enemigo y el siguiente.")]
        [Range(0.2f, 2f)]
        public float RetardoEntreEnemigos = 0.5f;

        [Tooltip("Segundos de pausa después de moverse y antes de atacar.")]
        [Range(0.1f, 1f)]
        public float RetardoAntesDeAtacar = 0.3f;

        [Header("Comportamiento")]
        [Tooltip("Si es true, el enemigo prioriza atacar al héroe con menos vida. " +
                 "Si es false, prioriza al más cercano.")]
        public bool PriorizarHeroeDebil = false;

        // ── Estado interno ─────────────────────────────────────────────────────
        /// <summary>True mientras la IA está procesando el turno enemigo.</summary>
        public bool EstaProcesando { get; private set; }

        private bool _suscrito = false;

        // ── Unity Lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            _aplicacionCerrando = false;
            if (_instancia != null && _instancia != this)
            {
                Debug.LogWarning("[EnemyAI] Ya existe una instancia. Destruyendo duplicado.");
                Destroy(gameObject);
                return;
            }
            _instancia = this;
        }

        private void Start()
        {
            // Suscribirse al evento de cambio de turno.
            // Usamos Start en lugar de OnEnable para garantizar que TurnManager ya exista.
            SuscribirseATurnManager();

            // Si ya estamos en turno enemigo (ej: EnemyAI fue auto-creada durante el cambio),
            // disparar la IA inmediatamente para no perder el evento.
            if (_suscrito && TurnManager.Instancia != null &&
                TurnManager.Instancia.EstadoActual == EstadoTurno.TurnoEnemigo &&
                !EstaProcesando)
            {
                StartCoroutine(EjecutarTurnoEnemigo());
            }
        }

        private void OnEnable()
        {
            // Re-suscribirse si el objeto fue desactivado y reactivado.
            if (_suscrito) return;
            SuscribirseATurnManager();
        }

        private void OnApplicationQuit()
        {
            _aplicacionCerrando = true;
        }

        private void OnDisable()
        {
            if (TurnManager.InstanciaExiste && _suscrito)
            {
                TurnManager.Instancia.OnCambioTurno -= OnCambioTurno;
                _suscrito = false;
            }
        }

        private void OnDestroy()
        {
            if (TurnManager.InstanciaExiste && _suscrito)
            {
                TurnManager.Instancia.OnCambioTurno -= OnCambioTurno;
                _suscrito = false;
            }

            if (_instancia == this)
            {
                _instancia = null;
            }
        }

        private void SuscribirseATurnManager()
        {
            if (_suscrito) return;
            if (TurnManager.Instancia != null)
            {
                TurnManager.Instancia.OnCambioTurno += OnCambioTurno;
                _suscrito = true;
                Debug.Log("[EnemyAI] Suscrito al evento OnCambioTurno del TurnManager.");
            }
            else
            {
                // Si TurnManager aún no existe, reintentar en el siguiente frame.
                StartCoroutine(ReintentarSuscripcion());
            }
        }

        private System.Collections.IEnumerator ReintentarSuscripcion()
        {
            yield return null; // Esperar un frame
            if (!_suscrito && TurnManager.Instancia != null)
            {
                TurnManager.Instancia.OnCambioTurno += OnCambioTurno;
                _suscrito = true;
                Debug.Log("[EnemyAI] Suscrito al evento OnCambioTurno (reintento).");
            }
        }

        // ── Escucha del evento de turno ────────────────────────────────────────
        /// <summary>
        /// Callback del evento TurnManager.OnCambioTurno.
        /// Se activa solo cuando el turno pasa a "TurnoEnemigo".
        /// </summary>
        private void OnCambioTurno(EstadoTurno nuevoEstado)
        {
            if (nuevoEstado == EstadoTurno.TurnoEnemigo && !EstaProcesando)
            {
                StartCoroutine(EjecutarTurnoEnemigo());
            }
        }

        // ── Flujo principal del turno enemigo ──────────────────────────────────
        /// <summary>
        /// Procesa secuencialmente todas las unidades enemigas:
        /// buscar objetivo → moverse → atacar → siguiente.
        /// </summary>
        private IEnumerator EjecutarTurnoEnemigo()
        {
            EstaProcesando = true;
            Debug.Log("[EnemyAI] ═══ TURNO ENEMIGO: Procesando IA ═══");

            // Pausa inicial para que el jugador vea el cambio de turno.
            yield return new WaitForSeconds(RetardoInicioTurno);

            // Obtener lista de enemigos vivos (copiar para evitar modificar durante iteración).
            var enemigos = new List<UnitController>(TurnManager.Instancia.UnidadesEnemigo);
            enemigos.RemoveAll(e => e == null);

            int indice = 0;
            foreach (var enemigo in enemigos)
            {
                // Saltar unidades destruidas o que ya actuaron.
                if (enemigo == null || enemigo.YaActuoEsteTurno) continue;

                indice++;
                Debug.Log($"[EnemyAI] ── Unidad {indice}/{enemigos.Count}: '{enemigo.NombreUnidad}' ──");

                // Procesar esta unidad.
                yield return StartCoroutine(ProcesarUnidadEnemiga(enemigo));

                // Pausa entre enemigos.
                if (indice < enemigos.Count)
                    yield return new WaitForSeconds(RetardoEntreEnemigos);
            }

            Debug.Log("[EnemyAI] ═══ TURNO ENEMIGO: Completado ═══");
            EstaProcesando = false;

            // Devolver el turno al TurnManager.
            TurnManager.Instancia?.TerminarTurnoActual();
        }

        /// <summary>
        /// Procesa una sola unidad enemiga: busca objetivo, calcula ruta,
        /// se mueve, intenta atacar.
        /// </summary>
        private IEnumerator ProcesarUnidadEnemiga(UnitController enemigo)
        {
            // ── Paso 1: Buscar al héroe más conveniente ───────────────────────
            UnitController objetivo = BuscarMejorObjetivo(enemigo);

            if (objetivo == null)
            {
                Debug.Log($"[EnemyAI] '{enemigo.NombreUnidad}': No hay héroes vivos. Esperando.");
                enemigo.MarcarComoUsadaPublico();
                yield break;
            }

            Debug.Log($"[EnemyAI] '{enemigo.NombreUnidad}' → Objetivo: '{objetivo.NombreUnidad}' " +
                      $"en ({objetivo.Coordenada.x},{objetivo.Coordenada.y})");

            // ── Paso 2: ¿Ya está en rango de ataque? ──────────────────────────
            bool yaEnRango = CombatSystem.Instancia != null &&
                             CombatSystem.Instancia.EstaEnRangoDeAtaque(enemigo, objetivo);

            // ── Paso 3: Si no está en rango, moverse hacia el objetivo ────────
            if (!yaEnRango)
            {
                yield return StartCoroutine(MoverHaciaObjetivo(enemigo, objetivo));

                // Re-evaluar si ahora está en rango tras moverse.
                yaEnRango = CombatSystem.Instancia != null &&
                            CombatSystem.Instancia.EstaEnRangoDeAtaque(enemigo, objetivo);
            }

            // ── Paso 4: Atacar si está en rango ───────────────────────────────
            if (yaEnRango && !enemigo.YaActuoEsteTurno)
            {
                yield return new WaitForSeconds(RetardoAntesDeAtacar);
                yield return StartCoroutine(RealizarAtaque(enemigo, objetivo));
            }
            else if (!enemigo.YaActuoEsteTurno)
            {
                // Se movió pero no llegó a rango: terminar turno de esta unidad.
                Debug.Log($"[EnemyAI] '{enemigo.NombreUnidad}': No alcanzó al objetivo. Esperando.");
                enemigo.MarcarComoUsadaPublico();
            }
        }

        // ── Búsqueda de objetivo ───────────────────────────────────────────────
        /// <summary>
        /// Busca al héroe más conveniente para atacar.
        /// Por defecto: el más cercano. Si PriorizarHeroeDebil está activo:
        /// el que tenga menos vida actual.
        /// </summary>
        private UnitController BuscarMejorObjetivo(UnitController enemigo)
        {
            var heroes = TurnManager.Instancia?.UnidadesJugador;
            if (heroes == null || heroes.Count == 0) return null;

            UnitController mejorObjetivo = null;
            int mejorPuntaje = int.MaxValue;

            foreach (var heroe in heroes)
            {
                if (heroe == null) continue;

                int distancia = DistanciaManhattan(enemigo.Coordenada, heroe.Coordenada);

                int puntaje;
                if (PriorizarHeroeDebil)
                {
                    // Prioridad por vida baja. Si empatan en vida, elige al más cercano.
                    puntaje = heroe.VidaActual * 100 + distancia;
                }
                else
                {
                    // Prioridad por cercanía. Si empatan en distancia, elige al de menor vida.
                    puntaje = distancia * 100 + heroe.VidaActual;
                }

                if (puntaje < mejorPuntaje)
                {
                    mejorPuntaje = puntaje;
                    mejorObjetivo = heroe;
                }
            }

            return mejorObjetivo;
        }

        // ── Movimiento de la IA ────────────────────────────────────────────────
        /// <summary>
        /// Calcula la ruta A* hacia el objetivo y mueve la unidad enemiga
        /// paso a paso (celda por celda) con interpolación suave,
        /// respetando su rango de movimiento.
        /// </summary>
        private IEnumerator MoverHaciaObjetivo(UnitController enemigo, UnitController objetivo)
        {
            // Calcular ruta limitada al rango de movimiento.
            var ruta = Pathfinding.BuscarRutaConRango(
                enemigo.Coordenada,
                objetivo.Coordenada,
                enemigo.RangoMovimiento
            );

            if (ruta.Count == 0)
            {
                Debug.Log($"[EnemyAI] '{enemigo.NombreUnidad}': No se encontró ruta válida.");
                yield break;
            }

            // Verificar que la última celda de la ruta no sea la celda del objetivo
            // (no queremos terminar encima de otra unidad).
            Vector2Int ultimoPaso = ruta[ruta.Count - 1];
            Cell celdaFinal = GridManager.Instancia.ObtenerCelda(ultimoPaso);
            if (celdaFinal != null && celdaFinal.EstaOcupada)
            {
                // Quitar el último paso si está ocupado.
                ruta.RemoveAt(ruta.Count - 1);
                if (ruta.Count == 0) yield break;
            }

            Debug.Log($"[EnemyAI] '{enemigo.NombreUnidad}': Ruta de {ruta.Count} pasos calculada.");

            // Mover celda por celda con interpolación suave.
            yield return StartCoroutine(MoverPorRuta(enemigo, ruta));
        }

        /// <summary>
        /// Mueve la unidad enemiga paso a paso a lo largo de la ruta calculada.
        /// Cada paso usa interpolación suave (MoveTowards) para evitar teletransportes.
        /// NO marca la unidad como "ya actuó" (eso lo hace el ataque o el MarcarComoUsada).
        /// </summary>
        private IEnumerator MoverPorRuta(UnitController enemigo, List<Vector2Int> ruta)
        {
            foreach (var paso in ruta)
            {
                // Verificar que la celda siga libre (otra unidad podría haberse movido).
                if (!GridManager.Instancia.EstaCeldaLibre(paso.x, paso.y))
                {
                    Debug.Log($"[EnemyAI] '{enemigo.NombreUnidad}': Celda ({paso.x},{paso.y}) bloqueada. Deteniendo.");
                    break;
                }

                // Liberar celda actual.
                GridManager.Instancia.SetOcupacion(enemigo.Coordenada.x, enemigo.Coordenada.y, false);

                // Calcular destino en mundo.
                Vector3 posDestino = GridManager.Instancia.CoordenadaAMundo(paso);

                // Actualizar coordenada lógica y ocupar la nueva celda.
                // Usamos reflexión ligera: accedemos a la propiedad Coordenada
                // indirectamente porque MoverACelda tiene validaciones de turno.
                SetCoordenadaEnemigo(enemigo, paso);
                GridManager.Instancia.SetOcupacion(paso.x, paso.y, true);

                // Interpolación suave celda a celda.
                while (Vector3.Distance(enemigo.transform.position, posDestino) > 0.001f)
                {
                    enemigo.transform.position = Vector3.MoveTowards(
                        enemigo.transform.position,
                        posDestino,
                        enemigo.VelocidadMovimiento * Time.deltaTime
                    );
                    yield return null;
                }

                // Snap al centro exacto.
                enemigo.transform.position = posDestino;
            }
        }

        /// <summary>
        /// Actualiza la coordenada lógica del enemigo directamente.
        /// Necesario porque MoverACelda() valida turno y ya-actuó,
        /// y la IA necesita mover sin esas restricciones internas.
        /// </summary>
        private void SetCoordenadaEnemigo(UnitController enemigo, Vector2Int nuevaCoordenada)
        {
            // Accedemos al campo Coordenada del UnitController usando
            // el patrón de reflexión segura con propiedad interna.
            // Como Coordenada es { get; private set; }, necesitamos usar
            // el método de movimiento directo que expone UnitController.
            //
            // Para evitar tocar la encapsulación, usamos un enfoque limpio:
            // Actualizamos la posición visual y la coordenada se sincroniza.
            // Pero como Coordenada es private set, añadimos un método helper
            // en UnitController (ver actualización de UnitController Fase 3).
            enemigo.SetCoordenadaDirecta(nuevaCoordenada);
        }

        // ── Ataque de la IA ────────────────────────────────────────────────────
        /// <summary>
        /// Ejecuta el ataque del enemigo hacia el héroe objetivo
        /// a través del CombatSystem.
        /// </summary>
        private IEnumerator RealizarAtaque(UnitController enemigo, UnitController objetivo)
        {
            if (CombatSystem.Instancia == null)
            {
                Debug.LogError("[EnemyAI] No se encontró CombatSystem.");
                yield break;
            }

            if (objetivo == null)
            {
                Debug.Log($"[EnemyAI] '{enemigo.NombreUnidad}': Objetivo eliminado antes de atacar.");
                yield break;
            }

            Debug.Log($"[EnemyAI] '{enemigo.NombreUnidad}' ataca a '{objetivo.NombreUnidad}'!");

            // El CombatSystem maneja daño, animación, eventos y marcado de "ya actuó".
            CombatSystem.Instancia.EjecutarAtaque(enemigo, objetivo);

            // Esperar a que termine la animación de ataque.
            yield return new WaitForSeconds(CombatSystem.Instancia.DuracionAnimacionAtaque + 0.1f);
        }

        // ── Utilidades ─────────────────────────────────────────────────────────
        /// <summary>
        /// Distancia Manhattan = |Δcol| + |Δfila|.
        /// </summary>
        private int DistanciaManhattan(Vector2Int a, Vector2Int b)
            => Mathf.Abs(b.x - a.x) + Mathf.Abs(b.y - a.y);
    }
}
