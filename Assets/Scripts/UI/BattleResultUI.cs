// ============================================================
//  BattleResultUI.cs
//  Felinaria: El último presagio
//  Fase 5 – Victoria, Derrota y Game Loop
//
//  RESPONSABILIDAD:
//    - Construye los paneles de fin de batalla (Victoria / Derrota).
//    - Muestra estadísticas de la partida (Rondas, Bajas, Tiempo, Daño).
//    - Ofrece opciones de "Siguiente Nivel", "Reintentar" y "Cargar Partida".
//    - Auto-configurable en runtime a resolución 1080p.
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using Felinaria.Managers;
using Felinaria.Data;
using Felinaria.Grid;

namespace Felinaria.UI
{
    /// <summary>
    /// UI flotante para la pantalla de victoria y derrota.
    /// Auto-configurable en cualquier Canvas.
    /// </summary>
    public class BattleResultUI : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        private static BattleResultUI _instancia;
        private static bool _aplicacionCerrando = false;

        /// <summary>Indica si existe una instancia activa sin forzar su creación.</summary>
        public static bool InstanciaExiste => _instancia != null && !_aplicacionCerrando;

        public static BattleResultUI Instancia
        {
            get
            {
                if (_aplicacionCerrando) return null;
                if (_instancia == null)
                {
                    _instancia = FindFirstObjectByType<BattleResultUI>();
                    if (_instancia == null && Application.isPlaying)
                    {
                        var go = new GameObject("BattleResultUI_Auto");
                        _instancia = go.AddComponent<BattleResultUI>();
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

        // ── Referencias ────────────────────────────────────────────────────────
        public Canvas CanvasPrincipal;

        private GameObject _modalVictoria;
        private GameObject _modalDerrota;
        private Font _fuenteCache;

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
        }

        private void Start()
        {
            _fuenteCache = ObtenerFuenteSegura();

            AsegurarCanvas();

            // Suscribirse a eventos de fin de batalla
            if (BattleManager.Instancia != null)
            {
                BattleManager.Instancia.OnVictoria += MostrarPantallaVictoria;
                BattleManager.Instancia.OnDerrota += MostrarPantallaDerrota;
            }
        }

        private void OnApplicationQuit()
        {
            _aplicacionCerrando = true;
        }

        private void OnDestroy()
        {
            if (BattleManager.InstanciaExiste)
            {
                BattleManager.Instancia.OnVictoria -= MostrarPantallaVictoria;
                BattleManager.Instancia.OnDerrota -= MostrarPantallaDerrota;
            }

            if (_instancia == this)
            {
                _instancia = null;
            }
        }

        private void AsegurarCanvas()
        {
            if (CanvasPrincipal == null || CanvasPrincipal.renderMode != RenderMode.ScreenSpaceOverlay || CanvasPrincipal.name == "HealthBar_Canvas")
            {
                CanvasPrincipal = null;
                var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                foreach (var c in canvases)
                {
                    if (c != null && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.name != "HealthBar_Canvas")
                    {
                        CanvasPrincipal = c;
                        break;
                    }
                }
            }

            if (CanvasPrincipal == null)
            {
                var canvasObj = new GameObject("BattleResult_Canvas");
                CanvasPrincipal = canvasObj.AddComponent<Canvas>();
                CanvasPrincipal.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasPrincipal.sortingOrder = 300; // Por encima de todo

                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Garantizar CanvasScaler a 1920x1080
            var scaler = CanvasPrincipal.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = CanvasPrincipal.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        // ── Pantallas de Fin de Partida ────────────────────────────────────────
        public void MostrarPantallaVictoria()
        {
            AsegurarCanvas();
            if (_modalVictoria != null)
            {
                _modalVictoria.SetActive(true);
                return;
            }

            _modalVictoria = CrearModal(
                "¡VICTORIA EN FELINARIA!",
                new Color(0.98f, 0.85f, 0.25f),
                new Color(0.06f, 0.14f, 0.10f, 0.96f),
                ObtenerTextoEstadisticas(),
                "Siguiente Batalla",
                new Color(0.2f, 0.72f, 0.35f),
                () =>
                {
                    if (_modalVictoria != null) _modalVictoria.SetActive(false);
                    if (Grid.MapLoader.InstanciaExiste)
                    {
                        Grid.MapLoader.Instancia.CargarSiguienteBatalla();
                    }
                    else
                    {
                        BattleManager.Instancia?.AvanzarSiguienteNivel();
                    }
                },
                "Reintentar Batalla",
                new Color(0.25f, 0.5f, 0.85f),
                () =>
                {
                    if (_modalVictoria != null) _modalVictoria.SetActive(false);
                    BattleManager.Instancia?.ReiniciarBatalla();
                }
            );
        }

        public void MostrarPantallaDerrota()
        {
            AsegurarCanvas();
            if (_modalDerrota != null)
            {
                _modalDerrota.SetActive(true);
                return;
            }

            _modalDerrota = CrearModal(
                "¡DERROTA!",
                new Color(1f, 0.32f, 0.32f),
                new Color(0.16f, 0.05f, 0.05f, 0.96f),
                "Tus guardianes no lograron resistir el embate.",
                "Reintentar Batalla",
                new Color(0.8f, 0.25f, 0.25f),
                () =>
                {
                    if (_modalDerrota != null) _modalDerrota.SetActive(false);
                    BattleManager.Instancia?.ReiniciarBatalla();
                },
                "Cargar Partida (F9)",
                new Color(0.3f, 0.5f, 0.9f),
                () =>
                {
                    if (_modalDerrota != null) _modalDerrota.SetActive(false);
                    SaveSystem.CargarPartida();
                    BattleManager.Instancia?.VerificarCondicionesFinDeBatalla();
                }
            );
        }

        private string ObtenerTextoEstadisticas()
        {
            int rondas = (TurnManager.Instancia != null) ? TurnManager.Instancia.RondaActual : 1;
            int bajas = (BattleManager.Instancia != null) ? BattleManager.Instancia.BajasEnemigas : 0;
            int danio = (BattleManager.Instancia != null) ? BattleManager.Instancia.DanioTotalInfligido : 0;
            float tiempo = (BattleManager.Instancia != null) ? BattleManager.Instancia.DuracionBatallaSegundos : 0f;

            int minutos = Mathf.FloorToInt(tiempo / 60f);
            int segundos = Mathf.FloorToInt(tiempo % 60f);

            return $"Rondas: {rondas}   |   Bajas Enemigas: {bajas}\n" +
                   $"Daño Total: {danio}   |   Tiempo: {minutos:00}:{segundos:00}";
        }

        // ── Generador de Modal ─────────────────────────────────────────────────
        private GameObject CrearModal(
            string titulo, Color colorTitulo, Color colorFondo,
            string subtitulo,
            string txtBtn1, Color colorBtn1, UnityEngine.Events.UnityAction cb1,
            string txtBtn2, Color colorBtn2, UnityEngine.Events.UnityAction cb2)
        {
            // Panel oscurecedor de fondo a pantalla completa
            var bloqueadorObj = new GameObject($"Modal_{titulo}");
            bloqueadorObj.transform.SetParent(CanvasPrincipal.transform, false);
            bloqueadorObj.transform.localScale = Vector3.one;

            var bloqueadorImg = bloqueadorObj.AddComponent<Image>();
            bloqueadorImg.color = new Color(0f, 0f, 0f, 0.72f);

            var rectBloqueador = bloqueadorObj.GetComponent<RectTransform>();
            rectBloqueador.anchorMin = Vector2.zero;
            rectBloqueador.anchorMax = Vector2.one;
            rectBloqueador.offsetMin = Vector2.zero;
            rectBloqueador.offsetMax = Vector2.zero;

            // Caja central del modal delimitada (640x440 px)
            var cajaObj = new GameObject("CajaModal");
            cajaObj.transform.SetParent(bloqueadorObj.transform, false);
            cajaObj.transform.localScale = Vector3.one;

            var cajaImg = cajaObj.AddComponent<Image>();
            cajaImg.color = colorFondo;

            var rectCaja = cajaObj.GetComponent<RectTransform>();
            rectCaja.anchorMin = new Vector2(0.5f, 0.5f);
            rectCaja.anchorMax = new Vector2(0.5f, 0.5f);
            rectCaja.pivot = new Vector2(0.5f, 0.5f);
            rectCaja.sizeDelta = new Vector2(640f, 440f);
            rectCaja.anchoredPosition = Vector2.zero;

            var layout = cajaObj.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 30, 30);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;

            // Título centrado (tamaño de fuente 30, nítido y delimitado)
            CrearTextoUI(cajaObj.transform, titulo, 30, FontStyle.Bold, colorTitulo, 50f);

            // Subtítulo / Estadísticas (tamaño de fuente 18)
            CrearTextoUI(cajaObj.transform, subtitulo, 18, FontStyle.Normal, new Color(0.92f, 0.92f, 0.95f), 60f);

            // Botón 1
            CrearBotonModal(cajaObj.transform, txtBtn1, colorBtn1, cb1);

            // Botón 2
            CrearBotonModal(cajaObj.transform, txtBtn2, colorBtn2, cb2);

            return bloqueadorObj;
        }

        private Button CrearBotonModal(Transform padre, string texto, Color color, UnityEngine.Events.UnityAction callback)
        {
            var btnObj = new GameObject($"Btn_{texto}");
            btnObj.transform.SetParent(padre, false);
            btnObj.transform.localScale = Vector3.one;

            var le = btnObj.AddComponent<LayoutElement>();
            le.minWidth = 340f;
            le.preferredWidth = 340f;
            le.minHeight = 54f;
            le.preferredHeight = 54f;

            var img = btnObj.AddComponent<Image>();
            img.color = color;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            var colores = btn.colors;
            colores.normalColor = color;
            colores.highlightedColor = Color.Lerp(color, Color.white, 0.3f);
            colores.pressedColor = Color.Lerp(color, Color.black, 0.3f);
            btn.colors = colores;
            btn.onClick.AddListener(callback);

            var txtObj = new GameObject("Texto");
            txtObj.transform.SetParent(btnObj.transform, false);
            txtObj.transform.localScale = Vector3.one;

            var txt = txtObj.AddComponent<Text>();
            txt.text = texto;
            txt.color = Color.white;
            txt.font = _fuenteCache;
            txt.fontSize = 20;
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

        private Text CrearTextoUI(Transform padre, string contenido, int tamanio, FontStyle estilo, Color color, float altura)
        {
            var obj = new GameObject("Texto");
            obj.transform.SetParent(padre, false);
            obj.transform.localScale = Vector3.one;

            var le = obj.AddComponent<LayoutElement>();
            le.minHeight = altura;
            le.preferredHeight = altura;

            var txt = obj.AddComponent<Text>();
            txt.text = contenido;
            txt.color = color;
            txt.font = _fuenteCache;
            txt.fontSize = tamanio;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontStyle = estilo;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;

            return txt;
        }

        private Font ObtenerFuenteSegura()
        {
            Font fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (fuente == null)
            {
                var fuentes = Resources.FindObjectsOfTypeAll<Font>();
                if (fuentes != null && fuentes.Length > 0) fuente = fuentes[0];
            }
            if (fuente == null)
            {
                try { fuente = Font.CreateDynamicFontFromOSFont("Arial", 16); } catch { }
            }
            return fuente;
        }
    }
}
