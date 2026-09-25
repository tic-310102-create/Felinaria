// ============================================================
//  HealthBar.cs
//  Felinaria: El último presagio
//  Fase 2 – Combate y Stats
//
//  RESPONSABILIDAD:
//    - Muestra una barra de vida flotante sobre cada unidad.
//    - Se actualiza automáticamente al suscribirse al evento
//      OnAtaqueRealizado del CombatSystem.
//    - Usa un Canvas en modo "World Space" para flotar sobre la unidad.
//    - Incluye animación suave de la barra y flash de daño.
//
//  CÓMO FUNCIONA (para principiantes):
//    Este script se coloca en la MISMA unidad que tiene UnitController.
//    Crea automáticamente un mini-Canvas con dos barras (fondo + relleno)
//    que flotan sobre la cabeza del personaje.
//    Cuando la unidad recibe daño, la barra se reduce suavemente.
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Felinaria.Units;
using Felinaria.Combat;

namespace Felinaria.UI
{
    /// <summary>
    /// Barra de vida flotante que se adjunta a cada unidad del tablero.
    /// Se crea automáticamente desde código (no necesitas armar el Canvas manualmente).
    /// </summary>
    [RequireComponent(typeof(UnitController))]
    public class HealthBar : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Posicionamiento")]
        [Tooltip("Desplazamiento vertical de la barra respecto al centro de la unidad.")]
        public float OffsetY = 0.65f;

        [Header("Dimensiones de la Barra")]
        [Tooltip("Ancho total de la barra de vida.")]
        public float AnchoBarra = 0.8f;

        [Tooltip("Alto total de la barra de vida.")]
        public float AltoBarra = 0.1f;

        [Header("Colores")]
        [Tooltip("Color de la barra cuando la vida está llena.")]
        public Color ColorVidaAlta = new Color(0.2f, 0.85f, 0.2f, 1f);  // Verde

        [Tooltip("Color de la barra cuando la vida está a la mitad.")]
        public Color ColorVidaMedia = new Color(1f, 0.85f, 0f, 1f);     // Amarillo

        [Tooltip("Color de la barra cuando la vida está baja.")]
        public Color ColorVidaBaja = new Color(0.9f, 0.15f, 0.15f, 1f); // Rojo

        [Tooltip("Color del fondo de la barra.")]
        public Color ColorFondo = new Color(0.15f, 0.15f, 0.15f, 0.85f);

        [Tooltip("Color de la barra de daño retardado (efecto ghost).")]
        public Color ColorDanioGhost = new Color(1f, 0.3f, 0.3f, 0.7f);

        [Header("Animación")]
        [Tooltip("Velocidad de reducción suave de la barra (0-1 por segundo).")]
        [Range(0.5f, 5f)]
        public float VelocidadAnimacion = 2f;

        [Tooltip("Retardo antes de que la barra ghost comience a reducirse.")]
        [Range(0f, 1f)]
        public float RetardoGhost = 0.4f;

        // ── Referencias internas ───────────────────────────────────────────────
        private UnitController _unidad;
        private Canvas _canvas;
        private Image _imagenFondo;
        private Image _imagenGhost;    // Barra de "daño retardado" que sigue a la principal.
        private Image _imagenRelleno;  // Barra principal (vida actual).

        // Valores de seguimiento para la animación.
        private float _vidaObjetivoNormalizado;  // Hacia dónde va la barra principal.
        private bool  _ghostEsperando;           // True durante el retardo del ghost.

