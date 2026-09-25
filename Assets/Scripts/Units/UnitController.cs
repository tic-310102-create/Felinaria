// ============================================================
//  UnitController.cs
//  Felinaria: El último presagio
//  Fase 3 – IA Enemiga y Mapa (actualizado)
//
//  RESPONSABILIDAD:
//    - Representa a cualquier unidad en el tablero (héroe o enemigo).
//    - Lee sus estadísticas desde un ScriptableObject UnitStats (Fase 2).
//    - Mueve a la unidad entre celdas con interpolación suave (sin teletransporte).
//    - Comunica su posición al GridManager para actualizar el mapa de ocupación.
//    - Respeta el turno activo (pregunta al TurnManager antes de actuar).
//    - Expone API para CombatSystem y ActionMenu.
//
//  CAMBIOS FASE 2:
//    - Se lee de UnitStats (ScriptableObject) en vez de valores hardcodeados.
//    - Se expone MarcarComoUsadaPublico() para CombatSystem.
//    - Se agregan propiedades de rango de ataque.
//    - Se agrega Collider2D automático para detección de clics.
// ============================================================

using System.Collections;
using UnityEngine;
using Felinaria.Grid;
using Felinaria.Managers;
using Felinaria.Data;

namespace Felinaria.Units
{
    // ─── Enum: bando al que pertenece la unidad ──────────────────────────────
    public enum Bando
    {
        Jugador,
        Enemigo
    }

    // ─── UnitController ──────────────────────────────────────────────────────
    /// <summary>
    /// Script que va en cada unidad (héroe o enemigo) del tablero.
    /// </summary>
    public class UnitController : MonoBehaviour
    {
        // ── Inspector: ScriptableObject de stats (Fase 2) ─────────────────────
        [Header("Ficha de Estadísticas (ScriptableObject)")]
        [Tooltip("Arrastra aquí el archivo UnitStats del personaje. " +
                 "Si está asignado, sobreescribe los valores manuales de abajo.")]
        public UnitStats FichaStats;

        // ── Inspector: Identidad ───────────────────────────────────────────────
        [Header("Identidad de la Unidad (manual si no hay FichaStats)")]
        [Tooltip("Nombre del personaje (p.ej. 'Sera', 'Guardia Enemigo').")]
        public string NombreUnidad = "Unidad";

        [Tooltip("¿A qué bando pertenece esta unidad?")]
        public Bando BandoUnidad = Bando.Jugador;

        // ── Inspector: Posición inicial ────────────────────────────────────────
        [Header("Posición inicial en la cuadrícula")]
        [Tooltip("Columna de inicio (eje X del tablero).")]
        public int ColInicial = 0;

        [Tooltip("Fila de inicio (eje Y del tablero).")]
        public int FilaInicial = 0;

        // ── Inspector: Estadísticas de combate (fallback manual) ──────────────
        [Header("Estadísticas de Combate (fallback si no hay FichaStats)")]
        [Tooltip("Puntos de Vida máximos.")]
        [Range(1, 999)]
        public int VidaMaxima = 30;

        [Tooltip("Puntos de Maná máximos para habilidades.")]
        [Range(0, 999)]
        public int ManaMaximo = 20;

        [Header("Habilidades Activas")]
        public System.Collections.Generic.List<Felinaria.Data.SkillData> Habilidades = new System.Collections.Generic.List<Felinaria.Data.SkillData>();

        [Tooltip("Puntos de ataque físico.")]
        [Range(0, 99)]
        public int Ataque = 8;

        [Tooltip("Puntos de defensa física.")]
        [Range(0, 99)]
        public int Defensa = 3;

        [Tooltip("Rango de movimiento en celdas por turno.")]
        [Range(1, 10)]
        public int RangoMovimiento = 3;

        [Tooltip("Rango mínimo de ataque (celdas Manhattan).")]
        [Range(1, 5)]
        public int RangoAtaqueMinimo = 1;

        [Tooltip("Rango máximo de ataque (celdas Manhattan).")]
        [Range(1, 5)]
        public int RangoAtaqueMaximo = 1;

