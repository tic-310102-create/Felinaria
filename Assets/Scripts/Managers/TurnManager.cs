// ============================================================
//  TurnManager.cs
//  Felinaria: El último presagio
//  Fase 3 – IA Enemiga y Mapa (actualizado)
//
//  RESPONSABILIDAD:
//    - Define la máquina de estados de turnos del juego.
//    - Controla qué bando puede actuar en cada momento.
//    - Notifica a todas las unidades cuando cambia el turno.
//    - Lleva el conteo de rondas.
//    - Expone eventos C# para que otros sistemas reaccionen al cambio de turno.
//
//  CÓMO FUNCIONA (resumen para principiantes):
//    Hay un Enum con los posibles estados del juego (TurnoJugador, TurnoEnemigo, etc.).
//    El TurnManager siempre sabe en qué estado está y bloquea las acciones
//    de las unidades que no corresponden al turno activo.
//    Cuando el jugador pulsa "Terminar Turno", se invoca TerminarTurnoActual()
//    y el sistema pasa al siguiente estado.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Felinaria.Units;

namespace Felinaria.Managers
{
    // ─── Enum: estados posibles del flujo de turnos ───────────────────────────
    /// <summary>
    /// Representa en qué fase del flujo de combate se encuentra el juego.
    /// </summary>
    public enum EstadoTurno
    {
        Inactivo,       // El juego no ha comenzado o está en pausa.
        TurnoJugador,   // El jugador puede mover y atacar con sus unidades.
        TurnoEnemigo,   // Las unidades enemigas (IA) realizan sus acciones.
        FinDeRonda,     // Ambos bandos actuaron; se resetean los turnos.
        FinDeJuego      // Condición de victoria o derrota alcanzada.
    }

    // ─── TurnManager ─────────────────────────────────────────────────────────
    /// <summary>
    /// Singleton que controla el flujo de turnos del combate.
    /// Se coloca en el GameObject "GameMaster".
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Configuración de Turnos")]
        [Tooltip("¿Qué bando actúa primero al iniciar el combate?")]
        public Bando BandoInicial = Bando.Jugador;

        [Tooltip("Segundos que la IA espera antes de ejecutar su turno " +
                 "(para que el jugador vea qué está pasando).")]
        [Range(0.5f, 5f)]
        public float RetardoTurnoEnemigo = 1.5f;

        [Header("Unidades Registradas")]
        [Tooltip("Lista de todas las unidades del JUGADOR en la escena. " +
                 "Arrástralas desde el Hierarchy.")]
        public List<UnitController> UnidadesJugador = new List<UnitController>();

        [Tooltip("Lista de todas las unidades ENEMIGAS en la escena. " +
                 "Arrástralas desde el Hierarchy.")]
        public List<UnitController> UnidadesEnemigo = new List<UnitController>();

        // ── Singleton ──────────────────────────────────────────────────────────
        private static TurnManager _instancia;
        private static bool _aplicacionCerrando = false;

        /// <summary>Indica si existe una instancia activa sin forzar su creación.</summary>
        public static bool InstanciaExiste => _instancia != null && !_aplicacionCerrando;

        /// <summary>Acceso global al TurnManager con auto-instanciación segura.</summary>
        public static TurnManager Instancia
        {
            get
            {
                if (_aplicacionCerrando) return null;
                if (_instancia == null)
                {
                    _instancia = FindFirstObjectByType<TurnManager>();
                    if (_instancia == null && Application.isPlaying)
                    {
                        var go = new GameObject("TurnManager_Auto");
                        _instancia = go.AddComponent<TurnManager>();
                    }
                }
                return _instancia;
            }
            private set => _instancia = value;
        }

        // ── Estado público ─────────────────────────────────────────────────────
        /// <summary>Estado actual de la máquina de turnos.</summary>
        public EstadoTurno EstadoActual { get; private set; } = EstadoTurno.Inactivo;

        /// <summary>Número de ronda actual (comienza en 1).</summary>
        public int RondaActual { get; set; } = 0;

