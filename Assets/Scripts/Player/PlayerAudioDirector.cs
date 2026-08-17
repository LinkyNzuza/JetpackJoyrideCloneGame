// Ported from the 3D runner's PlayerAudioDirector. Same principle: audio is a pure listener on
// the player's published events, the player does not know it exists, and every clip slot is
// optional so the scene stays playable while sound is still being sourced.
//
// What changed in the port. The 3D version fired one-shots on jump, land, slide and stand-up, and
// derived footstep cadence from forward speed because footsteps have no event. This game has no
// footsteps and no jump: it has one continuous verb. So the two speed-derived sounds became two
// continuous loops driven by published booleans, the jetpack while thrusting and the magnet while
// active. That is the same idea, which is that some sounds follow state rather than events, applied
// to a different game.
//
// This also fills the two empty cue hooks the player already had, so death and shield break finally
// make a sound.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Plays player audio by listening to <see cref="PlayerController"/>'s published events and
    /// read-only state. Writes nothing back.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAudioDirector : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("Player whose events drive audio. Found on this object or in the scene if left empty.")]
        [SerializeField] private PlayerController _player;

        [Header("Sources")]
        [Tooltip("Source for one-shot effects. Created automatically when left empty.")]
        [SerializeField] private AudioSource _effectSource;

        [Tooltip("Looping source for the jetpack. Created automatically when left empty.")]
        [SerializeField] private AudioSource _jetpackSource;

        [Tooltip("Looping source for the magnet. Created automatically when left empty.")]
        [SerializeField] private AudioSource _magnetSource;

        [Header("Continuous clips")]
        [Tooltip("Loops while the player is thrusting. Fades rather than cutting, so tapping does not click.")]
        [SerializeField] private AudioClip _jetpackLoop;

        [Tooltip("Loops while the magnet power-up is active.")]
        [SerializeField] private AudioClip _magnetLoop;

        [Header("One-shot clips")]
        [Tooltip("Played once per coin collected.")]
        [SerializeField] private AudioClip _coin;

        [Tooltip("Played once when any power-up becomes active.")]
        [SerializeField] private AudioClip _powerUpActivated;

        [Tooltip("Played once when any power-up ends, including when it ends because the player died.")]
        [SerializeField] private AudioClip _powerUpExpired;

        [Tooltip("Played once when the player dies.")]
        [SerializeField] private AudioClip _death;

        [Tooltip("Played once when a shield absorbs an obstacle. Wire to the shield break cue.")]
        [SerializeField] private AudioClip _shieldBreak;

        [Tooltip("Played once when a run resets, so a retry is audible.")]
        [SerializeField] private AudioClip _runStart;

        [Header("Levels")]
        [SerializeField, Range(0f, 1f)] private float _effectVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _jetpackVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _magnetVolume = 0.35f;

        [Tooltip("Seconds for a loop to fade in or out. Zero cuts, which clicks on a short tap.")]
        [SerializeField, Range(0f, 0.5f)] private float _loopFade = 0.08f;

        [Tooltip("Random pitch spread on one-shots, so a run of coins does not sound mechanical.")]
        [SerializeField, Range(0f, 0.5f)] private float _pitchVariation = 0.05f;

        // Tuning: the shortest gap allowed between two plays of the SAME clip. This exists because the
        // magnet works now. It drags a whole band of coins into contact within a few frames, and each
        // one raised OnCoinCollected, so ten copies of the coin sound started on one source inside a
        // fifth of a second. Ten overlapping copies at the same volume do not sound ten times better;
        // they sum past full scale and arrive as a click. Zero disables the guard.
        [Tooltip("Shortest gap between two plays of the same clip. Stops a magnet turning a band of " +
                 "coins into one clipped burst.")]
        [SerializeField, Range(0f, 0.5f)] private float _repeatGuard = 0.06f;

        // When each clip was last played, for the repeat guard. Keyed by clip so collapsing repeats of
        // one sound never suppresses a different one.
        private readonly Dictionary<AudioClip, float> _lastPlayed = new Dictionary<AudioClip, float>();

        // Stable delegates so subscribe and unsubscribe act on the same instances.
        private Action _onDeath;
        private Action<int> _onCoin;
        private Action<PowerUpType> _onActivated;
        private Action<PowerUpType> _onExpired;

        private void Awake()
        {
            if (_player == null) _player = GetComponent<PlayerController>();
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();

            if (_player == null)
            {
                Debug.LogWarning("[PlayerAudioDirector] No PlayerController found. Audio stays silent.", this);
                return;
            }

            // The one-shot source sits at full volume and _effectVolume is applied per play instead.
            // It used to be set on both, and PlayOneShot multiplies its scale by the source volume, so
            // a serialized 0.8 was actually coming out at 0.64. The number in the Inspector now means
            // what it says.
            _effectSource = EnsureSource(_effectSource, loop: false, volume: 1f);
            _jetpackSource = EnsureSource(_jetpackSource, loop: true, volume: 0f);
            _magnetSource = EnsureSource(_magnetSource, loop: true, volume: 0f);

            PreloadClips();

            _onDeath = HandleDeath;
            _onCoin = HandleCoin;
            _onActivated = HandlePowerUpActivated;
            _onExpired = HandlePowerUpExpired;
        }

        private void OnEnable()
        {
            if (_player == null) return;

            _player.OnPlayerDeath += _onDeath;
            _player.OnCoinCollected += _onCoin;
            _player.OnPowerUpActivated += _onActivated;
            _player.OnPowerUpExpired += _onExpired;
        }

        private void OnDisable()
        {
            if (_player == null) return;

            _player.OnPlayerDeath -= _onDeath;
            _player.OnCoinCollected -= _onCoin;
            _player.OnPowerUpActivated -= _onActivated;
            _player.OnPowerUpExpired -= _onExpired;

            StopLoop(_jetpackSource);
            StopLoop(_magnetSource);
        }

        /// <summary>
        /// Drives the two loops from published booleans. Read only, every frame, so a loop can never
        /// disagree with the state it represents. Volume is faded rather than switched, because the
        /// player taps thrust constantly and a hard cut on every tap is audible as a click.
        /// </summary>
        private void Update()
        {
            if (_player == null) return;

            float dt = Time.deltaTime;

            DriveLoop(_jetpackSource, _jetpackLoop, _player.IsThrusting, _jetpackVolume, dt);
            DriveLoop(_magnetSource, _magnetLoop, _player.IsMagnetActive, _magnetVolume, dt);
        }

        /// <summary>
        /// Wire this to the shield break cue on <see cref="PlayerCollision"/>, which is a UnityEvent
        /// rather than a C# event, so it needs a public method to point at.
        /// </summary>
        public void PlayShieldBreak() => PlayOnce(_shieldBreak);

        /// <summary>
        /// Wire this to whatever calls <see cref="PlayerController.ResetRun"/>, so a retry is audible.
        /// The player publishes no reset event outward, only an internal one, so this stays a method
        /// the game state system calls rather than a subscription.
        /// </summary>
        public void PlayRunStart() => PlayOnce(_runStart);

        private void HandleDeath()
        {
            // Death takes the source. Dying expires every active power-up first, and each expiry raises
            // its own event, so a player who dies holding a shield and a magnet had two copies of the
            // power-down sound already playing before the death sound started. Three overlapping cues
            // for one moment reads as noise rather than as an event. Clearing the source first means
            // death is heard cleanly, which matters because it is the one moment the player has to
            // understand.
            if (_effectSource != null) _effectSource.Stop();

            // Cleared so the guard cannot suppress the death sound because something else played it
            // moments ago, and so the next run starts with no history.
            _lastPlayed.Clear();

            PlayOnce(_death);

            // Cut both loops immediately on death. Fading them would leave the jetpack audible over a
            // corpse, which reads as the sound being detached from the game.
            StopLoop(_jetpackSource);
            StopLoop(_magnetSource);
        }

        private void HandleCoin(int value) => PlayOnce(_coin);

        private void HandlePowerUpActivated(PowerUpType type) => PlayOnce(_powerUpActivated);

        private void HandlePowerUpExpired(PowerUpType type) => PlayOnce(_powerUpExpired);

        private void DriveLoop(AudioSource source, AudioClip clip, bool wanted, float target, float dt)
        {
            if (source == null || clip == null) return;

            if (source.clip != clip) source.clip = clip;

            if (wanted && !source.isPlaying) source.Play();

            float goal = wanted ? target : 0f;

            if (_loopFade <= 0f || dt <= 0f)
            {
                source.volume = goal;
            }
            else
            {
                float step = (target <= 0f ? 1f : target) * dt / _loopFade;
                source.volume = Mathf.MoveTowards(source.volume, goal, step);
            }

            if (!wanted && source.isPlaying && source.volume <= 0f) source.Pause();
        }

        private void PlayOnce(AudioClip clip)
        {
            if (clip == null || _effectSource == null) return;

            // Per clip rather than global, so a coin arriving in the same frame as a shield break does
            // not silence either of them. Only repeats of the same sound are collapsed.
            if (_repeatGuard > 0f)
            {
                float now = Time.unscaledTime;
                if (_lastPlayed.TryGetValue(clip, out float previous) && now - previous < _repeatGuard)
                    return;

                _lastPlayed[clip] = now;
            }

            _effectSource.pitch = _pitchVariation <= 0f
                ? 1f
                : 1f + UnityEngine.Random.Range(-_pitchVariation, _pitchVariation);

            _effectSource.PlayOneShot(clip, _effectVolume);
        }

        /// <summary>
        /// Forces the assigned clips to load now rather than on first play.
        /// <para>
        /// These import with preloadAudioData off, so the data is fetched the first time a clip is
        /// asked for. For a short one-shot that means the first coin of a session, the first shield
        /// break and the first death can be swallowed or arrive late, which reads as the audio being
        /// unreliable rather than as a loading detail. Doing it here rather than by rewriting the
        /// import settings on forty-one files keeps the change in one place and touches no assets.
        /// </para>
        /// </summary>
        private void PreloadClips()
        {
            AudioClip[] clips =
            {
                _jetpackLoop, _magnetLoop, _coin, _powerUpActivated,
                _powerUpExpired, _death, _shieldBreak, _runStart
            };

            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip clip = clips[i];
                if (clip == null) continue;
                if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            }
        }

        private static void StopLoop(AudioSource source)
        {
            if (source == null) return;
            source.volume = 0f;
            if (source.isPlaying) source.Stop();
        }

        private AudioSource EnsureSource(AudioSource existing, bool loop, float volume)
        {
            if (existing != null) return existing;

            var created = gameObject.AddComponent<AudioSource>();
            created.playOnAwake = false;
            created.loop = loop;
            created.spatialBlend = 0f;
            created.volume = volume;
            return created;
        }
    }
}