        // ── Inspector: Movimiento ──────────────────────────────────────────────
        [Header("Configuración de Movimiento")]
        [Tooltip("Velocidad de desplazamiento visual entre celdas (unidades/segundo).")]
        [Range(1f, 20f)]
        public float VelocidadMovimiento = 6f;

        [Header("Representación Visual 2D")]
        [Tooltip("Referencia al SpriteRenderer asignado en el Inspector. Si está vacío, se busca automáticamente.")]
        public SpriteRenderer ComponenteSpriteRenderer;

        [Tooltip("Color del sprite cuando la unidad ya actuó este turno (atenuación).")]
        public Color ColorUsado = new Color(0.6f, 0.6f, 0.6f, 1f);

        [Tooltip("Color normal base del sprite (blanco #FFFFFF para respetar los píxeles del arte original).")]
        public Color ColorNormal = Color.white;

        // ── Estado interno ─────────────────────────────────────────────────────

        /// <summary>Vida actual de la unidad.</summary>
        public int VidaActual { get; private set; }

        /// <summary>Maná actual de la unidad.</summary>
        public int ManaActual { get; private set; }

        private System.Collections.Generic.Dictionary<string, int> _cooldowns = new System.Collections.Generic.Dictionary<string, int>();

        /// <summary>Coordenada actual en la cuadrícula.</summary>
        public Vector2Int Coordenada { get; private set; }

        /// <summary>True mientras la unidad está desplazándose visualmente.</summary>
        public bool EstaMoviendose { get; private set; }

        /// <summary>True si esta unidad ya actuó en el turno actual.</summary>
        public bool YaActuoEsteTurno { get; private set; }

        // Referencia al SpriteRenderer para cambiar colores.
        private SpriteRenderer _spriteRenderer;
        private Renderer _meshRenderer;

        // Coroutine activa de movimiento (para poder cancelarla si fuera necesario).
        private Coroutine _corrutinaMover;

        // ── Unity Lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            // Cargar stats desde ScriptableObject si está asignado.
            CargarDesdeScriptableObject();

            VidaActual = VidaMaxima;
            ManaActual = ManaMaximo;

            // Auto-asignar habilidades predeterminadas si no tiene ninguna
            if (Habilidades == null || Habilidades.Count == 0)
            {
                Habilidades = Felinaria.Combat.SkillSystem.ObtenerHabilidadesPredeterminadas();
            }

            // Asegurar componentes 2D puros respetando el sprite original
            AsegurarComponentes2D();

            // Auto-adjuntar HealthBar flotante si no existe
            if (GetComponent<Felinaria.UI.HealthBar>() == null)
            {
                gameObject.AddComponent<Felinaria.UI.HealthBar>();
            }
        }

        private void Start()
        {
            // Posicionar la unidad en su celda inicial al comenzar la escena.
            InicializarEnCuadricula();
        }

        // ── Carga desde ScriptableObject ──────────────────────────────────────
        /// <summary>
        /// Si FichaStats está asignada, sobreescribe los valores manuales
        /// con los del ScriptableObject. Esto permite a los diseñadores
        /// modificar stats sin tocar el Inspector de cada unidad individual.
        /// </summary>
        private void CargarDesdeScriptableObject()
        {
            if (FichaStats == null) return;

            NombreUnidad      = FichaStats.NombrePersonaje;
            VidaMaxima        = FichaStats.VidaMaxima;
            ManaMaximo        = FichaStats.ManaMaximo;
            Ataque            = FichaStats.Ataque;
            Defensa           = FichaStats.Defensa;
            RangoMovimiento   = FichaStats.RangoMovimiento;
            VelocidadMovimiento = FichaStats.VelocidadMovimiento;
            RangoAtaqueMinimo = FichaStats.RangoAtaqueMinimo;
            RangoAtaqueMaximo = FichaStats.RangoAtaqueMaximo;

            if (FichaStats.ColorGraybox != Color.white && FichaStats.ColorGraybox.a > 0.05f)
            {
                ColorNormal = FichaStats.ColorGraybox;
            }
            else
            {
                ColorNormal = Color.white;
            }
            ColorUsado        = FichaStats.ColorUsado;

            if (FichaStats.Habilidades != null && FichaStats.Habilidades.Count > 0)
            {
                Habilidades = new System.Collections.Generic.List<Felinaria.Data.SkillData>(FichaStats.Habilidades);
            }

            // Aplicar color base inmediatamente
            AplicarColorVisual(ColorNormal);

            Debug.Log($"[UnitController] Stats cargados desde ScriptableObject: '{FichaStats.NombrePersonaje}'");
        }

