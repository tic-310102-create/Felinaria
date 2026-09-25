// ============================================================
//  SaveLoadUI.cs
//  Felinaria: El último presagio
//  Fase 4 – Persistencia y Pulido
//
//  RESPONSABILIDAD:
//    - Crea botones temporales de prueba en el Canvas para:
//        [Guardar]  [Cargar]  [Borrar]  [Sincronizar Nube]
//    - Muestra un indicador de estado de sincronización.
//    - Este script es de DEBUG y puede eliminarse en la build final.
//
//  CÓMO FUNCIONA (para principiantes):
//    Se coloca en el mismo GameObject que tiene el ActionMenu
//    o en el GameMaster. Crea botones en la esquina superior
//    derecha del Canvas para probar el sistema de guardado.
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using Felinaria.Data;
using Felinaria.Cloud;

namespace Felinaria.UI
{
    /// <summary>
    /// Panel de botones de debug para probar guardado/carga/sincronización.
    /// Se coloca en el GameMaster o en un GO dedicado.
    /// </summary>
    public class SaveLoadUI : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Referencia al Canvas")]
        [Tooltip("Canvas donde se crearán los botones. Si está vacío, " +
                 "se buscará el Canvas del ActionMenu.")]
        public Canvas CanvasUI;

        [Header("Configuración Visual")]
        [Tooltip("Posición del panel desde la esquina superior-derecha.")]
        public Vector2 PosicionPanel = new Vector2(-10f, -10f);

        // ── Referencias internas ───────────────────────────────────────────────
        private Text _textoEstado;

        // ── Unity Lifecycle ────────────────────────────────────────────────────
        private void Start()
        {
            // Buscar Canvas si no se asignó.
            if (CanvasUI == null)
                CanvasUI = FindObjectOfType<Canvas>();

            if (CanvasUI == null)
            {
                Debug.LogWarning("[SaveLoadUI] No se encontró Canvas. Creando uno...");
                var canvasObj = new GameObject("SaveLoadUI_Canvas");
                CanvasUI = canvasObj.AddComponent<Canvas>();
                CanvasUI.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasUI.sortingOrder = 200;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
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

        // ── Creación de UI ─────────────────────────────────────────────────────
        /// <summary>
        /// Crea el panel con botones de guardado/carga por código.
        /// </summary>
        private void CrearPanelBotones()
        {
            // ── Panel contenedor (esquina superior-derecha) ────────────────────
            var panelObj = new GameObject("Panel_SaveLoad");
            panelObj.transform.SetParent(CanvasUI.transform, false);

            var panelImg = panelObj.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);

            var rectPanel = panelObj.GetComponent<RectTransform>();
            rectPanel.anchorMin = new Vector2(1f, 1f);  // Esquina superior-derecha.
            rectPanel.anchorMax = new Vector2(1f, 1f);
            rectPanel.pivot = new Vector2(1f, 1f);
            rectPanel.anchoredPosition = PosicionPanel;
            rectPanel.sizeDelta = new Vector2(170f, 230f);

            // Layout vertical.
            var layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 5f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // ── Título ─────────────────────────────────────────────────────────
            CrearTexto(panelObj.transform, "💾 GUARDADO", 14, FontStyle.Bold, Color.white);

            // ── Botones ────────────────────────────────────────────────────────
            CrearBoton(panelObj.transform, "✅ Guardar",
                new Color(0.2f, 0.7f, 0.3f), OnBotonGuardar);

            CrearBoton(panelObj.transform, "📂 Cargar",
                new Color(0.3f, 0.5f, 0.9f), OnBotonCargar);

            CrearBoton(panelObj.transform, "🗑 Borrar Save",
                new Color(0.7f, 0.3f, 0.3f), OnBotonBorrar);

            CrearBoton(panelObj.transform, "☁️ Sincronizar",
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
                ActualizarEstado("✅ Guardado OK");
                // Reproducir SFX si existe.
                Felinaria.Audio.AudioManager.Instancia?.SFX_Guardar();
            }
            else
            {
                ActualizarEstado("❌ Error al guardar");
            }
        }

        private void OnBotonCargar()
        {
            ActualizarEstado("Cargando...");
            GameData datos = SaveSystem.CargarPartida();

            if (datos != null)
            {
                ActualizarEstado($"✅ Cargado: Ronda {datos.RondaActual}");
                Debug.Log($"[SaveLoadUI] Datos cargados: {datos}");

                // Mostrar resumen de unidades cargadas.
                foreach (var u in datos.Unidades)
                {
                    Debug.Log($"  → {u.NombreUnidad}: HP {u.VidaActual}/{u.VidaMaxima} " +
                              $"en ({u.Columna},{u.Fila}) | Viva: {u.EstaViva}");
                }
            }
            else
            {
                ActualizarEstado("⚠️ No hay partida guardada");
            }
        }

        private void OnBotonBorrar()
        {
            SaveSystem.EliminarPartidaGuardada();
            ActualizarEstado("🗑 Archivos eliminados");
        }

        private void OnBotonSincronizar()
        {
            if (AndroidBridge.Instancia == null)
            {
                ActualizarEstado("❌ AndroidBridge no encontrado");
                return;
            }

            ActualizarEstado("☁️ Sincronizando...");
            AndroidBridge.Instancia.GuardarYSincronizar();
        }

        private void OnSyncCompletada(bool exito)
        {
            if (exito)
                ActualizarEstado("☁️ ✅ Sync OK");
            else
                ActualizarEstado("☁️ ⚠️ Sync fallida");
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

            var img = btnObj.AddComponent<Image>();
            img.color = color;

            var btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(callback);

            var rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(150f, 32f);

            var txtObj = new GameObject("Texto");
            txtObj.transform.SetParent(btnObj.transform, false);

            var txt = txtObj.AddComponent<Text>();
            txt.text = texto;
            txt.color = Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 13;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontStyle = FontStyle.Bold;

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

            var txt = obj.AddComponent<Text>();
            txt.text = contenido;
            txt.color = color;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = tamanio;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontStyle = estilo;

            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(150f, 24f);

            return txt;
        }
    }
}
