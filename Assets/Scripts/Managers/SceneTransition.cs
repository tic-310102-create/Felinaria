// ============================================================
//  SceneTransition.cs
//  Felinaria: El último presagio
//  Fase 5 – Victoria, Derrota y Game Loop
//
//  RESPONSABILIDAD:
//    - Singleton global con DontDestroyOnLoad para transiciones suaves
//      entre escenas o reinicio de niveles con fundido a negro (Fade In / Fade Out).
//    - Auto-crea su Canvas y cortina negra si no existen.
// ============================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Felinaria.Managers
{
    /// <summary>
    /// Gestiona transiciones animadas con fundido a negro entre escenas y niveles.
    /// Auto-configurable y persistente con DontDestroyOnLoad.
    /// </summary>
    public class SceneTransition : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        private static SceneTransition _instancia;
        private static bool _aplicacionCerrando = false;

        /// <summary>Indica si existe una instancia activa sin forzar su creación.</summary>
        public static bool InstanciaExiste => _instancia != null && !_aplicacionCerrando;

        public static SceneTransition Instancia
        {
            get
            {
                if (_aplicacionCerrando) return null;
                if (_instancia == null)
                {
                    _instancia = FindFirstObjectByType<SceneTransition>();
                    if (_instancia == null && Application.isPlaying)
                    {
                        var go = new GameObject("SceneTransition_Auto");
                        _instancia = go.AddComponent<SceneTransition>();
                    }
                }
                return _instancia;
            }
            private set => _instancia = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInicializar()
        {
            if (!_aplicacionCerrando)
            {
                var _ = Instancia;
            }
        }

        // ── Configuración ──────────────────────────────────────────────────────
        [Header("Configuración de Fundido")]
        [Tooltip("Duración predeterminada del fade in/out en segundos.")]
        public float DuracionFadeDefault = 0.5f;

        [Tooltip("Color de la cortinilla de transición.")]
        public Color ColorCortina = Color.black;

        // ── Referencias internas ───────────────────────────────────────────────
        private Canvas _canvasTransition;
        private Image _imagenCortina;
        private bool _estaEnTransicion = false;

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
            DontDestroyOnLoad(gameObject);

            CrearCortinaVisual();
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
            // Fade-in inicial al arrancar la escena (de negro a transparente)
            StartCoroutine(FadeIn(DuracionFadeDefault));
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Ejecutar fade-in cada vez que se carga una escena nueva
            StartCoroutine(FadeIn(DuracionFadeDefault));
        }

        // ── Creación visual ────────────────────────────────────────────────────
        private void CrearCortinaVisual()
        {
            if (_imagenCortina != null) return;

            var canvasObj = new GameObject("Transition_Canvas");
            canvasObj.transform.SetParent(transform);

            _canvasTransition = canvasObj.AddComponent<Canvas>();
            _canvasTransition.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvasTransition.sortingOrder = 999; // Máxima prioridad visual

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var raycaster = canvasObj.AddComponent<GraphicRaycaster>();

            var cortinaObj = new GameObject("CortinaNegra");
            cortinaObj.transform.SetParent(canvasObj.transform, false);

            _imagenCortina = cortinaObj.AddComponent<Image>();
            _imagenCortina.color = new Color(ColorCortina.r, ColorCortina.g, ColorCortina.b, 0f);
            _imagenCortina.raycastTarget = false;

            var rect = cortinaObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ── API pública ────────────────────────────────────────────────────────
        /// <summary>
        /// Carga una nueva escena por su nombre con fundido suave.
        /// </summary>
        public void CargarEscena(string nombreEscena, float duracion = -1f)
        {
            if (_estaEnTransicion) return;
            float d = (duracion > 0) ? duracion : DuracionFadeDefault;
            StartCoroutine(RutinaCargarEscena(nombreEscena, d));
        }

        /// <summary>
        /// Reinicia la escena actual con fundido suave.
        /// </summary>
        public void ReiniciarNivelActual(float duracion = -1f)
        {
            if (_estaEnTransicion) return;
            string escenaActual = SceneManager.GetActiveScene().name;
            float d = (duracion > 0) ? duracion : DuracionFadeDefault;
            StartCoroutine(RutinaCargarEscena(escenaActual, d));
        }

        /// <summary>
        /// Ejecuta un fade out a negro, invoca una acción en pantalla negra y luego fade in.
        /// </summary>
        public void TransicionPersonalizada(Action accionEnNegro, float duracion = -1f)
        {
            if (_estaEnTransicion) return;
            float d = (duracion > 0) ? duracion : DuracionFadeDefault;
            StartCoroutine(RutinaAccionPersonalizada(accionEnNegro, d));
        }

        // ── Coroutines ─────────────────────────────────────────────────────────
        private IEnumerator RutinaCargarEscena(string nombreEscena, float duracion)
        {
            _estaEnTransicion = true;
            yield return StartCoroutine(FadeOut(duracion));

            var operacion = SceneManager.LoadSceneAsync(nombreEscena);
            while (operacion != null && !operacion.isDone)
            {
                yield return null;
            }

            yield return StartCoroutine(FadeIn(duracion));
            _estaEnTransicion = false;
        }

        private IEnumerator RutinaAccionPersonalizada(Action accion, float duracion)
        {
            _estaEnTransicion = true;
            yield return StartCoroutine(FadeOut(duracion));

            try
            {
                accion?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneTransition] Error durante acción: {ex.Message}");
            }

            yield return StartCoroutine(FadeIn(duracion));
            _estaEnTransicion = false;
        }

        private IEnumerator FadeOut(float duracion)
        {
            if (_imagenCortina == null) yield break;
            _imagenCortina.raycastTarget = true; // Bloquear clics durante la transición

            float tiempo = 0f;
            while (tiempo < duracion)
            {
                tiempo += Time.unscaledDeltaTime;
                float alfa = Mathf.Clamp01(tiempo / duracion);
                _imagenCortina.color = new Color(ColorCortina.r, ColorCortina.g, ColorCortina.b, alfa);
                yield return null;
            }

            _imagenCortina.color = new Color(ColorCortina.r, ColorCortina.g, ColorCortina.b, 1f);
        }

        private IEnumerator FadeIn(float duracion)
        {
            if (_imagenCortina == null) yield break;

            float tiempo = 0f;
            while (tiempo < duracion)
            {
                tiempo += Time.unscaledDeltaTime;
                float alfa = 1f - Mathf.Clamp01(tiempo / duracion);
                _imagenCortina.color = new Color(ColorCortina.r, ColorCortina.g, ColorCortina.b, alfa);
                yield return null;
            }

            _imagenCortina.color = new Color(ColorCortina.r, ColorCortina.g, ColorCortina.b, 0f);
            _imagenCortina.raycastTarget = false; // Permitir clics de nuevo
        }
    }
}