        /// <summary>
        /// Asegura que la unidad utilice componentes 2D puros (BoxCollider2D y SpriteRenderer)
        /// y respete el sprite asignado en el Inspector sin forzar sprites o colores planos.
        /// </summary>
        private void AsegurarComponentes2D()
        {
            // 1. Destruir cualquier colisionador 3D residual para evitar interferencias
            var colliders3D = GetComponents<Collider>();
            foreach (var col3D in colliders3D)
            {
                DestroyImmediate(col3D);
            }

            // 2. Destruir MeshFilter y MeshRenderer 3D residuales
            var meshFilters = GetComponents<MeshFilter>();
            foreach (var mf in meshFilters)
            {
                DestroyImmediate(mf);
            }
            var meshRenderers = GetComponents<MeshRenderer>();
            foreach (var mr in meshRenderers)
            {
                DestroyImmediate(mr);
            }

            // 3. Asegurar BoxCollider2D para detección de clics 2D
            var col2D = GetComponent<BoxCollider2D>();
            if (col2D == null)
            {
                col2D = gameObject.AddComponent<BoxCollider2D>();
            }
            if (col2D != null)
            {
                col2D.size = new Vector2(0.85f, 0.85f);
                col2D.isTrigger = false;
            }

            // 4. Vincular SpriteRenderer respetando el componente o sprite configurado en el Inspector
            if (ComponenteSpriteRenderer != null)
            {
                _spriteRenderer = ComponenteSpriteRenderer;
            }
            else
            {
                _spriteRenderer = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
                if (_spriteRenderer == null)
                {
                    _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                    _spriteRenderer.sortingLayerName = "Default";
                    _spriteRenderer.sortingOrder = 2; // Por encima del tablero
                }
            }

            // Si el SpriteRenderer no tiene ningún sprite asignado, usar el blanco de respaldo para Graybox
            if (_spriteRenderer != null && _spriteRenderer.sprite == null)
            {
                _spriteRenderer.sprite = GridManager.ObtenerSpriteBlanco();
            }

            // Respetar #FFFFFF como color base por defecto para no tapar los píxeles del sprite
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = ColorNormal;
            }

