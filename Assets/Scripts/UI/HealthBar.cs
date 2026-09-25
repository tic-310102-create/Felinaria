// ============================================================
//  HealthBar.cs
//  Felinaria: El último presagio
//  Fase 6 – Combate, Magia y Stats
//
//  RESPONSABILIDAD:
//    - Muestra una barra de vida (HP) y barra de maná (MP) flotante sobre cada unidad.
//    - Se actualiza automáticamente con eventos de daño, curación y magia.
//    - Usa un Canvas en modo "World Space" para flotar sobre la unidad.
//    - Incluye animación suave de las barras y efecto ghost.
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Felinaria.Units;
using Felinaria.Combat;

namespace Felinaria.UI
{
    /// <summary>
    /// Barra de vida y maná flotante que se adjunta a cada unidad del tablero.
    /// Se crea automáticamente desde código en WorldSpace.
    /// </summary>
    [RequireComponent(typeof(UnitController))]
    public class HealthBar : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Posicionamiento")]
        [Tooltip("Desplazamiento vertical de la barra respecto al centro de la unidad.")]
        public float OffsetY = 0.68f;

        [Header("Dimensiones de la Barra")]
        [Tooltip("Ancho total de las barras.")]
        public float AnchoBarra = 0.85f;

        [Tooltip("Alto total del contenedor de barras.")]
        public float AltoBarra = 0.14f;

        [Header("Colores")]
        public Color ColorVidaAlta = new Color(0.2f, 0.85f, 0.2f, 1f);  // Verde
        public Color ColorVidaMedia = new Color(1f, 0.85f, 0f, 1f);     // Amarillo
        public Color ColorVidaBaja = new Color(0.9f, 0.15f, 0.15f, 1f); // Rojo
        public Color ColorFondo = new Color(0.12f, 0.12f, 0.15f, 0.9f);
        public Color ColorDanioGhost = new Color(1f, 0.3f, 0.3f, 0.7f);
        public Color ColorMana = new Color(0.2f, 0.65f, 1f, 1f);        // Azul maná

        [Header("Animación")]
        [Range(0.5f, 5f)]
        public float VelocidadAnimacion = 2.5f;

        [Range(0f, 1f)]
        public float RetardoGhost = 0.35f;

        // ── Referencias internas ───────────────────────────────────────────────
        private UnitController _unidad;
        private Canvas _canvas;
        private Image _imagenFondoHP;
        private Image _imagenGhostHP;
        private Image _imagenRellenoHP;

        private Image _imagenFondoMana;
        private Image _imagenRellenoMana;

        // Valores de seguimiento para la animación.
        private float _vidaObjetivoNormalizado = 1f;
        private float _manaObjetivoNormalizado = 1f;
        private bool  _ghostEsperando;

