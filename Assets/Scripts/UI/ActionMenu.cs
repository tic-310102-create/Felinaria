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
        private static ActionMenu _instancia;
        /// <summary>Acceso global al ActionMenu con auto-instanciación segura.</summary>
        public static ActionMenu Instancia
        {
            get
            {
                if (_instancia == null)
                {
                    _instancia = FindFirstObjectByType<ActionMenu>();
                    if (_instancia == null)
                    {
                        var go = new GameObject("ActionMenu_Auto");
                        _instancia = go.AddComponent<ActionMenu>();
                    }
                }
                return _instancia;
            }
            private set => _instancia = value;
        }

        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Referencia al Canvas")]
        [Tooltip("Canvas principal de la UI (Screen Space - Overlay). " +
                 "Si está vacío, se creará automáticamente.")]
        public Canvas CanvasPrincipal;

        [Header("Configuración Visual del Menú")]
        [Tooltip("Desplazamiento del menú respecto a la unidad seleccionada (en píxeles).")]
        public Vector2 OffsetMenu = new Vector2(100f, 0f);

        [Tooltip("Ancho de cada botón del menú.")]
        public float AnchoBoton = 180f;

        [Tooltip("Alto de cada botón del menú.")]
        public float AltoBoton = 50f;

        [Tooltip("Espacio entre botones.")]
        public float EspaciadoBoton = 8f;

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
            if (_instancia != null && _instancia != this)
            {
                Debug.LogWarning("[ActionMenu] Ya existe una instancia. Destruyendo duplicado.");
                Destroy(gameObject);
                return;
            }
            _instancia = this;
        }

        private void Start()
        {
            _camaraPrincipal = Camera.main ?? FindFirstObjectByType<Camera>();

            // Auto-instanciar SaveLoadUI como respaldo para asegurar que el panel de guardado esté activo
            _ = SaveLoadUI.Instancia;

            // Buscar Canvas existente en la escena o crear uno automático
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
                if (CanvasPrincipal == null)
                    CrearCanvasAutomatico();
            }

            if (CanvasPrincipal != null)
            {
                var scaler = CanvasPrincipal.GetComponent<CanvasScaler>();
                if (scaler == null)
                    scaler = CanvasPrincipal.gameObject.AddComponent<CanvasScaler>();

                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

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

        // ── Procesamiento de Input 2D Puro ────────────────────────────────────
        /// <summary>
        /// Lee clics del mouse / toques en pantalla y actúa según el modo actual.
        /// Utiliza física 2D (Physics2D) y conversión de coordenadas directas de cuadrícula.
        /// </summary>
        private void ProcesarInput()
        {
            bool clicIzquierdo = Input.GetMouseButtonDown(0);
            bool clicDerecho   = Input.GetMouseButtonDown(1);

            if (!clicIzquierdo && !clicDerecho) return;

            // Ignorar si el clic fue sobre la UI (botones del menú o panel de guardado)
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            if (_camaraPrincipal == null)
                _camaraPrincipal = Camera.main ?? FindFirstObjectByType<Camera>();

            if (_camaraPrincipal == null) return;

            // Convertir posición del cursor a coordenadas de mundo 2D
            Vector3 mousePos = Input.mousePosition;
            mousePos.z = -_camaraPrincipal.transform.position.z;
            Vector3 world3D = _camaraPrincipal.ScreenToWorldPoint(mousePos);
            Vector2 world2D = new Vector2(world3D.x, world3D.y);

            // Atajo con Clic Secundario (Derecho): Mover directamente a la celda clickeada
            if (clicDerecho && UnidadSeleccionada != null)
            {
                Debug.Log($"[ActionMenu] Clic secundario detectado → Intento de movimiento directo para '{UnidadSeleccionada.NombreUnidad}'");
                IntentarMover2D(world2D);
                return;
            }

            if (!clicIzquierdo) return;

            switch (ModoActual)
            {
                case ModoInteraccion.Seleccion:
                    IntentarSeleccionarUnidad2D(world2D);
                    break;

                case ModoInteraccion.MenuVisible:
                    ManejarClicConMenuVisible2D(world2D);
                    break;

                case ModoInteraccion.EsperandoMover:
                    IntentarMover2D(world2D);
                    break;

                case ModoInteraccion.EsperandoAtacar:
                    IntentarAtacar2D(world2D);
                    break;
            }
        }

        /// <summary>
        /// Busca una unidad en la posición del mundo 2D mediante colliders 2D o coordenada de celda.
        /// </summary>
        private UnitController ObtenerUnidadEnPosicion2D(Vector2 worldPos)
        {
            // 1. Detección con física 2D (BoxCollider2D / CircleCollider2D)
            Collider2D[] colliders = Physics2D.OverlapCircleAll(worldPos, 0.45f);
            foreach (var col in colliders)
            {
                var unidad = col.GetComponentInParent<UnitController>() ?? col.GetComponent<UnitController>();
                if (unidad != null) return unidad;
            }

            // 2. Fallback por coordenada de cuadrícula lógica
            if (GridManager.Instancia != null)
            {
                Vector2Int coord = GridManager.Instancia.MundoACoordenada(new Vector3(worldPos.x, worldPos.y, 0f));
                if (GridManager.Instancia.EsCoordenadaValida(coord))
                {
                    var todas = FindObjectsByType<UnitController>(FindObjectsSortMode.None);
                    foreach (var u in todas)
                    {
                        if (u != null && u.Coordenada == coord && u.VidaActual > 0)
                            return u;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Maneja un clic en el tablero cuando el menú de acciones está visible.
        /// </summary>
        private void ManejarClicConMenuVisible2D(Vector2 worldPos)
        {
            // 1. ¿Clic sobre otra unidad?
            var u = ObtenerUnidadEnPosicion2D(worldPos);
            if (u != null)
            {
                if (u == UnidadSeleccionada)
                {
                    // Clic en la misma unidad seleccionada: mantener menú
                    return;
                }
                if (u.BandoUnidad == Bando.Jugador && !u.YaActuoEsteTurno)
                {
                    // Seleccionar otra unidad aliada
                    SeleccionarUnidad(u);
                    return;
                }
                if (u.BandoUnidad == Bando.Enemigo)
                {
                    // Clic en enemigo: intentar atacarlo
                    IntentarAtacar2D(worldPos);
                    return;
                }
            }

            // 2. ¿Clic en una celda del tablero? Intentar mover directamente (Atajo / Fallback)
            if (UnidadSeleccionada != null && GridManager.Instancia != null)
            {
                Vector2Int coordDestino = GridManager.Instancia.MundoACoordenada(new Vector3(worldPos.x, worldPos.y, 0f));
                if (GridManager.Instancia.EsCoordenadaValida(coordDestino))
                {
                    if (coordDestino != UnidadSeleccionada.Coordenada)
                    {
                        Debug.Log($"[ActionMenu] Clic directo 2D en celda ({coordDestino.x},{coordDestino.y}) → Moviendo directamente.");
                        IntentarMover2D(worldPos);
                        return;
                    }
                }
            }

            // Si clicó fuera, cerrar menú
            CerrarMenu();
        }

        /// <summary>
        /// Intenta seleccionar una unidad aliada en la posición del cursor 2D.
        /// </summary>
        private void IntentarSeleccionarUnidad2D(Vector2 worldPos)
        {
            UnitController unidad = ObtenerUnidadEnPosicion2D(worldPos);
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
        /// Selecciona una unidad, resalta sus celdas de movimiento y muestra el menú de acciones.
        /// </summary>
        private void SeleccionarUnidad(UnitController unidad)
        {
            UnidadSeleccionada = unidad;
            Debug.Log($"[ActionMenu] Unidad seleccionada: '{unidad.NombreUnidad}' en ({unidad.Coordenada.x},{unidad.Coordenada.y})");

            // Resaltar inmediatamente las celdas a las que puede desplazarse
            ResaltarCeldasMovimiento();

            MostrarMenu();
        }

        /// <summary>
        /// Intenta mover la unidad seleccionada a la celda 2D clickeada.
        /// </summary>
        private void IntentarMover2D(Vector2 worldPos)
        {
            if (UnidadSeleccionada == null || GridManager.Instancia == null)
            {
                CerrarMenu();
                return;
            }

            Vector2Int coordDestino = GridManager.Instancia.MundoACoordenada(new Vector3(worldPos.x, worldPos.y, 0f));

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
        /// Intenta atacar al enemigo seleccionado mediante detección 2D.
        /// </summary>
        private void IntentarAtacar2D(Vector2 worldPos)
        {
            if (UnidadSeleccionada == null)
            {
                CerrarMenu();
                return;
            }

            UnitController objetivo = ObtenerUnidadEnPosicion2D(worldPos);

            if (objetivo == null || objetivo.BandoUnidad != Bando.Enemigo)
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
        /// Lanza un rayo contra colliders de la escena o el plano del tablero
        /// y devuelve el punto de impacto en coordenadas mundo.
        /// </summary>
        private bool ObtenerPuntoImpactoTablero(Ray ray, out Vector3 puntoImpacto)
        {
            puntoImpacto = Vector3.zero;

            // 1. Intentar Raycast 3D contra colliders
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Collide))
            {
                puntoImpacto = hit.point;
                return true;
            }

            // 2. Fallback: intersectar con el plano del tablero
            if (GridManager.Instancia != null)
            {
                Plane planoTablero = new Plane(Vector3.forward, GridManager.Instancia.OrigenMundo);
                if (planoTablero.Raycast(ray, out float distancia))
                {
                    puntoImpacto = ray.GetPoint(distancia);
                    return true;
                }

                Plane planoInvertido = new Plane(-Vector3.forward, GridManager.Instancia.OrigenMundo);
                if (planoInvertido.Raycast(ray, out distancia))
                {
                    puntoImpacto = ray.GetPoint(distancia);
                    return true;
                }
            }

            return false;
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

        // ── Creación automática del Canvas y UI ────────────────────────────────
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

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            Debug.Log("[ActionMenu] Canvas y EventSystem creados automáticamente.");
        }

        /// <summary>
        /// Crea los botones del menú de acciones por código con estructura robusta.
        /// </summary>
        private void CrearMenuUI()
        {
            if (CanvasPrincipal == null) return;

            if (_panelMenu != null)
                Destroy(_panelMenu);

            // ── Panel contenedor ──────────────────────────────────────────────
            _panelMenu = new GameObject("Panel_ActionMenu");
            _panelMenu.transform.SetParent(CanvasPrincipal.transform, false);
            _panelMenu.transform.localScale = Vector3.one;

            var panelImg = _panelMenu.AddComponent<Image>();
            panelImg.color = new Color(0.10f, 0.10f, 0.16f, 0.96f); // Fondo oscuro elegante y nítido

            var rectPanel = _panelMenu.GetComponent<RectTransform>();
            rectPanel.pivot = new Vector2(0f, 0.5f);
            rectPanel.sizeDelta = new Vector2(AnchoBoton + 28f, (AltoBoton * 3) + (EspaciadoBoton * 4) + 24f);

            // Layout vertical automático
            var layout = _panelMenu.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = EspaciadoBoton;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = _panelMenu.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // ── Botón MOVER ──────────────────────────────────────────────────
            _botonMover = CrearBoton("Btn_Mover", "Mover", ColorBotonMover, OnBotonMover);

            // ── Botón ATACAR ─────────────────────────────────────────────────
            _botonAtacar = CrearBoton("Btn_Atacar", "Atacar", ColorBotonAtacar, OnBotonAtacar);

            // ── Botón ESPERAR ────────────────────────────────────────────────
            _botonEsperar = CrearBoton("Btn_Esperar", "Esperar", ColorBotonEsperar, OnBotonEsperar);
        }

        /// <summary>
        /// Helper: crea un botón UI con texto y callback asignado de forma robusta y nítida.
        /// </summary>
        private Button CrearBoton(string nombre, string texto, Color colorFondo,
                                  UnityEngine.Events.UnityAction callback)
        {
            var btnObj = new GameObject(nombre);
            btnObj.transform.SetParent(_panelMenu.transform, false);
            btnObj.transform.localScale = Vector3.one;

            // LayoutElement para que el VerticalLayoutGroup no colapse la altura del botón
            var layoutElement = btnObj.AddComponent<LayoutElement>();
            layoutElement.minWidth = AnchoBoton;
            layoutElement.preferredWidth = AnchoBoton;
            layoutElement.minHeight = AltoBoton;
            layoutElement.preferredHeight = AltoBoton;
            layoutElement.flexibleWidth = 1f;
            layoutElement.flexibleHeight = 0f;

            // Imagen de fondo del botón
            var imgBtn = btnObj.AddComponent<Image>();
            imgBtn.color = colorFondo;

            // Componente Button con estados visuales interactivos
            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = imgBtn;
            var colores = btn.colors;
            colores.normalColor      = colorFondo;
            colores.highlightedColor = Color.Lerp(colorFondo, Color.white, 0.35f);
            colores.pressedColor     = Color.Lerp(colorFondo, Color.black, 0.35f);
            colores.selectedColor    = colorFondo;
            colores.disabledColor    = new Color(colorFondo.r * 0.4f, colorFondo.g * 0.4f, colorFondo.b * 0.4f, 0.5f);
            btn.colors = colores;
            btn.onClick.AddListener(callback);

            // RectTransform
            var rectBtn = btnObj.GetComponent<RectTransform>();
            rectBtn.sizeDelta = new Vector2(AnchoBoton, AltoBoton);

            // Texto del botón
            var txtObj = new GameObject("Texto");
            txtObj.transform.SetParent(btnObj.transform, false);
            txtObj.transform.localScale = Vector3.one;

            var txtComponent = txtObj.AddComponent<Text>();
            txtComponent.text = texto;
            txtComponent.color = ColorTexto;
            txtComponent.font = ObtenerFuenteSegura();
            txtComponent.fontSize = 18;
            txtComponent.alignment = TextAnchor.MiddleCenter;
            txtComponent.fontStyle = FontStyle.Bold;
            txtComponent.raycastTarget = false; // No bloquea los clics al botón
            txtComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
            txtComponent.verticalOverflow = VerticalWrapMode.Overflow;

            // RectTransform del texto ajustado al botón
            var rectTxt = txtObj.GetComponent<RectTransform>();
            rectTxt.anchorMin = Vector2.zero;
            rectTxt.anchorMax = Vector2.one;
            rectTxt.offsetMin = new Vector2(8f, 4f);
            rectTxt.offsetMax = new Vector2(-8f, -4f);

            return btn;
        }

        /// <summary>
        /// Helper para obtener una fuente legible compatible con cualquier versión de Unity.
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
                    fuente = Font.CreateDynamicFontFromOSFont("Arial", 15);
                }
                catch { }
            }
            return fuente;
        }

        // ── Escape para cancelar ───────────────────────────────────────────────
        private void LateUpdate()
        {
            // Permitir cancelar cualquier modo con Escape
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (ModoActual != ModoInteraccion.Seleccion)
                {
                    Debug.Log("[ActionMenu] Acción cancelada con Escape.");
                    CerrarMenu();
                }
            }
        }
    }
}
