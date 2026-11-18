using System;
using UnityEngine;

public enum CasinoSoundCue { Card, Chip, Step, Celebrate, Disappointed, Success, Mistake, Warning, Roulette, Button, TaskFailed, Tip, Door, Entrance, FinalTick, TimeUp }
public enum CasinoMusicTrack { Title, Casino, ResultsPassed, ResultsFailed }

[DefaultExecutionOrder(-110)]
public sealed class CasinoSoundscape : MonoBehaviour
{
    private static CasinoSoundscape instance;
    private readonly AudioClip[] cues = new AudioClip[16];
    private readonly float[] lastPlayed = new float[16];
    private AudioSource music;
    private AudioClip titleTrack;
    private AudioClip casinoTrack;
    private AudioClip resultsPassedTrack;
    private AudioClip resultsFailedTrack;
    private AudioSource ambience;
    private AudioSource[] voices;
    private int voice;
    private float intensity;
    private Transform focus;
    private CasinoMusicTrack currentMusicTrack;
    private const float TitleMusicGain = .72f;
    private const float CasinoMusicGain = .12f;
    private const float ResultsMusicGain = .72f;
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
        resultsPassedTrack = Resources.Load<AudioClip>("Audio/Music/LevelComplete");
        resultsFailedTrack = Resources.Load<AudioClip>("Audio/Music/TheFinalSpin");
        if (titleTrack == null || casinoTrack == null || resultsPassedTrack == null || resultsFailedTrack == null)
            throw new InvalidOperationException("The title, casino, and results music clips must be present in Resources/Audio/Music.");
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
        float gain = currentMusicTrack switch
        {
            CasinoMusicTrack.Title => TitleMusicGain,
            CasinoMusicTrack.Casino => CasinoMusicGain,
            _ => ResultsMusicGain
        };
        music.volume = MusicVolume * gain;
        ambience.volume = AmbienceVolume;
        foreach (AudioSource source in voices) source.volume = EffectsVolume;
    }

    public static void SetMusicTrack(CasinoMusicTrack track)
    {
        if (instance == null || instance.currentMusicTrack == track && instance.music.isPlaying) return;
        instance.currentMusicTrack = track;
        instance.music.Stop();
        instance.music.clip = track switch
        {
            CasinoMusicTrack.Title => instance.titleTrack,
            CasinoMusicTrack.Casino => instance.casinoTrack,
            CasinoMusicTrack.ResultsPassed => instance.resultsPassedTrack,
            CasinoMusicTrack.ResultsFailed => instance.resultsFailedTrack,
            _ => throw new ArgumentOutOfRangeException(nameof(track), track, "Unknown casino music track.")
        };
        instance.music.loop = track == CasinoMusicTrack.Title || track == CasinoMusicTrack.Casino;
        instance.ApplyVolumes();
        instance.music.Play();
    }

    public static void PlayResultsMusic(bool passed) =>
        SetMusicTrack(passed ? CasinoMusicTrack.ResultsPassed : CasinoMusicTrack.ResultsFailed);

    public static void StopMusic()
    {
        if (instance != null) instance.music.Stop();
    }

    public static void SetDialogueMusic(bool active)
    {
        if (instance != null) instance.music.ignoreListenerPause = active;
    }

    public static void Play(CasinoSoundCue cue, Vector3 position, Transform owner = null, float gain = 1f)
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
        source.PlayOneShot(instance.cues[index], Mathf.Clamp01(gain) * (cue == CasinoSoundCue.Step ? .15f :
            Mathf.Clamp01(20 / Mathf.Max(20, distance))));
    }

    public static void SetIntensity(float value)
    {
        if (instance != null) instance.intensity = Mathf.Clamp01(value);
    }

    public static void PlayShiftFinale(bool impact, bool urgent)
    {
        if (instance == null || AudioListener.pause) return;
        // The global shift cue must remain audible while a minigame owns audio focus.
        AudioSource source = instance.voices[instance.voice++ % instance.voices.Length];
        source.pitch = urgent ? 1.18f : 1;
        source.panStereo = 0;
        source.PlayOneShot(instance.cues[(int)(impact ? CasinoSoundCue.TimeUp : CasinoSoundCue.FinalTick)],
            impact ? 1 : urgent ? .9f : .65f);
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
        float length = cue == CasinoSoundCue.Roulette ? 2 : cue == CasinoSoundCue.Success || cue == CasinoSoundCue.Celebrate ? .6f :
            cue == CasinoSoundCue.TaskFailed ? .7f : cue == CasinoSoundCue.Door ? 1.15f :
            cue == CasinoSoundCue.TimeUp ? .85f : cue == CasinoSoundCue.FinalTick ? .12f :
            cue == CasinoSoundCue.Entrance ? .55f : cue == CasinoSoundCue.Tip ? .45f : .2f;
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
                CasinoSoundCue.Door => 130 + 14 * Mathf.Sin(t * 6),
                CasinoSoundCue.Entrance => 440,
                CasinoSoundCue.FinalTick => 960,
                CasinoSoundCue.TimeUp => 110 - t * 65,
                // The success jingle climbs; a failed or missed task falls in low, buzzy steps.
                CasinoSoundCue.TaskFailed => 330 * Mathf.Pow(.8f, Mathf.Floor(t * 6)),
                // A tip is a bright two-note coin "ding-ding".
                CasinoSoundCue.Tip => t < .12f ? 1319 : 1760,
                _ => 520 * Mathf.Pow(1.25f, Mathf.Floor(t * 10))
            };
            float tone = Mathf.Sin(t * frequency * Mathf.PI * 2);
            if (cue == CasinoSoundCue.TimeUp)
                tone = tone * .8f + Mathf.Sin(t * 2 * Mathf.PI * 220) * Mathf.Exp(-t * 12) * .5f;
            if (cue == CasinoSoundCue.Door)
            {
                float noise = (float)(random.NextDouble() * 2 - 1);
                float latch = Mathf.Exp(-t * 40) * (noise * .65f + Mathf.Sin(t * 2300) * .35f);
                float hingeEnvelope = Mathf.Sin(Mathf.PI * t / length);
                float hingePhase = 2 * Mathf.PI * (72 * t - 18 * t * t + .12f * Mathf.Sin(t * 31));
                float hinge = Mathf.Sin(hingePhase) * .45f + Mathf.Sin(hingePhase * 3) * .18f;
                float thud = Mathf.Sin(2 * Mathf.PI * 58 * t) * Mathf.Exp(-t * 8) * .7f;
                float stopTime = Mathf.Max(0, t - .62f);
                float doorStop = t >= .62f
                    ? Mathf.Sin(2 * Mathf.PI * 82 * stopTime) * Mathf.Exp(-stopTime * 13) * .6f : 0;
                tone = latch + thud + doorStop + hingeEnvelope * (hinge + noise * .12f);
                samples[i] = Mathf.Min(1, t * 300) * Mathf.Clamp01((length - t) / .12f) * tone;
                continue;
            }
            if (cue == CasinoSoundCue.Entrance) tone *= .25f;
            if (cue == CasinoSoundCue.TaskFailed) tone = Mathf.Sign(tone) * .55f + tone * .45f;
            if (cue == CasinoSoundCue.Card) tone = (float)(random.NextDouble() * 2 - 1);
            if (cue == CasinoSoundCue.Roulette) tone = Mathf.Sin(t * 4000) * Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * (100 - 30 * t))), 8);
            samples[i] = .22f * envelope * tone;
        }
        if (cue == CasinoSoundCue.Door)
        {
            float peak = 0;
            foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
            for (int i = 0; i < samples.Length; i++)
                samples[i] *= .9f / peak;
        }
        return Clip("Original casino " + cue, samples);
    }
}
