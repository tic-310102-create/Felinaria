// ============================================================
//  AudioManager.cs
//  Felinaria: El último presagio
//  Fase 4 – Persistencia y Pulido
//
//  RESPONSABILIDAD:
//    - Singleton global para reproducir música de fondo (BGM) y efectos (SFX).
//    - Puede ser llamado desde cualquier script del juego:
//        AudioManager.Instancia.ReproducirSFX(clipAtaque);
//        AudioManager.Instancia.ReproducirMusica(clipBGM);
//    - Controla volumen, silenciado y transiciones de música.
//    - Persiste entre escenas (DontDestroyOnLoad).
//
//  CÓMO FUNCIONA (para principiantes):
//    Tiene dos AudioSource separados: uno para música (loop) y
//    otro para SFX (one-shot). Expone métodos simples para
//    reproducir, pausar y cambiar volumen.
// ============================================================

using System.Collections;
using UnityEngine;

namespace Felinaria.Audio
{
    /// <summary>
    /// Singleton global de audio. Se coloca en un GameObject llamado "AudioManager".
    /// Persiste entre escenas con DontDestroyOnLoad.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        private static AudioManager _instancia;
        private static bool _aplicacionCerrando = false;

        /// <summary>Indica si existe una instancia activa sin forzar su creación.</summary>
        public static bool InstanciaExiste => _instancia != null && !_aplicacionCerrando;

        public static AudioManager Instancia
        {
            get
            {
                if (_aplicacionCerrando) return null;
                if (_instancia == null)
                {
                    _instancia = FindFirstObjectByType<AudioManager>();
                    if (_instancia == null && Application.isPlaying)
                    {
                        var go = new GameObject("AudioManager_Auto");
                        _instancia = go.AddComponent<AudioManager>();
                    }
                }
                return _instancia;
            }
            private set => _instancia = value;
        }

        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Música de Fondo (BGM)")]
        [Tooltip("AudioClip de la música que suena al iniciar la escena. " +
                 "Dejar vacío para no reproducir nada al inicio.")]
        public AudioClip MusicaInicial;

        [Tooltip("Volumen de la música (0 a 1).")]
        [Range(0f, 1f)]
        public float VolumenMusica = 0.7f;

        [Tooltip("Velocidad del fade al cambiar de pista (segundos).")]
        [Range(0.1f, 3f)]
        public float DuracionFadeMusica = 1f;

        [Header("Efectos de Sonido (SFX)")]
        [Tooltip("Volumen global de los SFX (0 a 1).")]
        [Range(0f, 1f)]
        public float VolumenSFX = 1f;

        [Header("Clips de SFX Predefinidos (arrastrar desde Assets)")]
        [Tooltip("Sonido al mover una unidad.")]
        public AudioClip SFXMover;

        [Tooltip("Sonido al atacar.")]
        public AudioClip SFXAtacar;

        [Tooltip("Sonido al recibir daño.")]
        public AudioClip SFXDanio;

        [Tooltip("Sonido al seleccionar una unidad.")]
        public AudioClip SFXSeleccionar;

        [Tooltip("Sonido al abrir/cerrar menú.")]
        public AudioClip SFXMenu;

        [Tooltip("Sonido de victoria.")]
        public AudioClip SFXVictoria;

        [Tooltip("Sonido de derrota.")]
        public AudioClip SFXDerrota;

        [Tooltip("Sonido al guardar partida.")]
        public AudioClip SFXGuardar;

        // ── Estado público ─────────────────────────────────────────────────────
        /// <summary>True si la música está silenciada.</summary>
        public bool MusicaSilenciada { get; private set; }

        /// <summary>True si los SFX están silenciados.</summary>
        public bool SFXSilenciados { get; private set; }

        // ── Componentes internos ───────────────────────────────────────────────
        private AudioSource _sourceMusica;
        private AudioSource _sourceSFX;
        private Coroutine _coroutinaFade;

