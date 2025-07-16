using System;
using System.Collections.Generic;
using UnityEngine;

namespace GungeonTogether.UI
{
    /// <summary>
    /// Audio manager for GungeonTogether UI
    /// Provides immersive sound effects for UI interactions
    /// </summary>
    public class UIAudioManager : MonoBehaviour
    {
        private static UIAudioManager _instance;
        public static UIAudioManager Instance => _instance;

        [Header("Audio Settings")]
        public float masterVolume = 0.7f;
        public bool enableSoundEffects = true;

        [Header("Audio Sources")]
        public AudioSource uiAudioSource;
        public AudioSource notificationAudioSource;
        public AudioSource ambientAudioSource;

        // Audio clip storage
        private Dictionary<string, AudioClip> audioClips = new Dictionary<string, AudioClip>();
        private Dictionary<string, AudioClipData> clipData = new Dictionary<string, AudioClipData>();

        // Audio clip data structure
        [System.Serializable]
        public class AudioClipData
        {
            public AudioClip clip;
            public float volume = 1f;
            public float pitch = 1f;
            public bool loop = false;
            public AudioSource preferredSource;

            public AudioClipData(AudioClip audioClip, float vol = 1f, float p = 1f, bool isLooping = false)
            {
                clip = audioClip;
                volume = vol;
                pitch = p;
                loop = isLooping;
            }
        }