            // Garantizar plano Z = 0
            transform.position = new Vector3(transform.position.x, transform.position.y, 0f);
        }

        // ── Inicialización ─────────────────────────────────────────────────────
        /// <summary>
        /// Coloca la unidad en su celda de inicio y la registra en GridManager y TurnManager.
        /// Garantiza que el GridManager esté listo antes de registrar ocupación.
        /// </summary>
        private void InicializarEnCuadricula()
        {
            if (GridManager.Instancia == null)
            {
                // Fallback por si la instancia estática aún no se asignó
                var gridEnEscena = FindFirstObjectByType<GridManager>();
                if (gridEnEscena == null)
                {
                    Debug.LogError($"[UnitController] '{NombreUnidad}': No se encontró GridManager en la escena.");
                    return;
                }
            }

            // Asegurar que el GridManager haya generado las celdas antes de interactuar
            if (!GridManager.Instancia.EstaInicializado)
            {
                GridManager.Instancia.GenerarCuadricula();
            }

            // Validar que las coordenadas iniciales estén dentro de los límites del tablero
            if (!GridManager.Instancia.EsCoordenadaValida(ColInicial, FilaInicial))
            {
                Debug.LogWarning($"[UnitController] '{NombreUnidad}': Coordenada inicial ({ColInicial},{FilaInicial}) " +
                                 "fuera de los límites del tablero. Reubicando en (0,0).");
                ColInicial  = 0;
                FilaInicial = 0;
            }

            // Verificar si la celda inicial está libre; si no, buscar la primera disponible
            if (!GridManager.Instancia.EstaCeldaLibre(ColInicial, FilaInicial))
            {
                Debug.LogWarning($"[UnitController] '{NombreUnidad}': Celda ({ColInicial},{FilaInicial}) " +
                                 "ya está ocupada. Buscando celda libre más cercana...");
                Vector2Int celdaLibre = EncontrarCeldaLibreMasCercana(ColInicial, FilaInicial);
                ColInicial  = celdaLibre.x;
                FilaInicial = celdaLibre.y;
            }

            // Posicionar el transform en el centro de la celda en el mundo
            Coordenada = new Vector2Int(ColInicial, FilaInicial);
            transform.position = GridManager.Instancia.CoordenadaAMundo(Coordenada);

            // Registrar ocupación en el GridManager
            GridManager.Instancia.SetOcupacion(Coordenada.x, Coordenada.y, true);

            // Auto-registro en TurnManager si no está en la lista
            if (TurnManager.Instancia != null)
            {
                if (BandoUnidad == Bando.Jugador && !TurnManager.Instancia.UnidadesJugador.Contains(this))
                {
                    TurnManager.Instancia.RegistrarUnidad(this);
                }
                else if (BandoUnidad == Bando.Enemigo && !TurnManager.Instancia.UnidadesEnemigo.Contains(this))
                {
                    TurnManager.Instancia.RegistrarUnidad(this);
                }
            }

            Debug.Log($"[UnitController] '{NombreUnidad}' ({BandoUnidad}) inicializado correctamente en ({Coordenada.x},{Coordenada.y}).");
        }

        /// <summary>
        /// Busca la celda libre más cercana a la coordenada deseada.
        /// </summary>
        private Vector2Int EncontrarCeldaLibreMasCercana(int colOrig, int filaOrig)
        {
            if (GridManager.Instancia == null) return new Vector2Int(colOrig, filaOrig);

            for (int r = 0; r < Mathf.Max(GridManager.Instancia.Columnas, GridManager.Instancia.Filas); r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        int c = colOrig + dx;
                        int f = filaOrig + dy;
                        if (GridManager.Instancia.EsCoordenadaValida(c, f) && GridManager.Instancia.EstaCeldaLibre(c, f))
                        {
                            return new Vector2Int(c, f);
                        }
                    }
                }
            }
            return new Vector2Int(colOrig, filaOrig);
        }

        // ── Movimiento ─────────────────────────────────────────────────────────
        /// <summary>
        /// Solicita mover la unidad a las coordenadas dadas.
        /// Valida turno, rango, límites y disponibilidad de celda antes de moverse.
        /// </summary>
        /// <param name="col">Columna destino.</param>
        /// <param name="fila">Fila destino.</param>
        /// <returns>True si el movimiento fue aceptado.</returns>
        public bool MoverACelda(int col, int fila)
        {
            // ── Validación 1: ¿Es el turno correcto para este bando? ───────────
            if (TurnManager.Instancia != null && !TurnManager.Instancia.EsTurnoDeUnidad(this))
            {
                Debug.Log($"[UnitController] '{NombreUnidad}': Movimiento bloqueado, no es tu turno.");
                return false;
            }

            // ── Validación 2: ¿Ya actuó esta unidad? ──────────────────────────
            if (YaActuoEsteTurno)
            {
                Debug.Log($"[UnitController] '{NombreUnidad}': Ya actuó este turno.");
                return false;
            }

            // ── Validación 3: ¿Está en movimiento? ────────────────────────────
            if (EstaMoviendose)
            {
                Debug.Log($"[UnitController] '{NombreUnidad}': Todavía en movimiento.");
                return false;
            }

            // ── Validación 4: ¿La celda destino existe? ───────────────────────
            if (!GridManager.Instancia.EsCoordenadaValida(col, fila))
            {
                Debug.Log($"[UnitController] '{NombreUnidad}': Destino ({col},{fila}) fuera del tablero.");
                return false;
            }

            // ── Validación 5: ¿La celda destino es transitable y está libre? ─────────
            if (!GridManager.Instancia.EsCeldaTransitable(col, fila))
            {
                Debug.Log($"[UnitController] '{NombreUnidad}': Celda ({col},{fila}) es intransitable (Obstáculo/Agua).");
                return false;
            }

            if (!GridManager.Instancia.EstaCeldaLibre(col, fila))
            {
                Debug.Log($"[UnitController] '{NombreUnidad}': Celda ({col},{fila}) está ocupada.");
                return false;
            }

            // ── Validación 6: ¿Hay una ruta navegable dentro del rango de movimiento? ──
            var ruta = Felinaria.AI.Pathfinding.BuscarRutaConRango(Coordenada, new Vector2Int(col, fila), RangoMovimiento);
            if (ruta.Count == 0 || ruta[ruta.Count - 1] != new Vector2Int(col, fila))
            {
                Debug.Log($"[UnitController] '{NombreUnidad}': No se puede alcanzar ({col},{fila}) con rango {RangoMovimiento} considerando obstáculos y costos de terreno.");
                return false;
            }

            // ── Todo OK: iniciar movimiento ────────────────────────────────────
            if (Felinaria.UI.ActionMenu.InstanciaExiste)
            {
                Felinaria.UI.ActionMenu.Instancia.CerrarMenu();
            }

            _corrutinaMover = StartCoroutine(DesplazarPorRuta(ruta));
            return true;
        }

        /// <summary>
        /// Coroutine que desplaza suavemente la unidad paso a paso por la ruta
        /// calculada por Pathfinding respetando obstáculos y terrenos.
        /// </summary>
        private IEnumerator DesplazarPorRuta(System.Collections.Generic.List<Vector2Int> ruta)
        {
            EstaMoviendose = true;

            // Liberar la celda actual en el GridManager.
            GridManager.Instancia.SetOcupacion(Coordenada.x, Coordenada.y, false);

            Vector2Int destinoFinal = ruta[ruta.Count - 1];

            // Desplazarse secuencialmente por cada celda de la ruta
            foreach (var paso in ruta)
            {
                Vector3 posPaso = GridManager.Instancia.CoordenadaAMundo(paso.x, paso.y);
                while (Vector3.Distance(transform.position, posPaso) > 0.001f)
                {
                    transform.position = Vector3.MoveTowards(
                        transform.position,
                        posPaso,
                        VelocidadMovimiento * Time.deltaTime
                    );
                    yield return null;
                }
                transform.position = posPaso;
                Coordenada = paso;
            }

            // Snappear al centro exacto de la celda de destino
            Vector3 posFinal = GridManager.Instancia.CoordenadaAMundo(destinoFinal);
            transform.position = posFinal;
            ColInicial = destinoFinal.x;
            FilaInicial = destinoFinal.y;
            Coordenada = destinoFinal;
            GridManager.Instancia.SetOcupacion(destinoFinal.x, destinoFinal.y, true);

            EstaMoviendose = false;
            MarcarComoUsada();

            Debug.Log($"[UnitController] '{NombreUnidad}' llegó a ({destinoFinal.x},{destinoFinal.y}).");
        }

        // ── Estado de turno ────────────────────────────────────────────────────
        /// <summary>
        /// Aplica un color al sprite 2D o material 3D de la unidad.
        /// </summary>
        private void AplicarColorVisual(Color c)
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = c;
            }
            else if (_meshRenderer != null && _meshRenderer.material != null)
            {
                _meshRenderer.material.color = c;
            }
        }

        /// <summary>
        /// Marca la unidad como "ya actuó" y cambia su color visual.
        /// Llamado automáticamente al terminar de moverse.
        /// </summary>
        private void MarcarComoUsada()
        {
            YaActuoEsteTurno = true;
            AplicarColorVisual(ColorUsado);
        }

        /// <summary>
        /// Versión pública de MarcarComoUsada().
        /// Llamada por CombatSystem al atacar o por ActionMenu al "Esperar".
        /// </summary>
        public void MarcarComoUsadaPublico()
        {
            MarcarComoUsada();
        }

        /// <summary>
        /// Obtiene los turnos restantes de cooldown para una habilidad dada.
        /// </summary>
        public int ObtenerCooldown(Felinaria.Data.SkillData skill)
        {
            if (skill == null || string.IsNullOrEmpty(skill.IdHabilidad)) return 0;
            return _cooldowns.ContainsKey(skill.IdHabilidad) ? _cooldowns[skill.IdHabilidad] : 0;
        }

        /// <summary>
        /// Valida si la unidad tiene suficiente maná y no está en cooldown para usar la habilidad.
        /// </summary>
        public bool PuedeUsarSkill(Felinaria.Data.SkillData skill)
        {
            if (skill == null) return false;
            return ManaActual >= skill.CostoMana && ObtenerCooldown(skill) == 0;
        }

        /// <summary>
        /// Descuenta maná de la unidad y actualiza la barra visual.
        /// </summary>
        public void ConsumirMana(int cantidad)
        {
            ManaActual = Mathf.Max(0, ManaActual - cantidad);
            var hb = GetComponent<Felinaria.UI.HealthBar>();
            if (hb != null) hb.ActualizarBarra();
        }

        /// <summary>
        /// Recupera maná hasta el máximo.
        /// </summary>
        public void RecuperarMana(int cantidad)
        {
            ManaActual = Mathf.Min(ManaMaximo, ManaActual + cantidad);
            var hb = GetComponent<Felinaria.UI.HealthBar>();
            if (hb != null) hb.ActualizarBarra();
        }

        /// <summary>
        /// Inicia el contador de cooldown para una habilidad.
        /// </summary>
        public void IniciarCooldown(Felinaria.Data.SkillData skill)
        {
            if (skill != null && skill.CooldownMaximo > 0)
            {
                _cooldowns[skill.IdHabilidad] = skill.CooldownMaximo;
            }
        }

        /// <summary>
        /// Reduce en 1 los cooldowns activos al inicio de cada nuevo turno.
        /// </summary>
        public void ReducirCooldowns()
        {
            var llaves = new System.Collections.Generic.List<string>(_cooldowns.Keys);
            foreach (var k in llaves)
            {
                if (_cooldowns[k] > 0)
                {
                    _cooldowns[k]--;
                }
            }
        }

        /// <summary>
        /// Reinicia el estado de la unidad al comienzo de un nuevo turno y decrementa cooldowns.
        /// </summary>
        public void ReiniciarTurno()
        {
            YaActuoEsteTurno = false;
            AplicarColorVisual(ColorNormal);
            ReducirCooldowns();
        }

        /// <summary>
        /// Restaura puntos de vida y actualiza la interfaz visual.
        /// </summary>
        public void Curar(int cantidad)
        {
            if (VidaActual <= 0) return;
            VidaActual = Mathf.Min(VidaMaxima, VidaActual + cantidad);
            Debug.Log($"[UnitController] '{NombreUnidad}' se curó {cantidad} HP. Vida: {VidaActual}/{VidaMaxima}");

            var healthBar = GetComponent<Felinaria.UI.HealthBar>();
            if (healthBar != null)
            {
                healthBar.ActualizarBarra();
            }
        }

        /// <summary>
        /// Restaura el estado guardado de la unidad (coordenadas lógicas, posición mundo, vida, maná, turno y visuales).
        /// </summary>
        public void RestaurarEstado(int col, int fila, int vida, int vidaMax, int mana, int manaMax, bool yaActuo, bool estaViva)
        {
            if (vidaMax > 0) VidaMaxima = vidaMax;
            VidaActual = Mathf.Clamp(vida, 0, VidaMaxima);

            if (manaMax > 0) ManaMaximo = manaMax;
            ManaActual = Mathf.Clamp(mana, 0, ManaMaximo);

            ColInicial = col;
            FilaInicial = fila;
            Coordenada = new Vector2Int(col, fila);
            YaActuoEsteTurno = yaActuo;

            // Actualizar posición física en el mundo
            if (GridManager.Instancia != null)
            {
                transform.position = GridManager.Instancia.CoordenadaAMundo(col, fila);
            }
            else
            {
                transform.position = new Vector3(col, fila, 0f);
            }

            AplicarColorVisual(yaActuo ? ColorUsado : ColorNormal);

            // Actualizar barra de vida y maná
            var healthBar = GetComponent<Felinaria.UI.HealthBar>();
            if (healthBar != null)
            {
                healthBar.ActualizarVida(VidaActual, VidaMaxima);
                healthBar.ActualizarMana(ManaActual, ManaMaximo);
                healthBar.ActualizarBarra();
            }

            if (!estaViva || VidaActual <= 0)
            {
                gameObject.SetActive(false);
            }
            else
            {
                gameObject.SetActive(true);
            }

            Debug.Log($"[UnitController] '{NombreUnidad}' restaurado: Pos=({col},{fila}), HP={VidaActual}/{VidaMaxima}, MP={ManaActual}/{ManaMaximo}, Actuo={yaActuo}");
        }

        public void RestaurarEstado(int col, int fila, int vida, int vidaMax, bool yaActuo, bool estaViva)
        {
            RestaurarEstado(col, fila, vida, vidaMax, ManaMaximo, ManaMaximo, yaActuo, estaViva);
        }

        // ── Combate ────────────────────────────────────────────────────────────
        /// <summary>
        /// Aplica daño a la unidad. Si la vida llega a 0, la elimina.
        /// </summary>
        public void RecibirDanio(int cantidad)
        {
            VidaActual = Mathf.Max(0, VidaActual - cantidad);
            Debug.Log($"[UnitController] '{NombreUnidad}' recibió {cantidad} de daño. Vida: {VidaActual}/{VidaMaxima}");

            var hb = GetComponent<Felinaria.UI.HealthBar>();
            if (hb != null)
            {
                hb.ActualizarBarra();
            }

            if (VidaActual <= 0)
            {
                Morir();
            }
        }

        /// <summary>
        /// Maneja la eliminación de la unidad del tablero con retardo visual para animar barra de vida a 0 y estado de caída.
        /// </summary>
        private void Morir()
        {
            Debug.Log($"[UnitController] '{NombreUnidad}' ha sido derrotado.");
            if (GridManager.Instancia != null)
            {
                GridManager.Instancia.SetOcupacion(Coordenada.x, Coordenada.y, false);
            }

            // Deshabilitar colisionador para no interferir con otros clics
            var col2D = GetComponent<Collider2D>();
            if (col2D != null) col2D.enabled = false;

            // Asegurar que la barra de vida actualice inmediatamente su objetivo a 0
            var healthBar = GetComponent<Felinaria.UI.HealthBar>();
            if (healthBar != null)
            {
                healthBar.ActualizarBarra();
            }

            // Notificar al BattleManager para comenzar evaluación de fin de batalla
            if (Felinaria.Managers.BattleManager.Instancia != null)
            {
                Felinaria.Managers.BattleManager.Instancia.VerificarCondicionesFinDeBatalla();
            }

            StartCoroutine(RutinaMuerte());
        }

        private IEnumerator RutinaMuerte()
        {
            // Efecto visual de caída/derrota: oscurecer y desvanecer ligeramente
            if (_spriteRenderer != null)
            {
                Color c = _spriteRenderer.color;
                _spriteRenderer.color = new Color(c.r * 0.4f, c.g * 0.4f, c.b * 0.4f, 0.6f);
            }

            // Esperar retardo suficiente para que la barra de vida se vacíe a 0 visualmente
            yield return new WaitForSeconds(0.95f);

            Destroy(gameObject);
        }

        // ── API para IA (Fase 3) ───────────────────────────────────────────────
        /// <summary>
        /// Actualiza la coordenada lógica de la unidad directamente.
        /// SOLO para uso de EnemyAI, que mueve las unidades celda a celda
        /// sin pasar por las validaciones de MoverACelda().
        /// </summary>
        public void SetCoordenadaDirecta(Vector2Int nuevaCoordenada)
        {
            Coordenada = nuevaCoordenada;
        }

        // ── Utilidades ─────────────────────────────────────────────────────────
        /// <summary>
        /// Distancia Manhattan = |Δcol| + |Δfila|. Usada en tableros sin diagonal.
        /// </summary>
        private int CalcularDistanciaManhattan(Vector2Int origen, Vector2Int destino)
            => Mathf.Abs(destino.x - origen.x) + Mathf.Abs(destino.y - origen.y);

        // ── Depuración ─────────────────────────────────────────────────────────
        private void OnDrawGizmos()
        {
            // Dibuja un pequeño cuadrado de color sobre la unidad en el editor.
            Gizmos.color = (BandoUnidad == Bando.Jugador)
                ? new Color(0.2f, 0.6f, 1f, 0.8f)
                : new Color(1f, 0.2f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}
