using System;
using UnityEngine;

public enum CasinoSoundCue { Card, Chip, Step, Celebrate, Disappointed, Success, Mistake, Warning, Roulette, Button }

[DefaultExecutionOrder(-110)]
public sealed class CasinoSoundscape : MonoBehaviour
{
    private static CasinoSoundscape instance;
    private readonly AudioClip[] cues = new AudioClip[10];
    private readonly float[] lastPlayed = new float[10];
    private AudioSource music;
    private AudioSource ambience;
    private AudioSource[] voices;
    private int voice;
    private float intensity;
    public static float MusicVolume => PlayerPrefs.GetFloat("PitPanic.MusicVolume", .45f);
    public static float AmbienceVolume => PlayerPrefs.GetFloat("PitPanic.AmbienceVolume", .3f);
    public static float EffectsVolume => PlayerPrefs.GetFloat("PitPanic.EffectsVolume", .65f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (instance != null) return;
        instance = new GameObject("Casino Soundscape").AddComponent<CasinoSoundscape>();
        DontDestroyOnLoad(instance.gameObject);
    }

    private void Awake()
    {
        music = Source(true);
        ambience = Source(true);
        voices = new AudioSource[12];
        for (int i = 0; i < voices.Length; i++) voices[i] = Source(false);
        music.clip = ComposeMusic();
        ambience.clip = ComposeAmbience();
        for (int i = 0; i < cues.Length; i++) cues[i] = ComposeCue((CasinoSoundCue)i);
        ApplyVolumes();
        music.Play();
        ambience.Play();
    }

    private AudioSource Source(bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = 0;
        return source;
    }

    public static void SetVolume(string channel, float volume)
    {
        if (channel != "Music" && channel != "Ambience" && channel != "Effects")
            throw new ArgumentException("Unknown casino audio channel.", nameof(channel));
        PlayerPrefs.SetFloat("PitPanic." + channel + "Volume", Mathf.Clamp01(volume));
        PlayerPrefs.Save();
        if (instance != null) instance.ApplyVolumes();
    }

    private void ApplyVolumes()
    {
        music.volume = MusicVolume;
        ambience.volume = AmbienceVolume;
        foreach (AudioSource source in voices) source.volume = EffectsVolume;
    }

    public static void Play(CasinoSoundCue cue, Vector3 position)
    {
        if (instance == null) return;
        int index = (int)cue;
        float interval = cue == CasinoSoundCue.Warning ? 8 : cue == CasinoSoundCue.Step ? .16f : .07f;
        if (Time.unscaledTime - instance.lastPlayed[index] < interval) return;
        instance.lastPlayed[index] = Time.unscaledTime;
        AudioSource source = instance.voices[instance.voice++ % instance.voices.Length];
        source.pitch = 1;
        source.panStereo = Camera.main != null ? Mathf.Clamp((Camera.main.WorldToViewportPoint(position).x - .5f) * 1.3f, -.7f, .7f) : 0;
        float distance = Camera.main != null ? Vector3.Distance(position, Camera.main.transform.position) : 10;
        source.PlayOneShot(instance.cues[index], cue == CasinoSoundCue.Step ? .15f :
            Mathf.Clamp01(20 / Mathf.Max(20, distance)));
    }

    public static void SetIntensity(float value)
    {
        if (instance != null) instance.intensity = Mathf.Clamp01(value);
    }

    private void Update()
    {
        music.pitch = Mathf.MoveTowards(music.pitch, 1 + intensity * .08f, Time.unscaledDeltaTime * .015f);
        ambience.volume = Mathf.MoveTowards(ambience.volume, AmbienceVolume * (1 + intensity * .2f), Time.unscaledDeltaTime * .02f);
    }

    private static AudioClip Clip(string name, float[] samples)
    {
        AudioClip clip = AudioClip.Create(name, samples.Length, 1, 22050, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip ComposeMusic()
    {
        const int rate = 22050;
        const float beat = .5f;
        float[] samples = new float[rate * 16];
        int[] melody = { 72, 76, 79, 76, 74, 77, 81, 77, 71, 74, 79, 74, 72, 76, 79, 84 };
        int[] bass = { 48, 53, 55, 48 };
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            int note = Mathf.FloorToInt(t / beat);
            float phase = t % beat;
            float f = 440 * Mathf.Pow(2, (melody[note % melody.Length] - 69) / 12f);
            float b = 440 * Mathf.Pow(2, (bass[note / 8 % 4] - 69) / 12f);
            float envelope = Mathf.Min(1, phase * 120) * Mathf.Exp(-phase * 7);
            float lead = Mathf.Sin(2 * Mathf.PI * f * phase) + .25f * Mathf.Sin(4 * Mathf.PI * f * phase);
            float kick = Mathf.Sin(2 * Mathf.PI * 65 * phase) * Mathf.Exp(-phase * 30);
            samples[i] = .14f * lead * envelope + .07f * Mathf.Sin(2 * Mathf.PI * b * t) * Mathf.Exp(-phase * 3) + .05f * kick;
        }
        return Clip("Original Pit Panic ragtime loop", samples);
    }

    private static AudioClip ComposeAmbience()
    {
        float[] samples = new float[22050 * 8];
        System.Random random = new System.Random(271);
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / 22050f;
            float envelope = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * 2.5f)), 3);
            samples[i] = .035f * envelope * (Mathf.Sin(t * 2 * Mathf.PI * (145 + 24 * Mathf.Sin(t * 5))) +
                .3f * Mathf.Sin(t * 2 * Mathf.PI * 280)) + (float)(random.NextDouble() - .5) * .003f;
        }
        return Clip("Original soft casino chatter", samples);
    }

    private static AudioClip ComposeCue(CasinoSoundCue cue)
    {
        float length = cue == CasinoSoundCue.Roulette ? 2 : cue == CasinoSoundCue.Success || cue == CasinoSoundCue.Celebrate ? .6f : .2f;
        float[] samples = new float[Mathf.CeilToInt(22050 * length)];
        System.Random random = new System.Random((int)cue + 91);
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / 22050f;
            float envelope = Mathf.Min(1, t * 300) * Mathf.Pow(1 - t / length, 2);
            float frequency = cue switch
            {
                CasinoSoundCue.Chip => 1600,
                CasinoSoundCue.Step => 110,
                CasinoSoundCue.Disappointed => 300 - t * 500,
                CasinoSoundCue.Mistake => 220,
                CasinoSoundCue.Warning => 660,
                CasinoSoundCue.Button => 880,
                _ => 520 * Mathf.Pow(1.25f, Mathf.Floor(t * 10))
            };
            float tone = Mathf.Sin(t * frequency * Mathf.PI * 2);
            if (cue == CasinoSoundCue.Card) tone = (float)(random.NextDouble() * 2 - 1);
            if (cue == CasinoSoundCue.Roulette) tone = Mathf.Sin(t * 4000) * Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * (100 - 30 * t))), 8);
            samples[i] = .22f * envelope * tone;
        }
        return Clip("Original casino " + cue, samples);
    }
}
