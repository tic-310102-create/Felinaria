// ============================================================
//  CombatSystem.cs
//  Felinaria: El último presagio
//  Fase 2 – Combate y Stats
//
//  RESPONSABILIDAD:
//    - Calcula y aplica daño entre unidades.
//    - Valida rango de ataque Manhattan entre atacante y objetivo.
//    - Actualiza la HealthBar del objetivo tras recibir daño.
//    - Notifica al TurnManager cuando una unidad muere.
//    - Expone eventos C# para que la UI reaccione (flash de daño, etc.).
//
//  FÓRMULA DE DAÑO (Fase 2, básica):
//    Daño = max(1, AtaqueAtacante - DefensaObjetivo)
//    La vida nunca baja de 0.
// ============================================================

using System;
using UnityEngine;
using Felinaria.Units;
using Felinaria.Managers;

namespace Felinaria.Combat
{
    /// <summary>
    /// Resultado detallado de un cálculo de combate.
    /// Se pasa como argumento en el evento OnAtaqueRealizado.
    /// </summary>
    [System.Serializable]
    public struct ResultadoCombate
    {
        /// <summary>Unidad que atacó.</summary>
        public UnitController Atacante;

        /// <summary>Unidad que recibió el daño.</summary>
        public UnitController Objetivo;

        /// <summary>Daño final aplicado (después de defensa).</summary>
        public int DanioAplicado;

        /// <summary>True si el objetivo fue eliminado por este ataque.</summary>
        public bool ObjetivoEliminado;

        /// <summary>Vida restante del objetivo después del ataque.</summary>
        public int VidaRestanteObjetivo;
    }

    /// <summary>
    /// Singleton que gestiona todos los cálculos y ejecución de ataques.
    /// Se coloca en el GameObject "GameMaster" junto al TurnManager.
    /// </summary>
    public class CombatSystem : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        private static CombatSystem _instancia;
        private static bool _aplicacionCerrando = false;

        /// <summary>Indica si existe una instancia activa sin forzar su creación.</summary>
        public static bool InstanciaExiste => _instancia != null && !_aplicacionCerrando;

        /// <summary>Acceso global al CombatSystem con auto-instanciación segura.</summary>
        public static CombatSystem Instancia
        {
            get
            {
                if (_aplicacionCerrando) return null;
                if (_instancia == null)
                {
                    _instancia = FindFirstObjectByType<CombatSystem>();
                    if (_instancia == null && Application.isPlaying)
                    {
                        var go = new GameObject("CombatSystem_Auto");
                        _instancia = go.AddComponent<CombatSystem>();
                    }
                }
                return _instancia;
            }
            private set => _instancia = value;
        }

        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Configuración de Combate")]
        [Tooltip("Daño mínimo que siempre se aplica, incluso si la defensa " +
                 "es mayor que el ataque.")]
        [Range(0, 10)]
        public int DanioMinimo = 1;

        [Tooltip("Tiempo que tarda la animación de ataque (en segundos). " +
                 "Durante este tiempo se bloquean las acciones.")]
        [Range(0.1f, 2f)]
        public float DuracionAnimacionAtaque = 0.3f;

        // ── Eventos C# ────────────────────────────────────────────────────────
        /// <summary>
        /// Se dispara cada vez que un ataque se ejecuta exitosamente.
        /// Parámetro: ResultadoCombate con todos los detalles.
        /// La UI y la HealthBar se suscriben a este evento.
        /// </summary>
        public event Action<ResultadoCombate> OnAtaqueRealizado;

        /// <summary>
        /// Se dispara cuando una unidad es eliminada por combate.
        /// Parámetro: la UnitController que murió.
        /// </summary>
        public event Action<UnitController> OnUnidadEliminada;

