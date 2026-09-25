// ============================================================
//  SkillSystem.cs
//  Felinaria: El último presagio
//  Fase 6 – Sistema de Habilidades Especiales y Magia
//
//  RESPONSABILIDAD:
//    - Gestiona la validación, cálculo y ejecución de habilidades activas.
//    - Soporta ataques mágicos a distancia, curación y ataques en área (AoE).
//    - Aplica consumo de maná, activa cooldowns y genera efectos visuales 2D.
//    - Auto-configurable en runtime con habilidades predeterminadas.
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Felinaria.Data;
using Felinaria.Units;
using Felinaria.Grid;
using Felinaria.Managers;
using Felinaria.Audio;

namespace Felinaria.Combat
{
    /// <summary>
    /// Resultado de la ejecución de una habilidad para eventos y UI.
    /// </summary>
    public struct ResultadoHabilidad
    {
        public UnitController Lanzador;
        public SkillData Habilidad;
        public Vector2Int CeldaCentro;
        public List<UnitController> ObjetivosAfectados;
        public List<int> ValoresAplicados;
        public List<Vector2Int> CeldasAfectadas;
    }

    /// <summary>
    /// Singleton que procesa el lanzamiento de habilidades y magia.
    /// </summary>
    public class SkillSystem : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        private static SkillSystem _instancia;
        private static bool _aplicacionCerrando = false;

        /// <summary>Indica si existe una instancia activa sin forzar su creación.</summary>
        public static bool InstanciaExiste => _instancia != null && !_aplicacionCerrando;

        public static SkillSystem Instancia
        {
            get
            {
                if (_aplicacionCerrando) return null;
                if (_instancia == null)
                {
                    _instancia = FindFirstObjectByType<SkillSystem>();
                    if (_instancia == null && Application.isPlaying)
                    {
                        var go = new GameObject("SkillSystem_Auto");
                        _instancia = go.AddComponent<SkillSystem>();
                    }
                }
                return _instancia;
            }
            private set => _instancia = value;
        }

        // ── Eventos C# ─────────────────────────────────────────────────────────
        public event Action<ResultadoHabilidad> OnSkillEjecutada;

        // ── Habilidades por defecto de respaldo ────────────────────────────────
        private static SkillData _habilidadRayoArcano;
        private static SkillData _habilidadCuracionMenor;
        private static SkillData _habilidadLlamaAoE;

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
            InicializarHabilidadesDefault();
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

        private static void InicializarHabilidadesDefault()
        {
            if (_habilidadRayoArcano == null)
            {
                _habilidadRayoArcano = SkillData.CrearInstanciaRuntime(
                    "rayo_arcano",
                    "⚡ Rayo Arcano",
                    "Dispara un haz de energía pura a un enemigo a distancia.",
                    TipoHabilidad.AtaqueMagico,
                    TipoObjetivo.Enemigo,
                    FormaArea.Puntual,
                    mana: 8,
                    cd: 0,
                    rMin: 1,
                    rMax: 3,
                    radio: 0,
                    potencia: 12,
                    color: new Color(0.2f, 0.75f, 1f, 1f)
                );
            }

            if (_habilidadCuracionMenor == null)
            {
                _habilidadCuracionMenor = SkillData.CrearInstanciaRuntime(
                    "curacion_menor",
                    "💚 Curación Menor",
                    "Restaura la salud de un aliado o del propio lanzador.",
                    TipoHabilidad.Curacion,
                    TipoObjetivo.Aliado,
                    FormaArea.Puntual,
                    mana: 6,
                    cd: 1,
                    rMin: 0,
                    rMax: 2,
                    radio: 0,
                    potencia: 14,
                    color: new Color(0.25f, 0.9f, 0.4f, 1f)
                );
            }

            if (_habilidadLlamaAoE == null)
            {
                _habilidadLlamaAoE = SkillData.CrearInstanciaRuntime(
                    "llama_felina_aoe",
                    "💥 Llama Felina (AoE)",
                    "Desata una explosión ígnea en forma de cruz que daña múltiples casillas.",
                    TipoHabilidad.AtaqueArea,
                    TipoObjetivo.CualquierCelda,
                    FormaArea.Cruz,
                    mana: 12,
                    cd: 2,
                    rMin: 1,
                    rMax: 3,
                    radio: 1,
                    potencia: 10,
                    color: new Color(1f, 0.45f, 0.15f, 1f)
                );
            }
        }

        public static List<SkillData> ObtenerHabilidadesPredeterminadas()
        {
            InicializarHabilidadesDefault();
            return new List<SkillData> { _habilidadRayoArcano, _habilidadCuracionMenor, _habilidadLlamaAoE };
        }

        // ── API pública de Ejecución ───────────────────────────────────────────