        void Awake()
        {
            if (ReferenceEquals(_instance, null))
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeAudioManager();
                GungeonTogether.Logging.Debug.Log("[UIAudio] UI Audio Manager initialized");
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Initialize the audio manager
        /// </summary>
        private void InitializeAudioManager()
        {
            // Create audio sources if they don't exist
            if (ReferenceEquals(uiAudioSource, null))
            {
                var uiAudioObj = new GameObject("UIAudioSource");
                uiAudioObj.transform.SetParent(transform);
                uiAudioSource = uiAudioObj.AddComponent<AudioSource>();
                uiAudioSource.playOnAwake = false;
                uiAudioSource.volume = masterVolume;
            }

            if (ReferenceEquals(notificationAudioSource, null))
            {
                var notificationAudioObj = new GameObject("NotificationAudioSource");
                notificationAudioObj.transform.SetParent(transform);
                notificationAudioSource = notificationAudioObj.AddComponent<AudioSource>();
                notificationAudioSource.playOnAwake = false;
                notificationAudioSource.volume = masterVolume * 0.8f;
            }

            if (ReferenceEquals(ambientAudioSource, null))
            {
                var ambientAudioObj = new GameObject("AmbientAudioSource");
                ambientAudioObj.transform.SetParent(transform);
                ambientAudioSource = ambientAudioObj.AddComponent<AudioSource>();
                ambientAudioSource.playOnAwake = false;
                ambientAudioSource.volume = masterVolume * 0.3f;
                ambientAudioSource.loop = true;
            }

            // Generate procedural audio clips
            GenerateAudioClips();
        }

        /// <summary>
        /// Generate procedural audio clips for UI sounds
        /// </summary>
        private void GenerateAudioClips()
        {
            try
            {
                // UI Interaction Sounds
                clipData["ui_click"] = new AudioClipData(GenerateClickSound(), 0.6f, 1f);
                clipData["ui_hover"] = new AudioClipData(GenerateHoverSound(), 0.4f, 1.2f);
                clipData["ui_open"] = new AudioClipData(GenerateOpenSound(), 0.7f, 1f);
                clipData["ui_close"] = new AudioClipData(GenerateCloseSound(), 0.7f, 0.9f);
                clipData["ui_success"] = new AudioClipData(GenerateSuccessSound(), 0.8f, 1f);
                clipData["ui_error"] = new AudioClipData(GenerateErrorSound(), 0.8f, 1f);
                clipData["ui_warning"] = new AudioClipData(GenerateWarningSound(), 0.7f, 1f);

                // Multiplayer Specific Sounds
                clipData["mp_connect"] = new AudioClipData(GenerateConnectSound(), 0.8f, 1f);
                clipData["mp_disconnect"] = new AudioClipData(GenerateDisconnectSound(), 0.8f, 0.8f);
                clipData["mp_host_found"] = new AudioClipData(GenerateHostFoundSound(), 0.6f, 1.1f);
                clipData["mp_player_joined"] = new AudioClipData(GeneratePlayerJoinedSound(), 0.7f, 1f);
                clipData["mp_player_left"] = new AudioClipData(GeneratePlayerLeftSound(), 0.6f, 0.9f);

                // Notification Sounds
                clipData["notification_info"] = new AudioClipData(GenerateNotificationSound(NotificationTone.Info), 0.5f, 1f);
                clipData["notification_success"] = new AudioClipData(GenerateNotificationSound(NotificationTone.Success), 0.6f, 1f);
                clipData["notification_warning"] = new AudioClipData(GenerateNotificationSound(NotificationTone.Warning), 0.7f, 1f);
                clipData["notification_error"] = new AudioClipData(GenerateNotificationSound(NotificationTone.Error), 0.8f, 1f);
                clipData["notification_steam"] = new AudioClipData(GenerateNotificationSound(NotificationTone.Steam), 0.6f, 1.1f);
                clipData["notification_multiplayer"] = new AudioClipData(GenerateNotificationSound(NotificationTone.Multiplayer), 0.6f, 1f);

                // Steam Sounds
                clipData["steam_overlay_open"] = new AudioClipData(GenerateSteamOverlaySound(), 0.5f, 1f);
                clipData["steam_friend_online"] = new AudioClipData(GenerateFriendOnlineSound(), 0.4f, 1.2f);

                GungeonTogether.Logging.Debug.Log("[UIAudio] Generated procedural audio clips");
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogError($"[UIAudio] Failed to generate audio clips: {ex.Message}");
            }
        }

        /// <summary>
        /// Play a UI sound effect
        /// </summary>
        public void PlaySound(string soundName, float volumeMultiplier = 1f, float pitchMultiplier = 1f)
        {
            if (!enableSoundEffects || !clipData.ContainsKey(soundName))
                return;

            try
            {
                var data = clipData[soundName];
                var source = data.preferredSource ?? uiAudioSource;

                source.clip = data.clip;
                source.volume = masterVolume * data.volume * volumeMultiplier;
                source.pitch = data.pitch * pitchMultiplier;
                source.loop = data.loop;
                source.Play();

                // Only log in debug builds or for important sounds
                // GungeonTogether.Logging.Debug.Log($"[UIAudio] Playing sound: {soundName}");
            }
            catch (Exception ex)
            {
                GungeonTogether.Logging.Debug.LogError($"[UIAudio] Failed to play sound {soundName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Play a sound with random pitch variation
        /// </summary>
        public void PlaySoundRandomPitch(string soundName, float pitchVariation = 0.1f, float volumeMultiplier = 1f)
        {
            float randomPitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
            PlaySound(soundName, volumeMultiplier, randomPitch);
        }

        /// <summary>
        /// Stop all UI sounds
        /// </summary>
        public void StopAllSounds()
        {
            uiAudioSource.Stop();
            notificationAudioSource.Stop();
            ambientAudioSource.Stop();
        }

        /// <summary>
        /// Set master volume
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            uiAudioSource.volume = masterVolume;
            notificationAudioSource.volume = masterVolume * 0.8f;
            ambientAudioSource.volume = masterVolume * 0.3f;
        }

        /// <summary>
        /// Enable or disable sound effects
        /// </summary>
        public void SetSoundEffectsEnabled(bool enabled)
        {
            enableSoundEffects = enabled;
        }

        // Procedural Audio Generation Methods

        private enum NotificationTone
        {
            Info,
            Success,
            Warning,
            Error,
            Steam,
            Multiplayer
        }

        /// <summary>
        /// Generate a click sound
        /// </summary>
        private AudioClip GenerateClickSound()
        {
            return GenerateTone(800f, 0.1f, ToneType.Square, 0.5f);
        }

        /// <summary>
        /// Generate a hover sound
        /// </summary>
        private AudioClip GenerateHoverSound()
        {
            return GenerateTone(600f, 0.05f, ToneType.Sine, 0.3f);
        }

        /// <summary>
        /// Generate an open sound
        /// </summary>
        private AudioClip GenerateOpenSound()
        {
            return GenerateChord(new float[] { 400f, 600f, 800f }, 0.3f, ToneType.Sine, 0.6f);
        }

        /// <summary>
        /// Generate a close sound
        /// </summary>
        private AudioClip GenerateCloseSound()
        {
            return GenerateChord(new float[] { 800f, 600f, 400f }, 0.25f, ToneType.Sine, 0.5f);
        }

        /// <summary>
        /// Generate a success sound
        /// </summary>
        private AudioClip GenerateSuccessSound()
        {
            return GenerateChord(new float[] { 523f, 659f, 784f }, 0.4f, ToneType.Sine, 0.7f); // C-E-G major chord
        }

        /// <summary>
        /// Generate an error sound
        /// </summary>
        private AudioClip GenerateErrorSound()
        {
            return GenerateTone(200f, 0.5f, ToneType.Sawtooth, 0.8f);
        }

        /// <summary>
        /// Generate a warning sound
        /// </summary>
        private AudioClip GenerateWarningSound()
        {
            return GenerateChord(new float[] { 440f, 440f, 440f }, 0.2f, ToneType.Triangle, 0.6f); // Triple A note
        }

        /// <summary>
        /// Generate connect sound
        /// </summary>
        private AudioClip GenerateConnectSound()
        {
            return GenerateChord(new float[] { 330f, 415f, 523f }, 0.6f, ToneType.Sine, 0.7f); // Ascending connection
        }

        /// <summary>
        /// Generate disconnect sound
        /// </summary>
        private AudioClip GenerateDisconnectSound()
        {
            return GenerateChord(new float[] { 523f, 415f, 330f }, 0.5f, ToneType.Sine, 0.6f); // Descending disconnection
        }

        /// <summary>
        /// Generate host found sound
        /// </summary>
        private AudioClip GenerateHostFoundSound()
        {
            return GenerateTone(880f, 0.15f, ToneType.Sine, 0.5f);
        }

        /// <summary>
        /// Generate player joined sound
        /// </summary>
        private AudioClip GeneratePlayerJoinedSound()
        {
            return GenerateChord(new float[] { 440f, 554f, 659f }, 0.3f, ToneType.Sine, 0.6f);
        }

        /// <summary>
        /// Generate player left sound
        /// </summary>
        private AudioClip GeneratePlayerLeftSound()
        {
            return GenerateChord(new float[] { 659f, 554f, 440f }, 0.25f, ToneType.Sine, 0.5f);
        }

        /// <summary>
        /// Generate notification sound based on type
        /// </summary>
        private AudioClip GenerateNotificationSound(NotificationTone tone)
        {
            switch (tone)
            {
                case NotificationTone.Info:
                    return GenerateTone(750f, 0.2f, ToneType.Sine, 0.4f);
                case NotificationTone.Success:
                    return GenerateChord(new float[] { 523f, 659f }, 0.3f, ToneType.Sine, 0.5f);
                case NotificationTone.Warning:
                    return GenerateTone(440f, 0.4f, ToneType.Triangle, 0.6f);
                case NotificationTone.Error:
                    return GenerateTone(220f, 0.6f, ToneType.Sawtooth, 0.7f);
                case NotificationTone.Steam:
                    return GenerateChord(new float[] { 392f, 494f, 622f }, 0.25f, ToneType.Sine, 0.5f);
                case NotificationTone.Multiplayer:
                    return GenerateChord(new float[] { 349f, 440f, 523f }, 0.3f, ToneType.Sine, 0.5f);
                default:
                    return GenerateTone(500f, 0.2f, ToneType.Sine, 0.4f);
            }
        }

        /// <summary>
        /// Generate Steam overlay sound
        /// </summary>
        private AudioClip GenerateSteamOverlaySound()
        {
            return GenerateChord(new float[] { 261f, 329f, 392f, 523f }, 0.4f, ToneType.Sine, 0.4f);
        }

        /// <summary>
        /// Generate friend online sound
        /// </summary>
        private AudioClip GenerateFriendOnlineSound()
        {
            return GenerateTone(1000f, 0.1f, ToneType.Sine, 0.3f);
        }

        // Core Audio Generation Methods

        private enum ToneType
        {
            Sine,
            Square,
            Triangle,
            Sawtooth
        }

        /// <summary>
        /// Generate a single tone
        /// </summary>
        private AudioClip GenerateTone(float frequency, float duration, ToneType toneType, float amplitude)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.RoundToInt(duration * sampleRate);

            var clip = AudioClip.Create("GeneratedTone", sampleCount, 1, sampleRate, false);
            var samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)i / sampleRate;
                float value = GenerateWaveform(time, frequency, toneType);

                // Apply envelope (fade in/out)
                float envelope = CalculateEnvelope(time, duration);
                samples[i] = value * amplitude * envelope;
            }

            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Generate a chord (multiple tones)
        /// </summary>
        private AudioClip GenerateChord(float[] frequencies, float duration, ToneType toneType, float amplitude)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.RoundToInt(duration * sampleRate);

            var clip = AudioClip.Create("GeneratedChord", sampleCount, 1, sampleRate, false);
            var samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)i / sampleRate;
                float value = 0f;