        // ── Unity Lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            _aplicacionCerrando = false;
            if (_instancia != null && _instancia != this)
            {
                Debug.LogWarning("[CombatSystem] Ya existe una instancia. Destruyendo duplicado.");
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

        // ── API pública ────────────────────────────────────────────────────────

        /// <summary>
        /// Intenta ejecutar un ataque del atacante hacia el objetivo.
        /// Valida turno, rango y estado antes de aplicar daño.
        /// </summary>
        /// <param name="atacante">La unidad que ataca.</param>
        /// <param name="objetivo">La unidad que recibe el ataque.</param>
        /// <returns>True si el ataque fue ejecutado.</returns>
        public bool EjecutarAtaque(UnitController atacante, UnitController objetivo)
        {
            // ── Validación 1: ¿Es el turno correcto? ───────────────────────────
            if (TurnManager.Instancia != null && !TurnManager.Instancia.EsTurnoDeUnidad(atacante))
            {
                Debug.Log($"[CombatSystem] '{atacante.NombreUnidad}' no puede atacar: no es su turno.");
                return false;
            }

            // ── Validación 2: ¿Ya actuó esta unidad? ──────────────────────────
            if (atacante.YaActuoEsteTurno)
            {
                Debug.Log($"[CombatSystem] '{atacante.NombreUnidad}' ya actuó este turno.");
                return false;
            }

            // ── Validación 3: ¿Está en movimiento? ────────────────────────────
            if (atacante.EstaMoviendose)
            {
                Debug.Log($"[CombatSystem] '{atacante.NombreUnidad}' está en movimiento.");
                return false;
            }

            // ── Validación 4: ¿El objetivo es del mismo bando? ────────────────
            if (atacante.BandoUnidad == objetivo.BandoUnidad)
            {
                Debug.Log($"[CombatSystem] No puedes atacar a unidades de tu propio bando.");
                return false;
            }

            // ── Validación 5: ¿Está dentro del rango de ataque? ───────────────
            int distancia = CalcularDistanciaManhattan(atacante.Coordenada, objetivo.Coordenada);
            int rangoMin = atacante.RangoAtaqueMinimo;
            int rangoMax = atacante.RangoAtaqueMaximo;

            if (distancia < rangoMin || distancia > rangoMax)
            {
                Debug.Log($"[CombatSystem] '{atacante.NombreUnidad}' → '{objetivo.NombreUnidad}': " +
                          $"Distancia {distancia}, rango permitido [{rangoMin}-{rangoMax}].");
                return false;
            }

            // ── Todo OK: calcular y aplicar daño ──────────────────────────────
            int danio = CalcularDanio(atacante, objetivo);
            AplicarDanio(atacante, objetivo, danio);

            return true;
        }

        /// <summary>
        /// Calcula el daño que el atacante inflige al objetivo.
        /// Fórmula básica: max(DanioMinimo, Ataque - Defensa).
        /// </summary>
        public int CalcularDanio(UnitController atacante, UnitController objetivo)
        {
            int defTerreno = 0;
            if (GridManager.Instancia != null && objetivo != null)
            {
                var terreno = GridManager.Instancia.ObtenerTileData(objetivo.Coordenada);
                if (terreno != null)
                {
                    defTerreno = terreno.BonusDefensa;
                }
            }

            int defensaTotal = objetivo.Defensa + defTerreno;
            int danioBase = atacante.Ataque - defensaTotal;
            int danioFinal = Mathf.Max(DanioMinimo, danioBase);

            if (defTerreno != 0)
            {
                Debug.Log($"[CombatSystem] Defensor '{objetivo.NombreUnidad}' en {objetivo.Coordenada} tiene bono de cobertura +{defTerreno} DEF. Defensa total: {defensaTotal}");
            }

            Debug.Log($"[CombatSystem] Cálculo: {atacante.Ataque} ATK - {defensaTotal} DEF (Base {objetivo.Defensa} + Terreno {defTerreno}) = " +
                      $"{danioBase} → final: {danioFinal}");

            return danioFinal;
        }

        /// <summary>
        /// Previsuailza el daño estimado sin aplicarlo (para mostrar en UI).
        /// </summary>
        public int PrevisualizarDanio(UnitController atacante, UnitController objetivo)
            => CalcularDanio(atacante, objetivo);

        /// <summary>
        /// Verifica si el atacante puede alcanzar al objetivo desde su posición.
        /// Útil para resaltar enemigos atacables en el ActionMenu.
        /// </summary>
        public bool EstaEnRangoDeAtaque(UnitController atacante, UnitController objetivo)
        {
            int distancia = CalcularDistanciaManhattan(atacante.Coordenada, objetivo.Coordenada);
            return distancia >= atacante.RangoAtaqueMinimo &&
                   distancia <= atacante.RangoAtaqueMaximo;
        }

        // ── Lógica interna ─────────────────────────────────────────────────────

        /// <summary>
        /// Aplica el daño al objetivo, dispara eventos y gestiona la muerte.
        /// </summary>
        private void AplicarDanio(UnitController atacante, UnitController objetivo, int danio)
        {
            // Aplicar daño (RecibirDanio ya clampea a 0).
            objetivo.RecibirDanio(danio);

            // Construir resultado para el evento.
            var resultado = new ResultadoCombate
            {
                Atacante           = atacante,
                Objetivo           = objetivo,
                DanioAplicado      = danio,
                ObjetivoEliminado  = objetivo.VidaActual <= 0,
                VidaRestanteObjetivo = objetivo.VidaActual
            };

            Debug.Log($"[CombatSystem] '{atacante.NombreUnidad}' → '{objetivo.NombreUnidad}': " +
                      $"-{danio} HP (queda {resultado.VidaRestanteObjetivo})");

            // Disparar evento de ataque (la HealthBar y la UI se actualizan aquí).
            OnAtaqueRealizado?.Invoke(resultado);

            // Marcar al atacante como "ya actuó este turno".
            atacante.MarcarComoUsadaPublico();

            // Si el objetivo fue eliminado, desregistrarlo del TurnManager.
            if (resultado.ObjetivoEliminado)
            {
                OnUnidadEliminada?.Invoke(objetivo);
                TurnManager.Instancia?.DesregistrarUnidad(objetivo);
            }

            // Iniciar animación visual de ataque (shake de la cámara, flash, etc.).
            StartCoroutine(AnimacionAtaque(atacante, objetivo));
        }

        /// <summary>
        /// Animación simple de ataque: el atacante se sacude brevemente hacia
        /// el objetivo y regresa. Efecto visual mínimo para Graybox.
        /// </summary>
        private System.Collections.IEnumerator AnimacionAtaque(
            UnitController atacante, UnitController objetivo)
        {
            if (atacante == null) yield break;

            Vector3 posOriginal = atacante.transform.position;
            Vector3 posObjetivo = objetivo != null
                ? objetivo.transform.position
                : posOriginal;

            // Lunge: moverse un 30% hacia el objetivo.
            Vector3 posLunge = Vector3.Lerp(posOriginal, posObjetivo, 0.3f);

            float tiempoMitad = DuracionAnimacionAtaque * 0.5f;
            float timer = 0f;

            // Ir hacia el objetivo.
            while (timer < tiempoMitad)
            {
                timer += Time.deltaTime;
                float t = timer / tiempoMitad;
                atacante.transform.position = Vector3.Lerp(posOriginal, posLunge, t);
                yield return null;
            }

            // Regresar a la posición original.
            timer = 0f;
            while (timer < tiempoMitad)
            {
                timer += Time.deltaTime;
                float t = timer / tiempoMitad;
                atacante.transform.position = Vector3.Lerp(posLunge, posOriginal, t);
                yield return null;
            }

            // Snap a la posición exacta.
            atacante.transform.position = posOriginal;
        }

        // ── Utilidades ─────────────────────────────────────────────────────────
        /// <summary>
        /// Distancia Manhattan = |Δcol| + |Δfila|.
        /// </summary>
        private int CalcularDistanciaManhattan(Vector2Int a, Vector2Int b)
            => Mathf.Abs(b.x - a.x) + Mathf.Abs(b.y - a.y);
    }
}