        // ── Unity Lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            _unidad = GetComponent<UnitController>();
        }

        private void Start()
        {
            CrearBarraVisual();

            // Inicializar la barra al 100%.
            _vidaObjetivoNormalizado = 1f;

            // Suscribirse al evento de combate para actualizarse automáticamente.
            if (CombatSystem.Instancia != null)
            {
                CombatSystem.Instancia.OnAtaqueRealizado += OnAtaqueRecibido;
            }
        }

        private void OnDestroy()
        {
            // Desuscribirse para evitar errores cuando la unidad muere.
            if (CombatSystem.Instancia != null)
            {
                CombatSystem.Instancia.OnAtaqueRealizado -= OnAtaqueRecibido;
            }
        }

        private void LateUpdate()
        {
            // Posicionar la barra sobre la unidad en cada frame.
            if (_canvas != null)
            {
                _canvas.transform.position = transform.position + Vector3.up * OffsetY;
            }

            // Animar la barra principal suavemente hacia el valor objetivo.
            if (_imagenRelleno != null)
            {
                float valorActual = _imagenRelleno.fillAmount;
                if (!Mathf.Approximately(valorActual, _vidaObjetivoNormalizado))
                {
                    _imagenRelleno.fillAmount = Mathf.MoveTowards(
                        valorActual,
                        _vidaObjetivoNormalizado,
                        VelocidadAnimacion * Time.deltaTime
                    );
                    // Actualizar color según porcentaje de vida.
                    _imagenRelleno.color = ObtenerColorVida(_imagenRelleno.fillAmount);
                }
            }

            // Animar la barra ghost (se reduce después del retardo).
            if (_imagenGhost != null && !_ghostEsperando)
            {
                float valorGhostActual = _imagenGhost.fillAmount;
                if (!Mathf.Approximately(valorGhostActual, _vidaObjetivoNormalizado))
                {
                    _imagenGhost.fillAmount = Mathf.MoveTowards(
                        valorGhostActual,
                        _vidaObjetivoNormalizado,
                        VelocidadAnimacion * 0.5f * Time.deltaTime
                    );
                }
            }
        }

        // ── Creación visual ────────────────────────────────────────────────────
        /// <summary>
        /// Construye toda la jerarquía visual de la barra de vida por código.
        /// No necesitas crear nada en el editor manualmente.
        /// </summary>
        private void CrearBarraVisual()
        {
            // ── 1. Canvas World Space ──────────────────────────────────────────
            var canvasObj = new GameObject("HealthBar_Canvas");
            canvasObj.transform.SetParent(transform);
            canvasObj.transform.localPosition = Vector3.up * OffsetY;

            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 10;  // Por encima de las celdas y unidades.

            // Configurar tamaño del Canvas.
            var rectCanvas = canvasObj.GetComponent<RectTransform>();
            rectCanvas.sizeDelta = new Vector2(AnchoBarra, AltoBarra);
            rectCanvas.localScale = Vector3.one;

            // ── 2. Imagen de Fondo ─────────────────────────────────────────────
            _imagenFondo = CrearImagen("Fondo", canvasObj.transform, ColorFondo);
            var rectFondo = _imagenFondo.GetComponent<RectTransform>();
            ConfigurarRectTransformEstirado(rectFondo);

            // ── 3. Imagen Ghost (daño retardado) ──────────────────────────────
            _imagenGhost = CrearImagen("Ghost", canvasObj.transform, ColorDanioGhost);
            var rectGhost = _imagenGhost.GetComponent<RectTransform>();
            ConfigurarRectTransformEstirado(rectGhost);
            _imagenGhost.type = Image.Type.Filled;
            _imagenGhost.fillMethod = Image.FillMethod.Horizontal;
            _imagenGhost.fillAmount = 1f;

            // ── 4. Imagen de Relleno (vida actual) ─────────────────────────────
            _imagenRelleno = CrearImagen("Relleno", canvasObj.transform, ColorVidaAlta);
            var rectRelleno = _imagenRelleno.GetComponent<RectTransform>();
            ConfigurarRectTransformEstirado(rectRelleno);
            _imagenRelleno.type = Image.Type.Filled;
            _imagenRelleno.fillMethod = Image.FillMethod.Horizontal;
            _imagenRelleno.fillAmount = 1f;
        }

        /// <summary>
        /// Helper: crea un GameObject con Image (sprite blanco generado).
        /// </summary>
        private Image CrearImagen(string nombre, Transform padre, Color color)
        {
            var obj = new GameObject(nombre);
            obj.transform.SetParent(padre, false);

            var img = obj.AddComponent<Image>();
            img.color = color;

            return img;
        }

        /// <summary>
        /// Configura un RectTransform para que ocupe todo el espacio del padre
        /// (estilo "Stretch" en el editor).
        /// </summary>
        private void ConfigurarRectTransformEstirado(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ── Respuesta a eventos de combate ─────────────────────────────────────
        /// <summary>
        /// Callback del evento CombatSystem.OnAtaqueRealizado.
        /// Solo reacciona si ESTA unidad fue el objetivo del ataque.
        /// </summary>
        private void OnAtaqueRecibido(ResultadoCombate resultado)
        {
            // Solo actualizar si esta unidad fue la que recibió daño.
            if (resultado.Objetivo != _unidad) return;

            ActualizarBarra();
        }

        // ── API pública ────────────────────────────────────────────────────────
        /// <summary>
        /// Recalcula el valor objetivo de la barra según la vida actual.
        /// Puede llamarse manualmente (ej: al curar).
        /// </summary>
        public void ActualizarBarra()
        {
            if (_unidad == null) return;

            float vidaMax = _unidad.VidaMaxima;
            if (vidaMax <= 0) vidaMax = 1;  // Evitar división por cero.

            _vidaObjetivoNormalizado = (float)_unidad.VidaActual / vidaMax;

            // Iniciar retardo del ghost.
            _ghostEsperando = true;
            StartCoroutine(RetardarGhost());
        }

        /// <summary>
        /// Espera el retardo configurado antes de que la barra ghost
        /// empiece a reducirse.
        /// </summary>
        private IEnumerator RetardarGhost()
        {
            yield return new WaitForSeconds(RetardoGhost);
            _ghostEsperando = false;
        }

        // ── Utilidades de color ────────────────────────────────────────────────
        /// <summary>
        /// Interpola entre los 3 colores de vida según el porcentaje.
        ///   100%-50% → Verde a Amarillo
        ///   50%-0%   → Amarillo a Rojo
        /// </summary>
        private Color ObtenerColorVida(float porcentaje)
        {
            if (porcentaje > 0.5f)
            {
                // Interpolar de amarillo a verde.
                float t = (porcentaje - 0.5f) / 0.5f;
                return Color.Lerp(ColorVidaMedia, ColorVidaAlta, t);
            }
            else
            {
                // Interpolar de rojo a amarillo.
                float t = porcentaje / 0.5f;
                return Color.Lerp(ColorVidaBaja, ColorVidaMedia, t);
            }
        }
    }
}
