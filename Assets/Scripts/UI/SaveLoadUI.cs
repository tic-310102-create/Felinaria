// ============================================================
//  SaveLoadUI.cs
//  Felinaria: El último presagio
//  Fase 4 – Persistencia y Pulido
//
//  RESPONSABILIDAD:
//    - Crea botones temporales de prueba en el Canvas para:
//        [Guardar]  [Cargar]  [Borrar]  [Sincronizar Nube]
//    - Muestra un indicador de estado de sincronización.
//    - Atajos de teclado: F5 = Guardar, F9 = Cargar.
//    - Este script es de DEBUG y puede eliminarse en la build final.
//
//  AUTO-CONFIGURACIÓN:
//    Si no existe Canvas en la escena, crea uno automáticamente.
//    Si no existe EventSystem, lo crea.
//    Usa ObtenerFuenteSegura() para garantizar texto visible en cualquier
//    versión de Unity, sin necesidad de configurar fuentes en el Inspector.
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using Felinaria.Data;
using Felinaria.Cloud;
using Felinaria.Audio;

namespace Felinaria.UI
{
    /// <summary>
    /// Panel de botones de debug para probar guardado/carga/sincronización.
    /// Auto-configurable: no requiere setup en el Inspector.
    /// </summary>
    public class SaveLoadUI : MonoBehaviour
    {
        // ── Singleton y Auto-Inicialización ────────────────────────────────────
        private static SaveLoadUI _instancia;
        public static SaveLoadUI Instancia
        {
            get
            {
                if (_instancia == null)
                {
                    _instancia = FindFirstObjectByType<SaveLoadUI>();
                    if (_instancia == null)
                    {
                        var go = new GameObject("SaveLoadUI_Auto");
                        _instancia = go.AddComponent<SaveLoadUI>();
                    }
                }
                return _instancia;
            }
            private set => _instancia = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInicializarEnEscena()
        {
            // Garantiza que SaveLoadUI siempre exista y se cree al arrancar cualquier escena
            var _ = Instancia;
        }

        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Referencia al Canvas")]
        [Tooltip("Canvas donde se crearán los botones. Si está vacío, " +
                 "se buscará o creará automáticamente.")]
        public Canvas CanvasUI;

        [Header("Configuración Visual")]
        [Tooltip("Posición del panel desde la esquina superior-derecha.")]
        public Vector2 PosicionPanel = new Vector2(-15f, -15f);

        // ── Referencias internas ───────────────────────────────────────────────
        private Text _textoEstado;
        private Font _fuenteCache;
        private GameObject _panelInstanciado;

        // ── Unity Lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            if (_instancia != null && _instancia != this)
            {
                Destroy(gameObject);
                return;
            }
            _instancia = this;
        }

        private void Start()
        {
            InicializarUI();
        }

        private void OnEnable()
        {
            if (CanvasUI == null || _panelInstanciado == null)
            {
                InicializarUI();
            }
        }

        public void InicializarUI()
        {
            if (_panelInstanciado != null) return;

            // Cachear fuente segura una sola vez.
            _fuenteCache = ObtenerFuenteSegura();

            // Buscar Canvas si no se asignó.
            if (CanvasUI == null)
                CanvasUI = FindFirstObjectByType<Canvas>();

            if (CanvasUI == null)
            {
                Debug.Log("[SaveLoadUI] No se encontró Canvas. Creando Canvas automático para UI...");
                var canvasObj = new GameObject("SaveLoadUI_Canvas");
                CanvasUI = canvasObj.AddComponent<Canvas>();
                CanvasUI.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasUI.sortingOrder = 200;

                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Asegurar EventSystem.
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            CrearPanelBotones();

            // Suscribirse al evento de sincronización.
            if (AndroidBridge.Instancia != null)
                AndroidBridge.Instancia.OnSincronizacionCompletada += OnSyncCompletada;
        }

        private void OnDestroy()
        {
            if (AndroidBridge.Instancia != null)
                AndroidBridge.Instancia.OnSincronizacionCompletada -= OnSyncCompletada;
        }

        private void Update()
        {
            // Atajos de teclado para guardado rápido.
            if (Input.GetKeyDown(KeyCode.F5))
            {
                Debug.Log("[SaveLoadUI] [Atajo F5] Guardando partida...");
                OnBotonGuardar();
            }
            if (Input.GetKeyDown(KeyCode.F9))
            {
                Debug.Log("[SaveLoadUI] [Atajo F9] Cargando partida...");
                OnBotonCargar();
            }
        }

        // ── Creación de UI ─────────────────────────────────────────────────────
        /// <summary>
        /// Crea el panel con botones de guardado/carga por código.
        /// </summary>
        private void CrearPanelBotones()
        {
            // ── Panel contenedor (esquina superior-derecha) ────────────────────
            var panelObj = new GameObject("Panel_SaveLoad");
            panelObj.transform.SetParent(CanvasUI.transform, false);
            _panelInstanciado = panelObj;

            var panelImg = panelObj.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);

            var rectPanel = panelObj.GetComponent<RectTransform>();
            rectPanel.anchorMin = new Vector2(1f, 1f);  // Esquina superior-derecha.
            rectPanel.anchorMax = new Vector2(1f, 1f);
            rectPanel.pivot = new Vector2(1f, 1f);
            rectPanel.anchoredPosition = PosicionPanel;

            // Layout vertical.
            var layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 5f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = panelObj.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── Título ─────────────────────────────────────────────────────────
            CrearTexto(panelObj.transform, "GUARDADO", 14, FontStyle.Bold, Color.white);

            // ── Botones ────────────────────────────────────────────────────────
            CrearBoton(panelObj.transform, "Guardar (F5)",
                new Color(0.2f, 0.7f, 0.3f), OnBotonGuardar);

            CrearBoton(panelObj.transform, "Cargar (F9)",
                new Color(0.3f, 0.5f, 0.9f), OnBotonCargar);

            CrearBoton(panelObj.transform, "Borrar Save",
                new Color(0.7f, 0.3f, 0.3f), OnBotonBorrar);

            CrearBoton(panelObj.transform, "Sincronizar",
                new Color(0.6f, 0.4f, 0.8f), OnBotonSincronizar);

            // ── Texto de estado ────────────────────────────────────────────────
            _textoEstado = CrearTexto(panelObj.transform, "Estado: Listo", 11,
                FontStyle.Normal, new Color(0.7f, 0.7f, 0.7f));
        }

