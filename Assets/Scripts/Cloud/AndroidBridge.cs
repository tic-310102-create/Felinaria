// ============================================================
//  AndroidBridge.cs
//  Felinaria: El último presagio
//  Fase 4 – Persistencia y Pulido
//
//  RESPONSABILIDAD:
//    - Stub (simulador) de la futura conexión con Firebase Firestore.
//    - Simula el envío del JSON de guardado a la nube con un retardo.
//    - Simula la descarga de datos desde la nube.
//    - Cuando se integre Firebase real, solo hay que reemplazar
//      los Debug.Log por llamadas reales al SDK de Firebase.
//
//  ¿POR QUÉ UN STUB? (para principiantes):
//    Todavía no tenemos Firebase configurado, pero queremos que
//    el código esté listo para conectarse. Un "stub" es un simulador
//    que imita el comportamiento final con mensajes de consola.
//    Cuando implementes Firebase, solo cambias el contenido de
//    SincronizarConNube() sin tocar ningún otro script.
// ============================================================

using System;
using System.Collections;
using UnityEngine;
using Felinaria.Data;

namespace Felinaria.Cloud
{
    /// <summary>
    /// Stub de comunicación con la nube (Firebase Firestore).
    /// Singleton que se coloca en el GameObject "GameMaster".
    ///
    /// Uso futuro con Firebase:
    ///   1. Instalar Firebase SDK para Unity.
    ///   2. Reemplazar los métodos stub por llamadas reales a Firestore.
    ///   3. El resto del juego no necesita cambios.
    /// </summary>
    public class AndroidBridge : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        private static AndroidBridge _instancia;
        private static bool _aplicacionCerrando = false;

        /// <summary>Indica si existe una instancia activa sin forzar su creación.</summary>
        public static bool InstanciaExiste => _instancia != null && !_aplicacionCerrando;

        public static AndroidBridge Instancia
        {
            get
            {
                if (_aplicacionCerrando) return null;
                if (_instancia == null)
                {
                    _instancia = FindFirstObjectByType<AndroidBridge>();
                    if (_instancia == null && Application.isPlaying)
                    {
                        var go = new GameObject("AndroidBridge_Auto");
                        _instancia = go.AddComponent<AndroidBridge>();
                    }
                }
                return _instancia;
            }
            private set => _instancia = value;
        }

        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Configuración de Sincronización")]
        [Tooltip("Tiempo simulado que tarda la sincronización con la nube (segundos).")]
        [Range(0.5f, 5f)]
        public float TiempoSimuladoSync = 2f;

        [Tooltip("URL base del endpoint de Firebase (stub, no se usa aún).")]
        public string FirebaseURL = "https://felinaria-default-rtdb.firebaseio.com/";

        [Tooltip("Colección de Firestore donde se guardan las partidas (stub).")]
        public string ColeccionFirestore = "partidas_guardadas";

        // ── Estado ─────────────────────────────────────────────────────────────
        /// <summary>True mientras se está sincronizando con la nube.</summary>
        public bool EstaSincronizando { get; private set; }

        /// <summary>Última fecha/hora de sincronización exitosa.</summary>
        public string UltimaSincronizacion { get; private set; } = "Nunca";

        // ── Eventos ────────────────────────────────────────────────────────────
        /// <summary>Se dispara cuando la sincronización termina.
        /// Bool = true si fue exitosa.</summary>
        public event System.Action<bool> OnSincronizacionCompletada;

        // ── Unity Lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            _aplicacionCerrando = false;
            if (_instancia != null && _instancia != this)
            {
                Debug.LogWarning("[AndroidBridge] Ya existe una instancia. Destruyendo duplicado.");
                Destroy(gameObject);
                return;
            }
            _instancia = this;
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

        // ── API pública ────────────────────────────────────────────────────────

        /// <summary>
        /// Simula el envío del JSON de guardado local a Firebase Firestore.
        /// En la implementación real, usará el SDK de Firebase.
        ///
        /// Flujo futuro real:
        ///   1. Obtener el JSON con SaveSystem.ObtenerJSONGuardado().
        ///   2. Conectar con FirebaseFirestore.DefaultInstance.
        ///   3. Subir a la colección "partidas_guardadas" con el IdJugador como documento.
        ///   4. Manejar errores de red.
        /// </summary>
        public void SincronizarConNube()
        {
            if (EstaSincronizando)
            {
                Debug.LogWarning("[AndroidBridge] Ya hay una sincronización en progreso.");
                return;
            }

            StartCoroutine(SimularSincronizacion());
        }

