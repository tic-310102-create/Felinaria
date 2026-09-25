// ============================================================
//  TurnManager.cs
//  Felinaria: El último presagio
//  Fase 1 – Prototipo Graybox
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
        /// <summary>Acceso global al TurnManager desde cualquier script.</summary>
        public static TurnManager Instancia { get; private set; }

        // ── Estado público ─────────────────────────────────────────────────────
        /// <summary>Estado actual de la máquina de turnos.</summary>
        public EstadoTurno EstadoActual { get; private set; } = EstadoTurno.Inactivo;

        /// <summary>Número de ronda actual (comienza en 1).</summary>
        public int RondaActual { get; private set; } = 0;

        // ── Eventos C# ─────────────────────────────────────────────────────────
        // Los eventos permiten que la UI, la IA y otros sistemas se enganchen
        // al cambio de turno sin que TurnManager los conozca directamente.
        // Uso: TurnManager.Instancia.OnCambioTurno += MiMetodo;

        /// <summary>
        /// Se dispara cada vez que el turno cambia de estado.
        /// Parámetro: el nuevo EstadoTurno.
        /// </summary>
        public event Action<EstadoTurno> OnCambioTurno;

        /// <summary>
        /// Se dispara al inicio de cada nueva ronda.
        /// Parámetro: el número de ronda.
        /// </summary>
        public event Action<int> OnNuevaRonda;

        // ── Unity Lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            // Patrón Singleton: solo puede existir uno.
            if (Instancia != null && Instancia != this)
            {
                Debug.LogWarning("[TurnManager] Ya existe una instancia. Destruyendo duplicado.");
                Destroy(gameObject);
                return;
            }
            Instancia = this;
        }

        private void Start()
        {
            IniciarCombate();
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
                    // Simular turno enemigo automáticamente tras un retardo.
                    StartCoroutine(EjecutarTurnoEnemigo());
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

        // ── Turno del Enemigo (IA stub) ────────────────────────────────────────
        /// <summary>
        /// Simula el turno enemigo: espera el retardo configurado
        /// y luego termina el turno del enemigo automáticamente.
        /// En Fase 3 aquí irá la lógica de IA real.
        /// </summary>
        private System.Collections.IEnumerator EjecutarTurnoEnemigo()
        {
            Debug.Log($"[TurnManager] Enemigo pensando... ({RetardoTurnoEnemigo}s)");
            yield return new WaitForSeconds(RetardoTurnoEnemigo);

            // TODO Fase 3: aquí la IA moverá sus unidades antes de llamar a esto.
            Debug.Log("[TurnManager] Turno enemigo terminado (IA stub).");
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