        // ── Callbacks de botones ───────────────────────────────────────────────

        private void OnBotonGuardar()
        {
            ActualizarEstado("Guardando...");
            bool exito = SaveSystem.GuardarPartida();

            if (exito)
            {
                ActualizarEstado("Guardado OK");
                AudioManager.Instancia?.SFX_Guardar();
            }
            else
            {
                ActualizarEstado("Error al guardar");
            }
        }

        private void OnBotonCargar()
        {
            ActualizarEstado("Cargando...");
            GameData datos = SaveSystem.CargarPartida();

            if (datos != null)
            {
                ActualizarEstado($"Cargado: Ronda {datos.RondaActual}");
                Debug.Log($"[SaveLoadUI] Datos cargados: {datos}");

                // Mostrar resumen de unidades cargadas.
                foreach (var u in datos.Unidades)
                {
                    Debug.Log($"  -> {u.NombreUnidad}: HP {u.VidaActual}/{u.VidaMaxima} " +
                              $"en ({u.Columna},{u.Fila}) | Viva: {u.EstaViva}");
                }
            }
            else
            {
                ActualizarEstado("No hay partida guardada");
            }
        }

        private void OnBotonBorrar()
        {
            SaveSystem.EliminarPartidaGuardada();
            ActualizarEstado("Archivos eliminados");
        }

        private void OnBotonSincronizar()
        {
            if (AndroidBridge.Instancia == null)
            {
                ActualizarEstado("AndroidBridge no encontrado");
                return;
            }

            ActualizarEstado("Sincronizando...");
            AndroidBridge.Instancia.GuardarYSincronizar();
        }

        private void OnSyncCompletada(bool exito)
        {
            if (exito)
                ActualizarEstado("Sync OK");
            else
                ActualizarEstado("Sync fallida");
        }

        // ── Utilidades de UI ───────────────────────────────────────────────────

        private void ActualizarEstado(string mensaje)
        {
            if (_textoEstado != null)
                _textoEstado.text = mensaje;
            Debug.Log($"[SaveLoadUI] {mensaje}");
        }

        private Button CrearBoton(Transform padre, string texto, Color color,
                                   UnityEngine.Events.UnityAction callback)
        {
            var btnObj = new GameObject($"Btn_{texto}");
            btnObj.transform.SetParent(padre, false);

            // LayoutElement para que el layout no colapse el botón.
            var le = btnObj.AddComponent<LayoutElement>();
            le.minWidth = 150f;
            le.preferredWidth = 150f;
            le.minHeight = 32f;
            le.preferredHeight = 32f;

            var img = btnObj.AddComponent<Image>();
            img.color = color;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(callback);

            var txtObj = new GameObject("Texto");
            txtObj.transform.SetParent(btnObj.transform, false);

            var txt = txtObj.AddComponent<Text>();
            txt.text = texto;
            txt.color = Color.white;
            txt.font = _fuenteCache;
            txt.fontSize = 13;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontStyle = FontStyle.Bold;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;

            var rectTxt = txtObj.GetComponent<RectTransform>();
            rectTxt.anchorMin = Vector2.zero;
            rectTxt.anchorMax = Vector2.one;
            rectTxt.offsetMin = Vector2.zero;
            rectTxt.offsetMax = Vector2.zero;

            return btn;
        }

        private Text CrearTexto(Transform padre, string contenido, int tamanio,
                                FontStyle estilo, Color color)
        {
            var obj = new GameObject("Texto");
            obj.transform.SetParent(padre, false);

            var le = obj.AddComponent<LayoutElement>();
            le.minHeight = 24f;
            le.preferredHeight = 24f;

            var txt = obj.AddComponent<Text>();
            txt.text = contenido;
            txt.color = color;
            txt.font = _fuenteCache;
            txt.fontSize = tamanio;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontStyle = estilo;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;

            return txt;
        }

        /// <summary>
        /// Helper para obtener una fuente legible compatible con cualquier versión de Unity.
        /// Mismo patrón que ActionMenu.ObtenerFuenteSegura().
        /// </summary>
        private Font ObtenerFuenteSegura()
        {
            Font fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (fuente == null)
                fuente = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (fuente == null)
            {
                var fuentes = Resources.FindObjectsOfTypeAll<Font>();
                if (fuentes != null && fuentes.Length > 0)
                    fuente = fuentes[0];
            }
            if (fuente == null)
            {
                try
                {
                    fuente = Font.CreateDynamicFontFromOSFont("Arial", 14);
                }
                catch { }
            }
            return fuente;
        }
    }
}