        /// <summary>
        /// Simula la descarga de datos desde Firebase Firestore.
        /// </summary>
        public void DescargarDesdeNube()
        {
            if (EstaSincronizando)
            {
                Debug.LogWarning("[AndroidBridge] Ya hay una sincronización en progreso.");
                return;
            }

            StartCoroutine(SimularDescarga());
        }

        /// <summary>
        /// Combo: guarda localmente y luego sincroniza con la nube.
        /// </summary>
        public void GuardarYSincronizar()
        {
            bool guardadoExitoso = SaveSystem.GuardarPartida();

            if (guardadoExitoso)
            {
                SincronizarConNube();
            }
            else
            {
                Debug.LogError("[AndroidBridge] No se pudo guardar localmente. Sincronización cancelada.");
                OnSincronizacionCompletada?.Invoke(false);
            }
        }

        // ── Simulaciones ───────────────────────────────────────────────────────

        /// <summary>
        /// Simula el envío de datos a Firebase con un retardo.
        /// REEMPLAZAR por llamadas reales al SDK de Firebase cuando se integre.
        /// </summary>
        private IEnumerator SimularSincronizacion()
        {
            EstaSincronizando = true;

            // Obtener el JSON guardado localmente.
            string json = SaveSystem.ObtenerJSONGuardado();

            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[AndroidBridge] No hay datos locales para sincronizar. Guarda primero.");
                EstaSincronizando = false;
                OnSincronizacionCompletada?.Invoke(false);
                yield break;
            }

            Debug.Log("═══════════════════════════════════════════════════");
            Debug.Log("[AndroidBridge] ☁️ Iniciando sincronización con Firebase...");
            Debug.Log($"[AndroidBridge] Endpoint: {FirebaseURL}");
            Debug.Log($"[AndroidBridge] Colección: {ColeccionFirestore}");
            Debug.Log($"[AndroidBridge] Tamaño del payload: {json.Length} bytes");
            Debug.Log("═══════════════════════════════════════════════════");

            // ── STUB: Simular latencia de red ──────────────────────────────────
            // En la implementación real, esto sería:
            //   var docRef = FirebaseFirestore.DefaultInstance
            //       .Collection(ColeccionFirestore)
            //       .Document(datos.IdJugador);
            //   yield return docRef.SetAsync(datosDict).AsCoroutine();
            yield return new WaitForSeconds(TiempoSimuladoSync);

            // Simular éxito (90% de probabilidad) o fallo de red (10%).
            bool exito = UnityEngine.Random.value > 0.1f;

            if (exito)
            {
                UltimaSincronizacion = DateTime.Now.ToString("HH:mm:ss");
                Debug.Log($"[AndroidBridge] ✅ Sincronización exitosa a las {UltimaSincronizacion}.");
                Debug.Log("[AndroidBridge] Datos subidos a Firestore correctamente (STUB).");
            }
            else
            {
                Debug.LogWarning("[AndroidBridge] ⚠️ Error de red simulado. Reintenta más tarde.");
                Debug.LogWarning("[AndroidBridge] Los datos locales están a salvo.");
            }

            EstaSincronizando = false;
            OnSincronizacionCompletada?.Invoke(exito);
        }

        /// <summary>
        /// Simula la descarga de datos desde Firebase.
        /// </summary>
        private IEnumerator SimularDescarga()
        {
            EstaSincronizando = true;

            Debug.Log("[AndroidBridge] ☁️ Descargando datos desde Firebase (STUB)...");
            yield return new WaitForSeconds(TiempoSimuladoSync);

            Debug.Log("[AndroidBridge] ✅ Descarga simulada completada.");
            Debug.Log("[AndroidBridge] En la implementación real, se actualizaría el GameData local.");

            EstaSincronizando = false;
            OnSincronizacionCompletada?.Invoke(true);
        }
    }
}
