// ============================================================
//  ActionMenu.cs
//  Felinaria: El último presagio
//  Fase 2 – Combate y Stats
//
//  RESPONSABILIDAD:
//    - Detecta clics/toques del jugador sobre unidades aliadas.
//    - Muestra un menú contextual flotante con opciones: Mover, Atacar, Esperar.
//    - Gestiona los diferentes modos de interacción:
//        Modo Selección → Modo Mover (clic en celda destino)
//        Modo Selección → Modo Atacar (clic en enemigo objetivo)
//    - Posiciona el menú cerca de la unidad seleccionada en coordenadas de pantalla.
//
//  CÓMO FUNCIONA (para principiantes):
//    1. El jugador hace clic en una de sus unidades → aparece el menú.
//    2. Selecciona "Mover" → el menú se oculta y espera otro clic en una celda.
//    3. El jugador hace clic en la celda destino → la unidad se mueve.
//    Se usa un Canvas en modo "Screen Space - Overlay" para los botones.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Felinaria.Units;
using Felinaria.Grid;
using Felinaria.Combat;
using Felinaria.Managers;

namespace Felinaria.UI
{
    // ─── Modos de interacción ────────────────────────────────────────────────
    /// <summary>
    /// Estado actual de la interacción del jugador con el tablero.
    /// </summary>
    public enum ModoInteraccion
    {
        Seleccion,       // Esperando que el jugador seleccione una unidad.
        MenuVisible,     // El menú de acciones está abierto.
        EsperandoMover,  // Esperando que el jugador haga clic en una celda destino.
        EsperandoAtacar, // Esperando que el jugador haga clic en un enemigo.
    }

    /// <summary>
    /// Singleton que gestiona la selección de unidades y el menú contextual de acciones.
    /// Se coloca en el GameObject "GameMaster" o en un GO dedicado "UIManager".
    /// </summary>
    public class ActionMenu : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        public static ActionMenu Instancia { get; private set; }

        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Referencia al Canvas")]
        [Tooltip("Canvas principal de la UI (Screen Space - Overlay). " +
                 "Si está vacío, se creará automáticamente.")]
        public Canvas CanvasPrincipal;

        [Header("Configuración Visual del Menú")]
        [Tooltip("Desplazamiento del menú respecto a la unidad seleccionada (en píxeles).")]
        public Vector2 OffsetMenu = new Vector2(80f, 0f);

        [Tooltip("Ancho de cada botón del menú.")]
        public float AnchoBoton = 120f;

        [Tooltip("Alto de cada botón del menú.")]
        public float AltoBoton = 40f;

        [Tooltip("Espacio entre botones.")]
        public float EspaciadoBoton = 5f;

        [Header("Colores de los Botones")]
        [Tooltip("Color del botón Mover.")]
        public Color ColorBotonMover   = new Color(0.2f, 0.6f, 1f, 1f);

        [Tooltip("Color del botón Atacar.")]
        public Color ColorBotonAtacar  = new Color(0.9f, 0.2f, 0.2f, 1f);

        [Tooltip("Color del botón Esperar.")]
        public Color ColorBotonEsperar = new Color(0.7f, 0.7f, 0.7f, 1f);

        [Tooltip("Color del texto de los botones.")]
        public Color ColorTexto = Color.white;

        [Header("Resaltado de Celdas")]
        [Tooltip("Color para resaltar las celdas a las que puedes moverte.")]
        public Color ColorResaltadoMover  = new Color(0.3f, 0.7f, 1f, 0.5f);

        [Tooltip("Color para resaltar los enemigos que puedes atacar.")]
        public Color ColorResaltadoAtacar = new Color(1f, 0.3f, 0.3f, 0.5f);

        // ── Estado interno ─────────────────────────────────────────────────────
        /// <summary>Modo de interacción actual.</summary>
        public ModoInteraccion ModoActual { get; private set; } = ModoInteraccion.Seleccion;

        /// <summary>Unidad actualmente seleccionada por el jugador.</summary>
        public UnitController UnidadSeleccionada { get; private set; }

        // Objetos UI del menú.
        private GameObject _panelMenu;
        private Button _botonMover;
        private Button _botonAtacar;
        private Button _botonEsperar;

        // Celdas resaltadas para limpiar después.
        private List<SpriteRenderer> _celdasResaltadas = new List<SpriteRenderer>();
        private Dictionary<SpriteRenderer, Color> _coloresOriginales = new Dictionary<SpriteRenderer, Color>();

        // Cámara principal cacheada.
        private Camera _camaraPrincipal;