        /// <summary>
        /// Valida y ejecuta una habilidad en la celda objetivo dada.
        /// </summary>
        public bool EjecutarHabilidad(UnitController lanzador, SkillData habilidad, Vector2Int celdaObjetivo)
        {
            if (lanzador == null || habilidad == null) return false;

            // 1. Validar turno
            if (TurnManager.Instancia != null && !TurnManager.Instancia.EsTurnoDeUnidad(lanzador))
            {
                Debug.Log($"[SkillSystem] No es el turno de '{lanzador.NombreUnidad}'.");
                return false;
            }

            // 2. Validar si ya actuó
            if (lanzador.YaActuoEsteTurno)
            {
                Debug.Log($"[SkillSystem] '{lanzador.NombreUnidad}' ya actuó este turno.");
                return false;
            }

            // 3. Validar costo de maná y cooldown
            if (!lanzador.PuedeUsarSkill(habilidad))
            {
                Debug.Log($"[SkillSystem] '{lanzador.NombreUnidad}' no puede usar '{habilidad.NombreHabilidad}' (Sin maná o en Cooldown).");
                return false;
            }

            // 4. Validar rango
            if (!EsCeldaEnRango(lanzador, habilidad, celdaObjetivo))
            {
                Debug.Log($"[SkillSystem] Celda ({celdaObjetivo.x},{celdaObjetivo.y}) fuera de rango para '{habilidad.NombreHabilidad}'.");
                return false;
            }

            // 5. Validar objetivo según tipo
            var unidadEnCelda = ObtenerUnidadEnCelda(celdaObjetivo);
            if (!EsObjetivoAceptable(lanzador, habilidad, celdaObjetivo, unidadEnCelda))
            {
                Debug.Log($"[SkillSystem] Objetivo no válido para '{habilidad.NombreHabilidad}'.");
                return false;
            }

            // Iniciar corrutina de ejecución visual y aplicación de efectos
            StartCoroutine(RutinaEjecutarHabilidad(lanzador, habilidad, celdaObjetivo));
            return true;
        }

        private IEnumerator RutinaEjecutarHabilidad(UnitController lanzador, SkillData habilidad, Vector2Int celdaObjetivo)
        {
            // Consumir maná y activar cooldown
            lanzador.ConsumirMana(habilidad.CostoMana);
            lanzador.IniciarCooldown(habilidad);

            // Calcular celdas del área de impacto
            List<Vector2Int> celdasAfectadas = ObtenerCeldasAfectadasArea(celdaObjetivo, habilidad);

            // Reproducir SFX si existe
            AudioManager.Instancia?.SFX_Atacar();

            // Efecto visual 2D sobre las celdas afectadas
            MostrarEfectoVisual2D(celdasAfectadas, habilidad.ColorEfecto);

            yield return new WaitForSeconds(0.3f);

            var objetivosAfectados = new List<UnitController>();
            var valoresAplicados = new List<int>();

            // Aplicar efectos en cada celda del área
            foreach (var coord in celdasAfectadas)
            {
                var unidad = ObtenerUnidadEnCelda(coord);
                if (unidad == null || unidad.VidaActual <= 0) continue;

                if (habilidad.Tipo == TipoHabilidad.Curacion)
                {
                    // Curación solo a aliados o al propio lanzador
                    if (unidad.BandoUnidad == lanzador.BandoUnidad)
                    {
                        int cura = habilidad.Potencia;
                        unidad.Curar(cura);
                        objetivosAfectados.Add(unidad);
                        valoresAplicados.Add(cura);
                        Debug.Log($"[SkillSystem] 💚 '{habilidad.NombreHabilidad}' curó {cura} HP a '{unidad.NombreUnidad}'.");
                    }
                }
                else // Ataque Mágico o Ataque en Área
                {
                    // Daño a enemigos (o todas las unidades contrarias en AoE)
                    if (unidad.BandoUnidad != lanzador.BandoUnidad)
                    {
                        int defMagica = (unidad.FichaStats != null) ? unidad.FichaStats.DefensaMagica : 0;
                        int atkMagico = (lanzador.FichaStats != null) ? lanzador.FichaStats.AtaqueMagico : 0;
                        int danioFinal = Mathf.Max(1, (habilidad.Potencia + atkMagico) - defMagica);

                        unidad.RecibirDanio(danioFinal);
                        objetivosAfectados.Add(unidad);
                        valoresAplicados.Add(danioFinal);
                        Debug.Log($"[SkillSystem] ⚡ '{habilidad.NombreHabilidad}' causó {danioFinal} de daño mágico a '{unidad.NombreUnidad}'.");
                    }
                }
            }

            // Marcar al lanzador como usado
            lanzador.MarcarComoUsadaPublico();

            var resultado = new ResultadoHabilidad
            {
                Lanzador = lanzador,
                Habilidad = habilidad,
                CeldaCentro = celdaObjetivo,
                ObjetivosAfectados = objetivosAfectados,
                ValoresAplicados = valoresAplicados,
                CeldasAfectadas = celdasAfectadas
            };

            OnSkillEjecutada?.Invoke(resultado);
        }

        // ── Cálculos de Rango y Área ───────────────────────────────────────────