                // Sum all frequencies
                foreach (float freq in frequencies)
                {
                    value += GenerateWaveform(time, freq, toneType);
                }

                // Normalize by number of frequencies
                value /= frequencies.Length;

                // Apply envelope
                float envelope = CalculateEnvelope(time, duration);
                samples[i] = value * amplitude * envelope;
            }

            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Generate waveform based on type
        /// </summary>
        private float GenerateWaveform(float time, float frequency, ToneType toneType)
        {
            float phase = time * frequency * 2f * Mathf.PI;

            switch (toneType)
            {
                case ToneType.Sine:
                    return Mathf.Sin(phase);
                case ToneType.Square:
                    return Mathf.Sign(Mathf.Sin(phase));
                case ToneType.Triangle:
                    return Mathf.Asin(Mathf.Sin(phase)) * 2f / Mathf.PI;
                case ToneType.Sawtooth:
                    return 2f * (phase / (2f * Mathf.PI) - Mathf.Floor(phase / (2f * Mathf.PI) + 0.5f));
                default:
                    return Mathf.Sin(phase);
            }
        }

        /// <summary>
        /// Calculate envelope for smooth fade in/out
        /// </summary>
        private float CalculateEnvelope(float time, float duration)
        {
            float fadeTime = duration * 0.1f; // 10% fade in/out

            if (time < fadeTime)
            {
                // Fade in
                return time / fadeTime;
            }
            else if (time > duration - fadeTime)
            {
                // Fade out
                return (duration - time) / fadeTime;
            }
            else
            {
                // Full volume
                return 1f;
            }
        }
    }
}