        // ── Unity Lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instancia != null && Instancia != this)
            {
                Debug.LogWarning("[ActionMenu] Ya existe una instancia. Destruyendo duplicado.");
                Destroy(gameObject);
                return;
            }
            Instancia = this;
        }

        private void Start()
        {
            _camaraPrincipal = Camera.main;

            // Crear Canvas si no se asignó uno manualmente.
            if (CanvasPrincipal == null)
                CrearCanvasAutomatico();

            CrearMenuUI();
            OcultarMenu();
        }

        private void Update()
        {
            // Solo procesar input durante el turno del jugador.
            if (TurnManager.Instancia != null &&
                TurnManager.Instancia.EstadoActual != EstadoTurno.TurnoJugador)
                return;

            ProcesarInput();
        }

        // ── Procesamiento de Input ─────────────────────────────────────────────
        /// <summary>
        /// Lee clics del mouse / toques en pantalla y actúa según el modo actual.
        /// Utiliza Physics.Raycast (3D) para interactuar con unidades y tablero.
        /// </summary>
        private void ProcesarInput()
        {
            // Detectar clic izquierdo o toque.
            if (!Input.GetMouseButtonDown(0)) return;

            // Ignorar si el clic fue sobre la UI (botones del menú).
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            if (_camaraPrincipal == null)
                _camaraPrincipal = Camera.main;

            if (_camaraPrincipal == null) return;

            // Generar rayo 3D desde la cámara a la posición del cursor.
            Ray ray = _camaraPrincipal.ScreenPointToRay(Input.mousePosition);

            switch (ModoActual)
            {
                case ModoInteraccion.Seleccion:
                    IntentarSeleccionarUnidad(ray);
                    break;

                case ModoInteraccion.MenuVisible:
                    // Si hace clic fuera del menú, cerrar.
                    CerrarMenu();
                    break;

                case ModoInteraccion.EsperandoMover:
                    IntentarMover(ray);
                    break;

                case ModoInteraccion.EsperandoAtacar:
                    IntentarAtacar(ray);
                    break;
            }
        }

        /// <summary>
        /// Intenta seleccionar una unidad aliada en la posición del rayo 3D.
        /// </summary>
        private void IntentarSeleccionarUnidad(Ray ray)
        {
            // RaycastAll 3D para asegurar que encontramos la unidad incluso si hay colliders de celdas o triggers superpuestos.
            RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0) return;

            // Ordenar por cercanía a la cámara
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            UnitController unidad = null;
            foreach (var hit in hits)
            {
                var u = hit.collider.GetComponentInParent<UnitController>() ?? hit.collider.GetComponent<UnitController>();
                if (u != null)
                {
                    unidad = u;
                    break;
                }
            }

            if (unidad == null) return;

            // Solo se pueden seleccionar unidades del jugador.
            if (unidad.BandoUnidad != Bando.Jugador)
            {
                Debug.Log($"[ActionMenu] '{unidad.NombreUnidad}' es enemiga. No puedes seleccionarla directamente.");
                return;
            }

            // No seleccionar unidades que ya actuaron.
            if (unidad.YaActuoEsteTurno)
            {
                Debug.Log($"[ActionMenu] '{unidad.NombreUnidad}' ya actuó este turno.");
                return;
            }

            SeleccionarUnidad(unidad);
        }

        /// <summary>
        /// Selecciona una unidad y muestra el menú de acciones.
        /// </summary>
        private void SeleccionarUnidad(UnitController unidad)
        {
            UnidadSeleccionada = unidad;
            Debug.Log($"[ActionMenu] Unidad seleccionada: '{unidad.NombreUnidad}' en ({unidad.Coordenada.x},{unidad.Coordenada.y})");

            MostrarMenu();
        }

        /// <summary>
        /// Intenta mover la unidad seleccionada a la celda clickeada en el espacio 3D.
        /// </summary>
        private void IntentarMover(Ray ray)
        {
            if (UnidadSeleccionada == null || GridManager.Instancia == null)
            {
                CerrarMenu();
                return;
            }

            Vector3 puntoImpacto = Vector3.zero;
            bool hayImpacto = false;

            // 1. Intentar Raycast 3D contra colliders de la escena (celdas o terreno)
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Collide))
            {
                puntoImpacto = hit.point;
                hayImpacto = true;
            }
            else
            {
                // 2. Si las celdas no tienen collider, intersectar con el plano del tablero
                Plane planoTablero = new Plane(Vector3.forward, GridManager.Instancia.OrigenMundo);
                if (planoTablero.Raycast(ray, out float distancia))
                {
                    puntoImpacto = ray.GetPoint(distancia);
                    hayImpacto = true;
                }
                else
                {
                    Plane planoInvertido = new Plane(-Vector3.forward, GridManager.Instancia.OrigenMundo);
                    if (planoInvertido.Raycast(ray, out distancia))
                    {
                        puntoImpacto = ray.GetPoint(distancia);
                        hayImpacto = true;
                    }
                }
            }

            if (!hayImpacto)
            {
                CerrarMenu();
                return;
            }

            Vector2Int coordDestino = GridManager.Instancia.MundoACoordenada(puntoImpacto);

            bool exito = UnidadSeleccionada.MoverACelda(coordDestino.x, coordDestino.y);

            if (exito)
            {
                Debug.Log($"[ActionMenu] Moviendo '{UnidadSeleccionada.NombreUnidad}' a ({coordDestino.x},{coordDestino.y}).");
            }
            else
            {
                Debug.Log($"[ActionMenu] No se pudo mover a ({coordDestino.x},{coordDestino.y}).");
            }

            // Limpiar siempre después del intento (exitoso o no).
            LimpiarResaltado();
            ModoActual = ModoInteraccion.Seleccion;
            UnidadSeleccionada = null;
        }

        /// <summary>
        /// Intenta atacar al enemigo seleccionado mediante Raycast 3D.
        /// </summary>
        private void IntentarAtacar(Ray ray)
        {
            if (UnidadSeleccionada == null)
            {
                CerrarMenu();
                return;
            }

            // RaycastAll 3D para encontrar la unidad enemiga clickeada.
            RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Collide);
            UnitController objetivo = null;

            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var hit in hits)
                {
                    var u = hit.collider.GetComponentInParent<UnitController>() ?? hit.collider.GetComponent<UnitController>();
                    if (u != null && u.BandoUnidad == Bando.Enemigo)
                    {
                        objetivo = u;
                        break;
                    }
                }
            }

            if (objetivo == null)
            {
                Debug.Log("[ActionMenu] No se detectó ninguna unidad enemiga en esa posición.");
                LimpiarResaltado();
                ModoActual = ModoInteraccion.Seleccion;
                return;
            }

            // Ejecutar ataque a través del CombatSystem.
            if (CombatSystem.Instancia != null)
            {
                bool exito = CombatSystem.Instancia.EjecutarAtaque(UnidadSeleccionada, objetivo);
                if (exito)
                    Debug.Log($"[ActionMenu] ¡Ataque ejecutado!");
                else
                    Debug.Log($"[ActionMenu] Ataque fallido (fuera de rango u otra validación).");
            }
            else
            {
                Debug.LogError("[ActionMenu] No se encontró CombatSystem en la escena.");
            }

            LimpiarResaltado();
            ModoActual = ModoInteraccion.Seleccion;
            UnidadSeleccionada = null;
        }

        // ── Acciones de los botones ────────────────────────────────────────────
        /// <summary>
        /// Callback del botón "Mover".
        /// </summary>
        public void OnBotonMover()
        {
            if (UnidadSeleccionada == null) return;

            Debug.Log($"[ActionMenu] Modo: MOVER para '{UnidadSeleccionada.NombreUnidad}'.");
            OcultarMenu();
            ModoActual = ModoInteraccion.EsperandoMover;

            // Resaltar celdas alcanzables.
            ResaltarCeldasMovimiento();
        }

        /// <summary>
        /// Callback del botón "Atacar".
        /// </summary>
        public void OnBotonAtacar()
        {
            if (UnidadSeleccionada == null) return;

            Debug.Log($"[ActionMenu] Modo: ATACAR para '{UnidadSeleccionada.NombreUnidad}'.");
            OcultarMenu();
            ModoActual = ModoInteraccion.EsperandoAtacar;

            // Resaltar enemigos en rango.
            ResaltarEnemigosEnRango();
        }

        /// <summary>
        /// Callback del botón "Esperar".
        /// La unidad pasa su turno sin hacer nada.
        /// </summary>
        public void OnBotonEsperar()
        {
            if (UnidadSeleccionada == null) return;

            Debug.Log($"[ActionMenu] '{UnidadSeleccionada.NombreUnidad}' decide esperar.");

            // Marcar como usada (gasta su turno).
            UnidadSeleccionada.MarcarComoUsadaPublico();

            CerrarMenu();
        }

        // ── Menú UI ────────────────────────────────────────────────────────────
        /// <summary>
        /// Muestra el menú de acciones posicionado junto a la unidad seleccionada.
        /// </summary>
        private void MostrarMenu()
        {
            if (_panelMenu == null || UnidadSeleccionada == null) return;

            // Convertir posición de la unidad (mundo) a posición de pantalla (UI).
            Vector3 posPantalla = _camaraPrincipal.WorldToScreenPoint(UnidadSeleccionada.transform.position);

            // Posicionar el panel del menú.
            var rectPanel = _panelMenu.GetComponent<RectTransform>();
            rectPanel.position = posPantalla + (Vector3)OffsetMenu;

            // Habilitar/deshabilitar botón atacar según si hay enemigos en rango.
            bool hayObjetivos = ExistenEnemigosEnRango();
            _botonAtacar.interactable = hayObjetivos;

            _panelMenu.SetActive(true);
            ModoActual = ModoInteraccion.MenuVisible;
        }

        /// <summary>
        /// Oculta el panel del menú sin cambiar el estado.
        /// </summary>
        private void OcultarMenu()
        {
            if (_panelMenu != null)
                _panelMenu.SetActive(false);
        }

        /// <summary>
        /// Oculta el menú y vuelve al modo Selección.
        /// </summary>
        private void CerrarMenu()
        {
            OcultarMenu();
            LimpiarResaltado();
            ModoActual = ModoInteraccion.Seleccion;
            UnidadSeleccionada = null;
        }

        // ── Resaltado de celdas ────────────────────────────────────────────────
        /// <summary>
        /// Colorea temporalmente las celdas a las que la unidad puede moverse.
        /// </summary>
        private void ResaltarCeldasMovimiento()
        {
            LimpiarResaltado();

            if (UnidadSeleccionada == null || GridManager.Instancia == null) return;

            int rango = UnidadSeleccionada.RangoMovimiento;
            var coord = UnidadSeleccionada.Coordenada;

            // Recorrer todas las celdas dentro del rango Manhattan.
            for (int dx = -rango; dx <= rango; dx++)
            {
                for (int dy = -rango; dy <= rango; dy++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) > rango) continue;
                    if (dx == 0 && dy == 0) continue;

                    int col = coord.x + dx;
                    int fila = coord.y + dy;

                    if (GridManager.Instancia.EstaCeldaLibre(col, fila))
                    {
                        var celda = GridManager.Instancia.ObtenerCelda(col, fila);
                        if (celda?.Objeto != null)
                        {
                            var sr = celda.Objeto.GetComponent<SpriteRenderer>();
                            if (sr != null)
                            {
                                _coloresOriginales[sr] = sr.color;
                                sr.color = ColorResaltadoMover;
                                _celdasResaltadas.Add(sr);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Colorea temporalmente los enemigos que están en rango de ataque.
        /// </summary>
        private void ResaltarEnemigosEnRango()
        {
            LimpiarResaltado();

            if (UnidadSeleccionada == null || TurnManager.Instancia == null) return;

            foreach (var enemigo in TurnManager.Instancia.UnidadesEnemigo)
            {
                if (enemigo == null) continue;
                if (CombatSystem.Instancia != null &&
                    CombatSystem.Instancia.EstaEnRangoDeAtaque(UnidadSeleccionada, enemigo))
                {
                    var sr = enemigo.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        _coloresOriginales[sr] = sr.color;
                        sr.color = ColorResaltadoAtacar;
                        _celdasResaltadas.Add(sr);
                    }
                }
            }
        }

        /// <summary>
        /// Restaura los colores originales de todas las celdas resaltadas.
        /// </summary>
        private void LimpiarResaltado()
        {
            foreach (var sr in _celdasResaltadas)
            {
                if (sr != null && _coloresOriginales.ContainsKey(sr))
                    sr.color = _coloresOriginales[sr];
            }
            _celdasResaltadas.Clear();
            _coloresOriginales.Clear();
        }

        /// <summary>
        /// Verifica si hay al menos un enemigo en rango de ataque.
        /// </summary>
        private bool ExistenEnemigosEnRango()
        {
            if (UnidadSeleccionada == null || TurnManager.Instancia == null ||
                CombatSystem.Instancia == null) return false;

            foreach (var enemigo in TurnManager.Instancia.UnidadesEnemigo)
            {
                if (enemigo == null) continue;
                if (CombatSystem.Instancia.EstaEnRangoDeAtaque(UnidadSeleccionada, enemigo))
                    return true;
            }
            return false;
        }

        // ── Creación automática del Canvas ─────────────────────────────────────
        /// <summary>
        /// Crea un Canvas "Screen Space - Overlay" si no se asignó uno manualmente.
        /// </summary>
        private void CrearCanvasAutomatico()
        {
            var canvasObj = new GameObject("ActionMenu_Canvas");
            canvasObj.transform.SetParent(transform);

            CanvasPrincipal = canvasObj.AddComponent<Canvas>();
            CanvasPrincipal.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasPrincipal.sortingOrder = 100;

            // CanvasScaler para que la UI se vea bien en diferentes resoluciones.
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // GraphicRaycaster necesario para que los botones funcionen.
            canvasObj.AddComponent<GraphicRaycaster>();

            // EventSystem necesario para detectar clics en UI.
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            Debug.Log("[ActionMenu] Canvas y EventSystem creados automáticamente.");
        }

        /// <summary>
        /// Crea los botones del menú de acciones por código.
        /// </summary>
        private void CrearMenuUI()
        {
            if (CanvasPrincipal == null) return;

            // ── Panel contenedor ──────────────────────────────────────────────
            _panelMenu = new GameObject("Panel_ActionMenu");
            _panelMenu.transform.SetParent(CanvasPrincipal.transform, false);

            var panelImg = _panelMenu.AddComponent<Image>();
            panelImg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f); // Fondo oscuro semi-transparente.

            var rectPanel = _panelMenu.GetComponent<RectTransform>();
            float alturaTotal = (AltoBoton * 3) + (EspaciadoBoton * 4);
            rectPanel.sizeDelta = new Vector2(AnchoBoton + 20f, alturaTotal);
            rectPanel.pivot = new Vector2(0f, 0.5f); // Pivot a la izquierda-centro.

            // Layout vertical automático.
            var layout = _panelMenu.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, (int)EspaciadoBoton, (int)EspaciadoBoton);
            layout.spacing = EspaciadoBoton;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // ── Botón MOVER ──────────────────────────────────────────────────
            _botonMover = CrearBoton("Btn_Mover", "⬛ Mover", ColorBotonMover, OnBotonMover);

            // ── Botón ATACAR ─────────────────────────────────────────────────
            _botonAtacar = CrearBoton("Btn_Atacar", "⚔️ Atacar", ColorBotonAtacar, OnBotonAtacar);

            // ── Botón ESPERAR ────────────────────────────────────────────────
            _botonEsperar = CrearBoton("Btn_Esperar", "⏳ Esperar", ColorBotonEsperar, OnBotonEsperar);
        }

        /// <summary>
        /// Helper: crea un botón UI con texto y callback asignado.
        /// </summary>
        private Button CrearBoton(string nombre, string texto, Color colorFondo,
                                  UnityEngine.Events.UnityAction callback)
        {
            var btnObj = new GameObject(nombre);
            btnObj.transform.SetParent(_panelMenu.transform, false);

            // Imagen de fondo del botón.
            var imgBtn = btnObj.AddComponent<Image>();
            imgBtn.color = colorFondo;

            // Componente Button.
            var btn = btnObj.AddComponent<Button>();
            var colores = btn.colors;
            colores.highlightedColor = colorFondo * 1.2f;
            colores.pressedColor     = colorFondo * 0.8f;
            btn.colors = colores;
            btn.onClick.AddListener(callback);

            // RectTransform con altura fija.
            var rectBtn = btnObj.GetComponent<RectTransform>();
            rectBtn.sizeDelta = new Vector2(AnchoBoton, AltoBoton);

            // Texto del botón.
            var txtObj = new GameObject("Texto");
            txtObj.transform.SetParent(btnObj.transform, false);

            var txtComponent = txtObj.AddComponent<Text>();
            txtComponent.text = texto;
            txtComponent.color = ColorTexto;
            txtComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txtComponent.fontSize = 16;
            txtComponent.alignment = TextAnchor.MiddleCenter;
            txtComponent.fontStyle = FontStyle.Bold;

            // Estirar el texto para que ocupe todo el botón.
            var rectTxt = txtObj.GetComponent<RectTransform>();
            rectTxt.anchorMin = Vector2.zero;
            rectTxt.anchorMax = Vector2.one;
            rectTxt.offsetMin = Vector2.zero;
            rectTxt.offsetMax = Vector2.zero;

            return btn;
        }

        // ── Clic derecho / Escape para cancelar ───────────────────────────────
        private void LateUpdate()
        {
            // Permitir cancelar cualquier modo con clic derecho o Escape.
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                if (ModoActual != ModoInteraccion.Seleccion)
                {
                    Debug.Log("[ActionMenu] Acción cancelada.");
                    CerrarMenu();
                }
            }
        }
    }
}