        // ── Eventos C# ─────────────────────────────────────────────────────────
        public event Action<EstadoTurno> OnCambioTurno;
        public event Action<int> OnNuevaRonda;

        // ── Unity Lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            _aplicacionCerrando = false;
            if (_instancia != null && _instancia != this)
            {
                Debug.LogWarning("[TurnManager] Ya existe una instancia. Destruyendo duplicado.");
                Destroy(gameObject);
                return;
            }
            _instancia = this;
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
            AutoDescubrirUnidades();
            IniciarCombate();
        }

        /// <summary>
        /// Busca todas las unidades presentes en la escena y las clasifica por bando.
        /// </summary>
        private void AutoDescubrirUnidades()
        {
            var todas = FindObjectsByType<UnitController>(FindObjectsSortMode.None);
            foreach (var u in todas)
            {
                if (u.BandoUnidad == Bando.Jugador && !UnidadesJugador.Contains(u))
                    UnidadesJugador.Add(u);
                else if (u.BandoUnidad == Bando.Enemigo && !UnidadesEnemigo.Contains(u))
                    UnidadesEnemigo.Add(u);
            }
        }

        // ── Flujo principal ────────────────────────────────────────────────────
        /// <summary>
        /// Arranca el combate. Llamado automáticamente al iniciar la escena.
        /// También puede llamarse manualmente para reiniciar el combate.
        /// </summary>
        public void IniciarCombate()
        {
            RondaActual = 0;
            Debug.Log("[TurnManager] ¡Combate iniciado!");
            IniciarNuevaRonda();
        }

        /// <summary>
        /// Inicia una nueva ronda: reinicia todas las unidades y cede el turno
        /// al bando correspondiente.
        /// </summary>
        private void IniciarNuevaRonda()
        {
            RondaActual++;
            Debug.Log($"[TurnManager] ── Ronda {RondaActual} ──");

            // Notificar a los suscriptores del evento.
            OnNuevaRonda?.Invoke(RondaActual);

            // Reiniciar todas las unidades para que puedan actuar.
            ReiniciarTodasLasUnidades();

            // Comenzar con el bando configurado.
            if (BandoInicial == Bando.Jugador)
                CambiarEstado(EstadoTurno.TurnoJugador);
            else
                CambiarEstado(EstadoTurno.TurnoEnemigo);
        }

        /// <summary>
        /// Llama ReiniciarTurno() en todas las unidades de ambos bandos.
        /// </summary>
        private void ReiniciarTodasLasUnidades()
        {
            foreach (var u in UnidadesJugador) u?.ReiniciarTurno();
            foreach (var u in UnidadesEnemigo) u?.ReiniciarTurno();
        }

        /// <summary>
        /// El jugador (o la IA) llama a este método cuando terminó de actuar.
        /// Avanza la máquina de estados al siguiente turno.
        /// </summary>
        public void TerminarTurnoActual()
        {
            switch (EstadoActual)
            {
                case EstadoTurno.TurnoJugador:
                    // Pasa al turno del enemigo.
                    CambiarEstado(EstadoTurno.TurnoEnemigo);
                    // Fase 3: el EnemyAI se activa automáticamente por evento OnCambioTurno.
                    // Fallback: si no hay EnemyAI, usar el stub original.
                    if (Felinaria.AI.EnemyAI.Instancia == null)
                        StartCoroutine(EjecutarTurnoEnemigoFallback());
                    break;

                case EstadoTurno.TurnoEnemigo:
                    // El enemigo terminó, empieza nueva ronda.
                    CambiarEstado(EstadoTurno.FinDeRonda);
                    IniciarNuevaRonda();
                    break;

                default:
                    Debug.LogWarning($"[TurnManager] TerminarTurnoActual llamado en estado: {EstadoActual}");
                    break;
            }
        }

        /// <summary>
        /// Cambia el estado interno y dispara el evento OnCambioTurno.
        /// </summary>
        private void CambiarEstado(EstadoTurno nuevoEstado)
        {
            EstadoActual = nuevoEstado;
            Debug.Log($"[TurnManager] Estado → {nuevoEstado}");
            OnCambioTurno?.Invoke(nuevoEstado);
        }

        // ── Turno del Enemigo (Fallback sin EnemyAI) ─────────────────────────
        /// <summary>
        /// Fallback: si no existe EnemyAI en la escena, usa este stub.
        /// Cuando EnemyAI está presente, se activa por el evento OnCambioTurno
        /// y llama a TerminarTurnoActual() al finalizar.
        /// </summary>
        private System.Collections.IEnumerator EjecutarTurnoEnemigoFallback()
        {
            Debug.Log($"[TurnManager] Enemigo pensando (fallback)... ({RetardoTurnoEnemigo}s)");
            yield return new WaitForSeconds(RetardoTurnoEnemigo);

            Debug.Log("[TurnManager] Turno enemigo terminado (fallback sin EnemyAI).");
            TerminarTurnoActual();
        }

        // ── API pública ────────────────────────────────────────────────────────
        /// <summary>
        /// Indica si es el turno del bando de la unidad dada.
        /// Usado por UnitController para validar si puede actuar.
        /// </summary>
        public bool EsTurnoDeUnidad(UnitController unidad)
        {
            if (EstadoActual == EstadoTurno.TurnoJugador && unidad.BandoUnidad == Bando.Jugador)
                return true;
            if (EstadoActual == EstadoTurno.TurnoEnemigo && unidad.BandoUnidad == Bando.Enemigo)
                return true;
            return false;
        }

        /// <summary>
        /// Registra dinámicamente una nueva unidad en el sistema de turnos.
        /// Útil si las unidades se instancian en runtime (no desde el Inspector).
        /// </summary>
        public void RegistrarUnidad(UnitController unidad)
        {
            if (unidad.BandoUnidad == Bando.Jugador)
                UnidadesJugador.Add(unidad);
            else
                UnidadesEnemigo.Add(unidad);

            Debug.Log($"[TurnManager] Unidad '{unidad.NombreUnidad}' registrada como {unidad.BandoUnidad}.");
        }

        /// <summary>
        /// Elimina una unidad del registro (llamar al morir).
        /// </summary>
        public void DesregistrarUnidad(UnitController unidad)
        {
            UnidadesJugador.Remove(unidad);
            UnidadesEnemigo.Remove(unidad);

            // Verificar condición de fin de juego.
            VerificarCondicionFin();
        }

        /// <summary>
        /// Verifica si algún bando quedó sin unidades (condición de victoria/derrota).
        /// </summary>
        private void VerificarCondicionFin()
        {
            // Limpiar nulls (unidades destruidas que quedaron en la lista).
            UnidadesJugador.RemoveAll(u => u == null);
            UnidadesEnemigo.RemoveAll(u => u == null);

            if (UnidadesJugador.Count == 0)
            {
                Debug.Log("[TurnManager] ¡DERROTA! Todas las unidades del jugador han caído.");
                CambiarEstado(EstadoTurno.FinDeJuego);
                return;
            }

            if (UnidadesEnemigo.Count == 0)
            {
                Debug.Log("[TurnManager] ¡VICTORIA! Todos los enemigos han sido derrotados.");
                CambiarEstado(EstadoTurno.FinDeJuego);
            }
        }

        // ── Utilidad de debug ──────────────────────────────────────────────────
        /// <summary>
        /// Método de prueba rápida para terminar el turno con la tecla Enter.
        /// ¡Deshabilitar en la build final!
        /// </summary>
        [Header("Debug (solo en prototipo)")]
        [Tooltip("Activa la tecla Enter para terminar el turno manualmente (solo pruebas).")]
        public bool TeclaTerminarTurnoActiva = true;

        private void Update()
        {
            if (!TeclaTerminarTurnoActiva) return;

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Debug.Log("[TurnManager] [DEBUG] Terminar turno por teclado.");
                TerminarTurnoActual();
            }
        }
    }
}