        // ── Unity Lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            _unidad = GetComponent<UnitController>();
            if (_canvas == null)
            {
                CrearBarraVisual();
            }
        }

        private void Start()
        {
            if (_canvas == null)
            {
                CrearBarraVisual();
            }
            ActualizarBarra();

            if (CombatSystem.Instancia != null)
            {
                CombatSystem.Instancia.OnAtaqueRealizado += OnAtaqueRecibido;
            }

            if (SkillSystem.Instancia != null)
            {
                SkillSystem.Instancia.OnSkillEjecutada += OnSkillEjecutada;
            }
        }

        private void OnDestroy()
        {
            if (CombatSystem.InstanciaExiste)
            {
                CombatSystem.Instancia.OnAtaqueRealizado -= OnAtaqueRecibido;
            }

            if (SkillSystem.InstanciaExiste)
            {
                SkillSystem.Instancia.OnSkillEjecutada -= OnSkillEjecutada;
            }
        }

        private void LateUpdate()
        {
            if (_canvas != null)
            {
                _canvas.transform.position = transform.position + Vector3.up * OffsetY;
            }

            // Animar la barra de HP principal suavemente
            if (_imagenRellenoHP != null)
            {
                float valorActual = _imagenRellenoHP.fillAmount;
                if (!Mathf.Approximately(valorActual, _vidaObjetivoNormalizado))
                {
                    _imagenRellenoHP.fillAmount = Mathf.MoveTowards(
                        valorActual,
                        _vidaObjetivoNormalizado,
                        VelocidadAnimacion * Time.deltaTime
                    );
                    _imagenRellenoHP.color = ObtenerColorVida(_imagenRellenoHP.fillAmount);
                }
            }

            // Animar la barra ghost de HP
            if (_imagenGhostHP != null && !_ghostEsperando)
            {
                float valorGhostActual = _imagenGhostHP.fillAmount;
                if (!Mathf.Approximately(valorGhostActual, _vidaObjetivoNormalizado))
                {
                    _imagenGhostHP.fillAmount = Mathf.MoveTowards(
                        valorGhostActual,
                        _vidaObjetivoNormalizado,
                        VelocidadAnimacion * 0.6f * Time.deltaTime
                    );
                }
            }

            // Animar la barra de Maná suavemente
            if (_imagenRellenoMana != null)
            {
                float valorActualMana = _imagenRellenoMana.fillAmount;
                if (!Mathf.Approximately(valorActualMana, _manaObjetivoNormalizado))
                {
                    _imagenRellenoMana.fillAmount = Mathf.MoveTowards(
                        valorActualMana,
                        _manaObjetivoNormalizado,
                        VelocidadAnimacion * 1.5f * Time.deltaTime
                    );
                }
            }
        }

        // ── Creación visual ────────────────────────────────────────────────────
        private void CrearBarraVisual()
        {
            if (_canvas != null) return;

            if (_unidad == null)
            {
                _unidad = GetComponent<UnitController>();
            }

            if (_unidad != null)
            {
                float vm = _unidad.VidaMaxima > 0 ? _unidad.VidaMaxima : 1;
                _vidaObjetivoNormalizado = Mathf.Clamp01((float)_unidad.VidaActual / vm);

                float mm = _unidad.ManaMaximo > 0 ? _unidad.ManaMaximo : 1;
                _manaObjetivoNormalizado = Mathf.Clamp01((float)_unidad.ManaActual / mm);
            }

            var canvasObj = new GameObject("HealthBar_Canvas");
            canvasObj.transform.SetParent(transform);
            canvasObj.transform.localPosition = Vector3.up * OffsetY;

            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 10;

            var rectCanvas = canvasObj.GetComponent<RectTransform>();
            rectCanvas.sizeDelta = new Vector2(AnchoBarra, AltoBarra);
            rectCanvas.localScale = Vector3.one;

            // ── 1. Contenedor Barra HP (parte superior 68%) ───────────────────
            var hpContainer = new GameObject("HP_Container");
            hpContainer.transform.SetParent(canvasObj.transform, false);
            var rectHPContainer = hpContainer.AddComponent<RectTransform>();
            rectHPContainer.anchorMin = new Vector2(0f, 0.38f);
            rectHPContainer.anchorMax = new Vector2(1f, 1f);
            rectHPContainer.offsetMin = Vector2.zero;
            rectHPContainer.offsetMax = Vector2.zero;

            _imagenFondoHP = CrearImagen("FondoHP", hpContainer.transform, ColorFondo);
            ConfigurarRectTransformEstirado(_imagenFondoHP.GetComponent<RectTransform>());

            _imagenGhostHP = CrearImagen("GhostHP", hpContainer.transform, ColorDanioGhost);
            ConfigurarRectTransformEstirado(_imagenGhostHP.GetComponent<RectTransform>());
            _imagenGhostHP.type = Image.Type.Filled;
            _imagenGhostHP.fillMethod = Image.FillMethod.Horizontal;
            _imagenGhostHP.fillAmount = _vidaObjetivoNormalizado;

            _imagenRellenoHP = CrearImagen("RellenoHP", hpContainer.transform, ColorVidaAlta);
            ConfigurarRectTransformEstirado(_imagenRellenoHP.GetComponent<RectTransform>());
            _imagenRellenoHP.type = Image.Type.Filled;
            _imagenRellenoHP.fillMethod = Image.FillMethod.Horizontal;
            _imagenRellenoHP.fillAmount = _vidaObjetivoNormalizado;
            _imagenRellenoHP.color = ObtenerColorVida(_vidaObjetivoNormalizado);

            // ── 2. Contenedor Barra Maná (parte inferior 28%) ──────────────────
            var manaContainer = new GameObject("Mana_Container");
            manaContainer.transform.SetParent(canvasObj.transform, false);
            var rectManaContainer = manaContainer.AddComponent<RectTransform>();
            rectManaContainer.anchorMin = new Vector2(0f, 0f);
            rectManaContainer.anchorMax = new Vector2(1f, 0.28f);
            rectManaContainer.offsetMin = Vector2.zero;
            rectManaContainer.offsetMax = Vector2.zero;

            _imagenFondoMana = CrearImagen("FondoMana", manaContainer.transform, ColorFondo);
            ConfigurarRectTransformEstirado(_imagenFondoMana.GetComponent<RectTransform>());

            _imagenRellenoMana = CrearImagen("RellenoMana", manaContainer.transform, ColorMana);
            ConfigurarRectTransformEstirado(_imagenRellenoMana.GetComponent<RectTransform>());
            _imagenRellenoMana.type = Image.Type.Filled;
            _imagenRellenoMana.fillMethod = Image.FillMethod.Horizontal;
            _imagenRellenoMana.fillAmount = _manaObjetivoNormalizado;
        }

        private Image CrearImagen(string nombre, Transform padre, Color color)
        {
            var obj = new GameObject(nombre);
            obj.transform.SetParent(padre, false);
            var img = obj.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private void ConfigurarRectTransformEstirado(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ── Callbacks de combate y magia ───────────────────────────────────────
        private void OnAtaqueRecibido(ResultadoCombate resultado)
        {
            if (resultado.Objetivo != _unidad) return;
            ActualizarBarra();
        }

        private void OnSkillEjecutada(ResultadoHabilidad resultado)
        {
            if (resultado.Lanzador == _unidad || (resultado.ObjetivosAfectados != null && resultado.ObjetivosAfectados.Contains(_unidad)))
            {
                ActualizarBarra();
            }
        }

        // ── API pública ────────────────────────────────────────────────────────
        public void ActualizarBarra()
        {
            if (_unidad == null) return;
            if (_canvas == null)
            {
                CrearBarraVisual();
            }

            float vidaMax = _unidad.VidaMaxima > 0 ? _unidad.VidaMaxima : 1;
            _vidaObjetivoNormalizado = Mathf.Clamp01((float)_unidad.VidaActual / vidaMax);

            float manaMax = _unidad.ManaMaximo > 0 ? _unidad.ManaMaximo : 1;
            _manaObjetivoNormalizado = Mathf.Clamp01((float)_unidad.ManaActual / manaMax);

            _ghostEsperando = true;
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(RetardarGhost());
            }
        }

        /// <summary>
        /// Actualiza de forma inmediata y directa la barra de vida con los valores dados.
        /// </summary>
        public void ActualizarVida(int vidaActual, int vidaMaximo)
        {
            if (_canvas == null)
            {
                CrearBarraVisual();
            }

            if (vidaMaximo <= 0) vidaMaximo = 1;
            _vidaObjetivoNormalizado = Mathf.Clamp01((float)vidaActual / vidaMaximo);
            if (_imagenRellenoHP != null)
            {
                _imagenRellenoHP.fillAmount = _vidaObjetivoNormalizado;
                _imagenRellenoHP.color = ObtenerColorVida(_vidaObjetivoNormalizado);
            }
            if (_imagenGhostHP != null)
            {
                _imagenGhostHP.fillAmount = _vidaObjetivoNormalizado;
            }
        }

        /// <summary>
        /// Actualiza de forma inmediata y directa la barra de maná con los valores dados.
        /// </summary>
        public void ActualizarMana(int manaActual, int manaMaximo)
        {
            if (_canvas == null)
            {
                CrearBarraVisual();
            }

            if (manaMaximo <= 0) manaMaximo = 1;
            _manaObjetivoNormalizado = Mathf.Clamp01((float)manaActual / manaMaximo);
            if (_imagenRellenoMana != null)
            {
                _imagenRellenoMana.fillAmount = _manaObjetivoNormalizado;
            }
        }

        private IEnumerator RetardarGhost()
        {
            yield return new WaitForSeconds(RetardoGhost);
            _ghostEsperando = false;
        }

        private Color ObtenerColorVida(float porcentaje)
        {
            if (porcentaje > 0.5f)
            {
                float t = (porcentaje - 0.5f) / 0.5f;
                return Color.Lerp(ColorVidaMedia, ColorVidaAlta, t);
            }
            else
            {
                float t = porcentaje / 0.5f;
                return Color.Lerp(ColorVidaBaja, ColorVidaMedia, t);
            }
        }
    }
}

