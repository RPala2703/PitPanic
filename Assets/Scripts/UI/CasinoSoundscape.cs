using System;
using UnityEngine;

public enum CasinoSoundCue { Card, Chip, Step, Celebrate, Disappointed, Success, Mistake, Warning, Roulette, Button }
public enum CasinoMusicTrack { Title, Casino }

[DefaultExecutionOrder(-110)]
public sealed class CasinoSoundscape : MonoBehaviour
{
    private static CasinoSoundscape instance;
    private readonly AudioClip[] cues = new AudioClip[10];
    private readonly float[] lastPlayed = new float[10];
    private AudioSource music;
    private AudioClip titleTrack;
    private AudioClip casinoTrack;
    private AudioSource ambience;
    private AudioSource[] voices;
    private int voice;
    private float intensity;
    private Transform focus;
    private CasinoMusicTrack currentMusicTrack;
    private const float TitleMusicGain = .72f;
    private const float CasinoMusicGain = .12f;
    public static CasinoMusicTrack CurrentMusicTrack => instance != null ? instance.currentMusicTrack : CasinoMusicTrack.Title;
    public static Transform Focus => instance != null ? instance.focus : null;
    public static void SetFocus(Transform owner)
    {
        if (instance == null) return;
        instance.focus = owner;
        foreach (AudioSource source in instance.voices) source.Stop();
        instance.ambience.mute = owner != null;
    }
    public static void ReleaseFocus(Transform owner)
    {
        if (instance != null && instance.focus == owner) SetFocus(null);
    }
    public static bool AllowsEffect(Transform owner) => !AudioListener.pause &&
        (instance == null || instance.focus == null || instance.focus == owner);
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
        titleTrack = Resources.Load<AudioClip>("Audio/Music/PitPanicWelcome");
        casinoTrack = Resources.Load<AudioClip>("Audio/Music/PixelatedRoulette");
        if (titleTrack == null || casinoTrack == null)
            throw new InvalidOperationException("The title and casino music clips must be present in Resources/Audio/Music.");
        music.loop = true;
        music.clip = titleTrack;
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
        music.volume = MusicVolume * (currentMusicTrack == CasinoMusicTrack.Title ? TitleMusicGain : CasinoMusicGain);
        ambience.volume = AmbienceVolume;
        foreach (AudioSource source in voices) source.volume = EffectsVolume;
    }

    public static void SetMusicTrack(CasinoMusicTrack track)
    {
        if (instance == null || instance.currentMusicTrack == track && instance.music.isPlaying) return;
        instance.currentMusicTrack = track;
        instance.music.Stop();
        instance.music.clip = track == CasinoMusicTrack.Title ? instance.titleTrack : instance.casinoTrack;
        instance.ApplyVolumes();
        instance.music.Play();
    }

    public static void Play(CasinoSoundCue cue, Vector3 position, Transform owner = null)
    {
        if (instance == null || !AllowsEffect(owner)) return;
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
        if (AudioListener.pause) return;
        music.pitch = Mathf.MoveTowards(music.pitch, 1 + intensity * .08f, Time.unscaledDeltaTime * .015f);
        ambience.volume = Mathf.MoveTowards(ambience.volume, AmbienceVolume * (1 + intensity * .2f), Time.unscaledDeltaTime * .02f);
    }

    private static AudioClip Clip(string name, float[] samples)
    {
        AudioClip clip = AudioClip.Create(name, samples.Length, 1, 22050, false);
        clip.SetData(samples, 0);
        return clip;
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