        public bool EsCeldaEnRango(UnitController lanzador, SkillData habilidad, Vector2Int celda)
        {
            if (GridManager.Instancia == null || !GridManager.Instancia.EsCoordenadaValida(celda))
                return false;

            int dist = Mathf.Abs(lanzador.Coordenada.x - celda.x) + Mathf.Abs(lanzador.Coordenada.y - celda.y);
            return dist >= habilidad.RangoMinimo && dist <= habilidad.RangoMaximo;
        }

        public List<Vector2Int> ObtenerCeldasEnRango(UnitController lanzador, SkillData habilidad)
        {
            var resultado = new List<Vector2Int>();
            if (lanzador == null || habilidad == null || GridManager.Instancia == null)
                return resultado;

            int rMax = habilidad.RangoMaximo;
            for (int dx = -rMax; dx <= rMax; dx++)
            {
                for (int dy = -rMax; dy <= rMax; dy++)
                {
                    int dist = Mathf.Abs(dx) + Mathf.Abs(dy);
                    if (dist >= habilidad.RangoMinimo && dist <= habilidad.RangoMaximo)
                    {
                        var coord = new Vector2Int(lanzador.Coordenada.x + dx, lanzador.Coordenada.y + dy);
                        if (GridManager.Instancia.EsCoordenadaValida(coord))
                        {
                            resultado.Add(coord);
                        }
                    }
                }
            }
            return resultado;
        }

        public List<Vector2Int> ObtenerCeldasAfectadasArea(Vector2Int centro, SkillData habilidad)
        {
            var lista = new List<Vector2Int>();
            if (GridManager.Instancia == null || !GridManager.Instancia.EsCoordenadaValida(centro))
                return lista;

            lista.Add(centro);

            if (habilidad.RadioArea <= 0 || habilidad.Forma == FormaArea.Puntual)
                return lista;

            if (habilidad.Forma == FormaArea.Cruz)
            {
                Vector2Int[] offsets = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                foreach (var offset in offsets)
                {
                    var c = centro + offset;
                    if (GridManager.Instancia.EsCoordenadaValida(c))
                        lista.Add(c);
                }
            }
            else if (habilidad.Forma == FormaArea.Diamante)
            {
                for (int dx = -habilidad.RadioArea; dx <= habilidad.RadioArea; dx++)
                {
                    for (int dy = -habilidad.RadioArea; dy <= habilidad.RadioArea; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        if (Mathf.Abs(dx) + Mathf.Abs(dy) <= habilidad.RadioArea)
                        {
                            var c = new Vector2Int(centro.x + dx, centro.y + dy);
                            if (GridManager.Instancia.EsCoordenadaValida(c))
                                lista.Add(c);
                        }
                    }
                }
            }

            return lista;
        }

        private bool EsObjetivoAceptable(UnitController lanzador, SkillData habilidad, Vector2Int celda, UnitController target)
        {
            switch (habilidad.Objetivo)
            {
                case TipoObjetivo.Enemigo:
                    return target != null && target.BandoUnidad != lanzador.BandoUnidad;

                case TipoObjetivo.Aliado:
                    return target != null && target.BandoUnidad == lanzador.BandoUnidad;

                case TipoObjetivo.UnoMismo:
                    return target == lanzador;

                case TipoObjetivo.CualquierCelda:
                    return true;

                default:
                    return true;
            }
        }

        private UnitController ObtenerUnidadEnCelda(Vector2Int coord)
        {
            var todas = FindObjectsByType<UnitController>(FindObjectsSortMode.None);
            foreach (var u in todas)
            {
                if (u != null && u.gameObject.activeInHierarchy && u.Coordenada == coord && u.VidaActual > 0)
                    return u;
            }
            return null;
        }

        private void MostrarEfectoVisual2D(List<Vector2Int> celdas, Color color)
        {
            if (GridManager.Instancia == null) return;

            foreach (var c in celdas)
            {
                Vector3 pos = GridManager.Instancia.CoordenadaAMundo(c);
                pos.z = -0.1f; // Al frente en 2D

                var fxObj = new GameObject("SpellFX_2D");
                fxObj.transform.position = pos;

                var sr = fxObj.AddComponent<SpriteRenderer>();
                sr.sprite = GridManager.ObtenerSpriteBlanco();
                sr.color = new Color(color.r, color.g, color.b, 0.85f);
                sr.sortingOrder = 15;
                fxObj.transform.localScale = new Vector3(0.9f, 0.9f, 1f);

                StartCoroutine(AnimarDesvanecimientoFX(fxObj));
            }
        }

        private IEnumerator AnimarDesvanecimientoFX(GameObject fxObj)
        {
            if (fxObj == null) yield break;
            var sr = fxObj.GetComponent<SpriteRenderer>();
            float duracion = 0.45f;
            float timer = 0f;
            Vector3 escalaInicial = fxObj.transform.localScale;

            while (timer < duracion && fxObj != null)
            {
                timer += Time.deltaTime;
                float t = timer / duracion;
                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = Mathf.Lerp(0.85f, 0f, t);
                    sr.color = c;
                }
                fxObj.transform.localScale = Vector3.Lerp(escalaInicial, escalaInicial * 1.35f, t);
                yield return null;
            }

            if (fxObj != null)
                Destroy(fxObj);
        }
    }
}
