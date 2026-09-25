// ============================================================
//  HealthBar.cs
//  Felinaria: El último presagio
//  Fase 6 – Combate, Magia y Stats
//
//  RESPONSABILIDAD:
//    - Muestra barra de vida (HP) y barra de maná (MP) flotante sobre cada unidad en WorldSpace.
//    - Muestra textos numéricos TextMeshPro: "HP: {actual}/{maximo}" y "MP: {actual}/{maximo}".
//    - Actualización inmediata con daño, curación, magia y restauración de partida.
//    - Animación suave de relleno y efecto ghost de daño.
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Felinaria.Units;
using Felinaria.Combat;

namespace Felinaria.UI
{
    /// <summary>
    /// Barra de vida y maná flotante que se adjunta a cada unidad del tablero en World Space.
    /// Incluye etiquetas numéricas legibles TextMeshPro para HP y MP.
    /// </summary>
    [RequireComponent(typeof(UnitController))]
    public class HealthBar : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Posicionamiento")]
        [Tooltip("Desplazamiento vertical de la barra respecto al centro de la unidad.")]
        public float OffsetY = 0.72f;

        [Header("Dimensiones en Espacio de Mundo")]
        [Tooltip("Ancho en píxeles del canvas.")]
        public float AnchoCanvas = 100f;

        [Tooltip("Alto en píxeles del canvas.")]
        public float AltoCanvas = 32f;

        [Tooltip("Escala del canvas en el mundo.")]
        public float EscalaMundo = 0.01f;

        [Header("Colores")]
        public Color ColorVidaAlta = new Color(0.2f, 0.85f, 0.2f, 1f);  // Verde
        public Color ColorVidaMedia = new Color(1f, 0.85f, 0f, 1f);     // Amarillo
        public Color ColorVidaBaja = new Color(0.9f, 0.15f, 0.15f, 1f); // Rojo
        public Color ColorFondo = new Color(0.1f, 0.1f, 0.14f, 0.92f);
        public Color ColorDanioGhost = new Color(1f, 0.3f, 0.3f, 0.75f);
        public Color ColorMana = new Color(0.18f, 0.65f, 1f, 1f);       // Azul maná

        [Header("Animación")]
        [Range(0.5f, 5f)]
        public float VelocidadAnimacion = 3f;

        [Range(0f, 1f)]
        public float RetardoGhost = 0.3f;

        // ── Referencias internas ───────────────────────────────────────────────
        private UnitController _unidad;
        private Canvas _canvas;
        private Image _imagenFondoHP;
        private Image _imagenGhostHP;
        private Image _imagenRellenoHP;
        private TextMeshProUGUI _textoHP;

        private Image _imagenFondoMana;
        private Image _imagenRellenoMana;
        private TextMeshProUGUI _textoMana;

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
            rectCanvas.sizeDelta = new Vector2(AnchoCanvas, AltoCanvas);
            rectCanvas.localScale = Vector3.one * EscalaMundo;

            // ── 1. Contenedor Barra HP (parte superior) ───────────────────────
            var hpContainer = new GameObject("HP_Container");
            hpContainer.transform.SetParent(canvasObj.transform, false);
            var rectHPContainer = hpContainer.AddComponent<RectTransform>();
            rectHPContainer.anchorMin = new Vector2(0f, 0.42f);
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

            // Texto numérico HP con TextMeshPro
            _textoHP = CrearTextoTMP("TextoHP", hpContainer.transform, 12f, Color.white);

            // ── 2. Contenedor Barra Maná (parte inferior) ──────────────────
            var manaContainer = new GameObject("Mana_Container");
            manaContainer.transform.SetParent(canvasObj.transform, false);
            var rectManaContainer = manaContainer.AddComponent<RectTransform>();
            rectManaContainer.anchorMin = new Vector2(0f, 0f);
            rectManaContainer.anchorMax = new Vector2(1f, 0.36f);
            rectManaContainer.offsetMin = Vector2.zero;
            rectManaContainer.offsetMax = Vector2.zero;

