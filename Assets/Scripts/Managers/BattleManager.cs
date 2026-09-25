// ============================================================
//  BattleManager.cs
//  Felinaria: El último presagio
//  Fase 5 – Victoria, Derrota y Game Loop
//
//  RESPONSABILIDAD:
//    - Controla las condiciones de fin de partida (Victoria / Derrota).
//    - Recopila estadísticas del combate (bajas, rondas, tiempo).
//    - Notifica a BattleResultUI y AudioManager cuando la batalla concluye.
//    - Auto-configurable en runtime.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Felinaria.Units;
using Felinaria.Combat;
using Felinaria.Audio;

namespace Felinaria.Managers
{
    public enum EstadoBatalla
    {
        EnProgreso,
        Victoria,
        Derrota
    }

    /// <summary>
    /// Gestiona las reglas de victoria/derrota y el flujo de fin de nivel.
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        private static BattleManager _instancia;
        public static BattleManager Instancia
        {
            get
            {
                if (_instancia == null)
                {
                    _instancia = FindFirstObjectByType<BattleManager>();
                    if (_instancia == null)
                    {
                        var go = new GameObject("BattleManager_Auto");
                        _instancia = go.AddComponent<BattleManager>();
                    }
                }
                return _instancia;
            }
            private set => _instancia = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInicializar()
        {
            var _ = Instancia;
        }

        // ── Estado público ─────────────────────────────────────────────────────
        public EstadoBatalla EstadoActual { get; private set; } = EstadoBatalla.EnProgreso;

        // ── Estadísticas de Batalla ────────────────────────────────────────────
        public int BajasEnemigas { get; private set; } = 0;
        public int BajasAliadas { get; private set; } = 0;
        public int DanioTotalInfligido { get; private set; } = 0;
        public float TiempoInicioBatalla { get; private set; } = 0f;
        public float DuracionBatallaSegundos => Time.time - TiempoInicioBatalla;

        // ── Eventos C# ─────────────────────────────────────────────────────────
        public event Action OnVictoria;
        public event Action OnDerrota;
        public event Action<EstadoBatalla> OnFinBatalla;

        // ── Unity Lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            if (_instancia != null && _instancia != this)
            {
                Destroy(gameObject);
                return;
            }
            _instancia = this;
            TiempoInicioBatalla = Time.time;
        }

        private void Start()
        {
            // Suscribirse a eventos de combate para monitoreo en tiempo real
            if (CombatSystem.Instancia != null)
            {
                CombatSystem.Instancia.OnAtaqueRealizado += OnAtaqueRealizado;
            }

            // Auto-instanciar UI de resultados de batalla
            _ = Felinaria.UI.BattleResultUI.Instancia;
        }

        private void OnDestroy()
        {
            if (CombatSystem.Instancia != null)
            {
                CombatSystem.Instancia.OnAtaqueRealizado -= OnAtaqueRealizado;
            }
        }

        // ── Monitoreo de Combate ───────────────────────────────────────────────
        private void OnAtaqueRealizado(ResultadoCombate res)
        {
            if (EstadoActual != EstadoBatalla.EnProgreso) return;

            if (res.Atacante != null && res.Atacante.BandoUnidad == Bando.Jugador)
            {
                DanioTotalInfligido += res.DanioInfligido;
            }

            if (res.FueEliminado)
            {
                if (res.Objetivo != null && res.Objetivo.BandoUnidad == Bando.Enemigo)
                    BajasEnemigas++;
                else if (res.Objetivo != null && res.Objetivo.BandoUnidad == Bando.Jugador)
                    BajasAliadas++;
            }

            // Verificar condiciones de victoria o derrota con breve retardo para permitir animaciones
            Invoke(nameof(VerificarCondicionesFinDeBatalla), 0.35f);
        }

        /// <summary>
        /// Evalúa si la batalla ha concluido por victoria o derrota.
        /// </summary>
        public void VerificarCondicionesFinDeBatalla()
        {
            if (EstadoActual != EstadoBatalla.EnProgreso) return;

            var unidades = FindObjectsByType<UnitController>(FindObjectsSortMode.None);
            int aliadosVivos = 0;
            int enemigosVivos = 0;

            foreach (var u in unidades)
            {
                if (u != null && u.gameObject.activeInHierarchy && u.VidaActual > 0)
                {
                    if (u.BandoUnidad == Bando.Jugador) aliadosVivos++;
                    else if (u.BandoUnidad == Bando.Enemigo) enemigosVivos++;
                }
            }

            // Condición de Victoria: Todos los enemigos eliminados
            if (enemigosVivos == 0 && aliadosVivos > 0)
            {
                DeclararVictoria();
            }
            // Condición de Derrota: Todas las unidades del jugador caídas
            else if (aliadosVivos == 0)
            {
                DeclararDerrota();
            }
        }

        private void DeclararVictoria()
        {
            EstadoActual = EstadoBatalla.Victoria;
            Debug.Log("[BattleManager] 🏆 ¡VICTORIA! Todos los enemigos han sido derrotados.");

            AudioManager.Instancia?.SFX_Victoria();

            OnVictoria?.Invoke();
            OnFinBatalla?.Invoke(EstadoBatalla.Victoria);
        }

        private void DeclararDerrota()
        {
            EstadoActual = EstadoBatalla.Derrota;
            Debug.Log("[BattleManager] 💀 ¡DERROTA! Todas las unidades aliadas han caído.");

            AudioManager.Instancia?.SFX_Derrota();

            OnDerrota?.Invoke();
            OnFinBatalla?.Invoke(EstadoBatalla.Derrota);
        }

        // ── API pública de navegación ──────────────────────────────────────────
        /// <summary>
        /// Reinicia la batalla actual con fade out animado.
        /// </summary>
        public void ReiniciarBatalla()
        {
            Debug.Log("[BattleManager] Reiniciando nivel actual...");
            SceneTransition.Instancia?.ReiniciarNivelActual(0.5f);
        }

        /// <summary>
        /// Avanza al siguiente nivel o carga la escena especificada.
        /// </summary>
        public void AvanzarSiguienteNivel(string nombreEscena = null)
        {
            string destino = !string.IsNullOrEmpty(nombreEscena) ? nombreEscena : "Nivel1";
            Debug.Log($"[BattleManager] Avanzando al siguiente nivel: '{destino}'");
            SceneTransition.Instancia?.CargarEscena(destino, 0.5f);
        }
    }
}
