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
        public static BattleResultUI Instancia
        {
            get
            {
                if (_instancia == null)
                {
                    _instancia = FindFirstObjectByType<BattleResultUI>();
                    if (_instancia == null)
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
            var _ = Instancia;
        }

        // ── Referencias ────────────────────────────────────────────────────────
        public Canvas CanvasPrincipal;

        private GameObject _modalVictoria;
        private GameObject _modalDerrota;
        private Font _fuenteCache;

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
            _fuenteCache = ObtenerFuenteSegura();

            AsegurarCanvas();

            // Suscribirse a eventos de fin de batalla
            if (BattleManager.Instancia != null)
            {
                BattleManager.Instancia.OnVictoria += MostrarPantallaVictoria;
                BattleManager.Instancia.OnDerrota += MostrarPantallaDerrota;
            }
        }

        private void OnDestroy()
        {
            if (BattleManager.Instancia != null)
            {
                BattleManager.Instancia.OnVictoria -= MostrarPantallaVictoria;
                BattleManager.Instancia.OnDerrota -= MostrarPantallaDerrota;
            }
        }

        private void AsegurarCanvas()
        {
            if (CanvasPrincipal == null)
                CanvasPrincipal = FindFirstObjectByType<Canvas>();

            if (CanvasPrincipal == null)
            {
                var canvasObj = new GameObject("BattleResult_Canvas");
                CanvasPrincipal = canvasObj.AddComponent<Canvas>();
                CanvasPrincipal.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasPrincipal.sortingOrder = 300; // Por encima de todo

                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
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
                new Color(0.95f, 0.8f, 0.2f),
                new Color(0.05f, 0.15f, 0.1f, 0.95f),
                ObtenerTextoEstadisticas(),
                "Siguiente Batalla",
                new Color(0.2f, 0.75f, 0.35f),
                () => BattleManager.Instancia?.AvanzarSiguienteNivel(),
                "Reintentar Batalla",
                new Color(0.25f, 0.5f, 0.85f),
                () => BattleManager.Instancia?.ReiniciarBatalla()
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
                new Color(1f, 0.3f, 0.3f),
                new Color(0.18f, 0.05f, 0.05f, 0.95f),
                "Tus guardianes no lograron resistir el embate.",
                "Reintentar Batalla",
                new Color(0.8f, 0.25f, 0.25f),
                () => BattleManager.Instancia?.ReiniciarBatalla(),
                "Cargar Partida (F9)",
                new Color(0.3f, 0.5f, 0.9f),
                () =>
                {
                    _modalDerrota.SetActive(false);
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
            // Panel oscurecedor de fondo (bloquea clics en el juego)
            var bloqueadorObj = new GameObject($"Modal_{titulo}");
            bloqueadorObj.transform.SetParent(CanvasPrincipal.transform, false);

            var bloqueadorImg = bloqueadorObj.AddComponent<Image>();
            bloqueadorImg.color = new Color(0f, 0f, 0f, 0.65f);

            var rectBloqueador = bloqueadorObj.GetComponent<RectTransform>();
            rectBloqueador.anchorMin = Vector2.zero;
            rectBloqueador.anchorMax = Vector2.one;
            rectBloqueador.offsetMin = Vector2.zero;
            rectBloqueador.offsetMax = Vector2.zero;

            // Caja central del modal
            var cajaObj = new GameObject("CajaModal");
            cajaObj.transform.SetParent(bloqueadorObj.transform, false);

            var cajaImg = cajaObj.AddComponent<Image>();
            cajaImg.color = colorFondo;

            var rectCaja = cajaObj.GetComponent<RectTransform>();
            rectCaja.anchorMin = new Vector2(0.5f, 0.5f);
            rectCaja.anchorMax = new Vector2(0.5f, 0.5f);
            rectCaja.pivot = new Vector2(0.5f, 0.5f);
            rectCaja.sizeDelta = new Vector2(580f, 400f);

            var layout = cajaObj.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 30, 30);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;

            // Título
            CrearTextoUI(cajaObj.transform, titulo, 28, FontStyle.Bold, colorTitulo, 45f);

            // Subtítulo / Estadísticas
            CrearTextoUI(cajaObj.transform, subtitulo, 17, FontStyle.Normal, new Color(0.9f, 0.9f, 0.95f), 55f);

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

            var le = btnObj.AddComponent<LayoutElement>();
            le.minWidth = 320f;
            le.preferredWidth = 320f;
            le.minHeight = 58f;
            le.preferredHeight = 58f;

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