            _imagenFondoMana = CrearImagen("FondoMana", manaContainer.transform, ColorFondo);
            ConfigurarRectTransformEstirado(_imagenFondoMana.GetComponent<RectTransform>());

            _imagenRellenoMana = CrearImagen("RellenoMana", manaContainer.transform, ColorMana);
            ConfigurarRectTransformEstirado(_imagenRellenoMana.GetComponent<RectTransform>());
            _imagenRellenoMana.type = Image.Type.Filled;
            _imagenRellenoMana.fillMethod = Image.FillMethod.Horizontal;
            _imagenRellenoMana.fillAmount = _manaObjetivoNormalizado;

            // Texto numérico MP con TextMeshPro
            _textoMana = CrearTextoTMP("TextoMana", manaContainer.transform, 8.5f, new Color(0.9f, 0.95f, 1f));

            // Actualizar etiquetas numéricas iniciales
            if (_unidad != null)
            {
                ActualizarTextos(_unidad.VidaActual, _unidad.VidaMaxima, _unidad.ManaActual, _unidad.ManaMaximo);
            }
        }

        private Image CrearImagen(string nombre, Transform padre, Color color)
        {
            var obj = new GameObject(nombre);
            obj.transform.SetParent(padre, false);
            var img = obj.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private TextMeshProUGUI CrearTextoTMP(string nombre, Transform padre, float tamanioFuente, Color color)
        {
            var obj = new GameObject(nombre);
            obj.transform.SetParent(padre, false);

            var rect = obj.AddComponent<RectTransform>();
            ConfigurarRectTransformEstirado(rect);

            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = "";
            tmp.fontSize = tamanioFuente;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.enableAutoSizing = false;
            tmp.raycastTarget = false;
            tmp.extraPadding = true;

            // Sombra/Outline nítido para máximo contraste
            tmp.outlineWidth = 0.22f;
            tmp.outlineColor = new Color32(0, 0, 0, 240);

            return tmp;
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
        /// <summary>
        /// Actualiza los rellenos visuales y los textos numéricos HP y MP de la barra.
        /// </summary>
        public void ActualizarBarra()
        {
            if (_unidad == null) return;
            if (_canvas == null)
            {
                CrearBarraVisual();
            }

            int vidaAct = _unidad.VidaActual;
            int vidaMax = _unidad.VidaMaxima > 0 ? _unidad.VidaMaxima : 1;
            _vidaObjetivoNormalizado = Mathf.Clamp01((float)vidaAct / vidaMax);

            int manaAct = _unidad.ManaActual;
            int manaMax = _unidad.ManaMaximo > 0 ? _unidad.ManaMaximo : 1;
            _manaObjetivoNormalizado = Mathf.Clamp01((float)manaAct / manaMax);

            ActualizarTextos(vidaAct, vidaMax, manaAct, manaMax);

            _ghostEsperando = true;
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(RetardarGhost());
            }
        }

        /// <summary>
        /// Actualiza de forma inmediata y directa la barra de vida y su texto con los valores dados.
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

            int manaAct = (_unidad != null) ? _unidad.ManaActual : 0;
            int manaMax = (_unidad != null && _unidad.ManaMaximo > 0) ? _unidad.ManaMaximo : 1;
            ActualizarTextos(vidaActual, vidaMaximo, manaAct, manaMax);
        }

        /// <summary>
        /// Actualiza de forma inmediata y directa la barra de maná y su texto con los valores dados.
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

            int vidaAct = (_unidad != null) ? _unidad.VidaActual : 0;
            int vidaMax = (_unidad != null && _unidad.VidaMaxima > 0) ? _unidad.VidaMaxima : 1;
            ActualizarTextos(vidaAct, vidaMax, manaActual, manaMaximo);
        }

        private void ActualizarTextos(int vidaActual, int vidaMaximo, int manaActual, int manaMaximo)
        {
            if (_textoHP != null)
            {
                _textoHP.text = $"HP: {vidaActual}/{vidaMaximo}";
            }

            if (_textoMana != null)
            {
                _textoMana.text = $"MP: {manaActual}/{manaMaximo}";
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