        // ── Unity Lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            _aplicacionCerrando = false;
            // Singleton con persistencia entre escenas.
            if (_instancia != null && _instancia != this)
            {
                Debug.LogWarning("[AudioManager] Ya existe una instancia. Destruyendo duplicado.");
                Destroy(gameObject);
                return;
            }
            _instancia = this;
            DontDestroyOnLoad(gameObject);

            // Crear los AudioSource necesarios.
            ConfigurarAudioSources();
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
            // Reproducir música inicial si está asignada.
            if (MusicaInicial != null)
                ReproducirMusica(MusicaInicial);
        }

        // ── Configuración ──────────────────────────────────────────────────────
        /// <summary>
        /// Crea y configura los dos AudioSource (BGM y SFX).
        /// </summary>
        private void ConfigurarAudioSources()
        {
            // AudioSource para música (loop continuo).
            _sourceMusica = gameObject.AddComponent<AudioSource>();
            _sourceMusica.loop = true;
            _sourceMusica.playOnAwake = false;
            _sourceMusica.volume = VolumenMusica;
            _sourceMusica.priority = 0;  // Máxima prioridad.

            // AudioSource para SFX (one-shot, sin loop).
            _sourceSFX = gameObject.AddComponent<AudioSource>();
            _sourceSFX.loop = false;
            _sourceSFX.playOnAwake = false;
            _sourceSFX.volume = VolumenSFX;
            _sourceSFX.priority = 128;

            Debug.Log("[AudioManager] AudioSources configurados.");
        }

        // ── API pública: Música ────────────────────────────────────────────────

        /// <summary>
        /// Reproduce una pista de música con fade-in.
        /// Si ya hay música sonando, hace crossfade.
        /// </summary>
        public void ReproducirMusica(AudioClip clip)
        {
            if (clip == null)
            {
                Debug.LogWarning("[AudioManager] ReproducirMusica: clip es null.");
                return;
            }

            // Si ya está sonando la misma pista, no hacer nada.
            if (_sourceMusica.clip == clip && _sourceMusica.isPlaying)
                return;

            // Detener fade anterior si existe.
            if (_coroutinaFade != null)
                StopCoroutine(_coroutinaFade);

            _coroutinaFade = StartCoroutine(CambiarMusicaConFade(clip));
        }

        /// <summary>
        /// Pausa la música actual.
        /// </summary>
        public void PausarMusica()
        {
            _sourceMusica.Pause();
            Debug.Log("[AudioManager] Música pausada.");
        }

        /// <summary>
        /// Reanuda la música pausada.
        /// </summary>
        public void ReanudarMusica()
        {
            _sourceMusica.UnPause();
            Debug.Log("[AudioManager] Música reanudada.");
        }

        /// <summary>
        /// Detiene la música con fade-out.
        /// </summary>
        public void DetenerMusica()
        {
            if (_coroutinaFade != null)
                StopCoroutine(_coroutinaFade);

            _coroutinaFade = StartCoroutine(FadeOut());
        }

        /// <summary>
        /// Silencia/des-silencia la música.
        /// </summary>
        public void ToggleSilenciarMusica()
        {
            MusicaSilenciada = !MusicaSilenciada;
            _sourceMusica.mute = MusicaSilenciada;
            Debug.Log($"[AudioManager] Música {(MusicaSilenciada ? "silenciada" : "activada")}.");
        }

        /// <summary>
        /// Cambia el volumen de la música en runtime.
        /// </summary>
        public void SetVolumenMusica(float volumen)
        {
            VolumenMusica = Mathf.Clamp01(volumen);
            _sourceMusica.volume = VolumenMusica;
        }

        // ── API pública: SFX ───────────────────────────────────────────────────

        /// <summary>
        /// Reproduce un efecto de sonido (one-shot).
        /// No interrumpe otros SFX que estén sonando.
        /// </summary>
        public void ReproducirSFX(AudioClip clip)
        {
            if (clip == null || SFXSilenciados) return;
            _sourceSFX.PlayOneShot(clip, VolumenSFX);
        }

        /// <summary>
        /// Reproduce un SFX con volumen personalizado.
        /// </summary>
        public void ReproducirSFX(AudioClip clip, float volumenOverride)
        {
            if (clip == null || SFXSilenciados) return;
            _sourceSFX.PlayOneShot(clip, volumenOverride);
        }

        /// <summary>
        /// Silencia/des-silencia los SFX.
        /// </summary>
        public void ToggleSilenciarSFX()
        {
            SFXSilenciados = !SFXSilenciados;
            _sourceSFX.mute = SFXSilenciados;
            Debug.Log($"[AudioManager] SFX {(SFXSilenciados ? "silenciados" : "activados")}.");
        }

        /// <summary>
        /// Cambia el volumen de los SFX en runtime.
        /// </summary>
        public void SetVolumenSFX(float volumen)
        {
            VolumenSFX = Mathf.Clamp01(volumen);
            _sourceSFX.volume = VolumenSFX;
        }

        // ── API pública: SFX predefinidos ──────────────────────────────────────
        // Métodos de conveniencia para reproducir SFX sin buscar el clip.

        /// <summary>Reproduce el SFX de mover unidad.</summary>
        public void SFX_Mover()     => ReproducirSFX(SFXMover);

        /// <summary>Reproduce el SFX de ataque.</summary>
        public void SFX_Atacar()    => ReproducirSFX(SFXAtacar);

        /// <summary>Reproduce el SFX de recibir daño.</summary>
        public void SFX_Danio()     => ReproducirSFX(SFXDanio);

        /// <summary>Reproduce el SFX de selección.</summary>
        public void SFX_Seleccionar() => ReproducirSFX(SFXSeleccionar);

        /// <summary>Reproduce el SFX de menú.</summary>
        public void SFX_Menu()      => ReproducirSFX(SFXMenu);

        /// <summary>Reproduce el SFX de victoria.</summary>
        public void SFX_Victoria()  => ReproducirSFX(SFXVictoria);

        /// <summary>Reproduce el SFX de derrota.</summary>
        public void SFX_Derrota()   => ReproducirSFX(SFXDerrota);

        /// <summary>Reproduce el SFX de guardar partida.</summary>
        public void SFX_Guardar()   => ReproducirSFX(SFXGuardar);

        // ── Transiciones de música ─────────────────────────────────────────────

        /// <summary>
        /// Crossfade: baja el volumen de la pista actual, cambia el clip,
        /// y sube el volumen de la nueva pista.
        /// </summary>
        private IEnumerator CambiarMusicaConFade(AudioClip nuevoClip)
        {
            // Fade out si hay música sonando.
            if (_sourceMusica.isPlaying)
            {
                yield return StartCoroutine(FadeOut());
            }

            // Cambiar clip y fade in.
            _sourceMusica.clip = nuevoClip;
            _sourceMusica.Play();
            yield return StartCoroutine(FadeIn());

            Debug.Log($"[AudioManager] 🎵 Música cambiada a: '{nuevoClip.name}'");
        }

        /// <summary>
        /// Reduce gradualmente el volumen de la música hasta 0.
        /// </summary>
        private IEnumerator FadeOut()
        {
            float volumenInicial = _sourceMusica.volume;
            float timer = 0f;

            while (timer < DuracionFadeMusica)
            {
                timer += Time.deltaTime;
                _sourceMusica.volume = Mathf.Lerp(volumenInicial, 0f, timer / DuracionFadeMusica);
                yield return null;
            }

            _sourceMusica.volume = 0f;
            _sourceMusica.Stop();
        }

        /// <summary>
        /// Incrementa gradualmente el volumen de la música desde 0 hasta VolumenMusica.
        /// </summary>
        private IEnumerator FadeIn()
        {
            _sourceMusica.volume = 0f;
            float timer = 0f;

            while (timer < DuracionFadeMusica)
            {
                timer += Time.deltaTime;
                _sourceMusica.volume = Mathf.Lerp(0f, VolumenMusica, timer / DuracionFadeMusica);
                yield return null;
            }

            _sourceMusica.volume = VolumenMusica;
        }
    }
}
