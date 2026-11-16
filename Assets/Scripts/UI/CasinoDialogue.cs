using System;
using UnityEngine;
using UnityEngine.UI;

public enum DialogueSpeaker { Penguin, Tiger, Bear, Fox, Pig }
public enum DialogueExpression { Neutral, Happy, Determined, Shocked, Angry, Smug }

public readonly struct DialogueLine
{
    public readonly DialogueSpeaker Speaker;
    public readonly DialogueExpression Expression;
    public readonly string Text;
    public readonly float PauseBefore;
    public DialogueLine(DialogueSpeaker speaker, DialogueExpression expression, string text, float pauseBefore = 0f)
    { Speaker = speaker; Expression = expression; Text = text; PauseBefore = pauseBefore; }
}

public sealed class CasinoDialogue : MonoBehaviour
{
    private const float LettersPerSecond = 35f;
    private static Sprite[,] portraits;
    private Text nameLabel;
    private Text message;
    private Text indicator;
    private Image portrait;
    private DialogueLine[] lines;
    private int index;
    private float visible;
    private int sounded;
    private Action finished;
    private AudioSource voice;
    private AudioClip bleep;
    private RectTransform card;
    private float openedAt;
    private float lineStartedAt;
    public float VoiceGain { get; set; } = .32f;

    public bool IsComplete => lines != null && visible >= lines[index].Text.Length;
    public int LineIndex => index;
    public string FullText => lines != null ? lines[index].Text : "";
    public static Sprite Portrait(DialogueSpeaker speaker, DialogueExpression expression)
    {
        if (portraits == null)
        {
            portraits = new Sprite[5, 6];
            for (int row = 0; row < 5; row++)
            for (int col = 0; col < 6; col++)
            {
                string path = $"Portraits/Staff/{(DialogueSpeaker)row}_{(DialogueExpression)col}";
                portraits[row, col] = Resources.Load<Sprite>(path);
                if (portraits[row, col] == null)
                    throw new InvalidOperationException("Missing staff dialogue portrait: " + path);
            }
        }
        return portraits[(int)speaker, (int)expression];
    }

    public void Configure(RectTransform panelCard, Image portraitImage, Text speakerName, Text text, Text nextIndicator)
    {
        card = panelCard;
        portrait = portraitImage;
        portrait.type = Image.Type.Simple;
        portrait.useSpriteMesh = false;
        portrait.preserveAspect = true;
        nameLabel = speakerName;
        message = text;
        indicator = nextIndicator;
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.ignoreListenerPause = true;
        voice.spatialBlend = 0;
        const int rate = 22050;
        float[] samples = new float[440];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = Mathf.Sin(2 * Mathf.PI * 560 * i / rate) * Mathf.Sin(Mathf.PI * i / samples.Length) * .17f;
        bleep = AudioClip.Create("Dialogue voice", samples.Length, 1, rate, false);
        bleep.SetData(samples, 0);
    }

    public void Begin(DialogueLine[] conversation, Action onFinished)
    {
        if (conversation == null || conversation.Length == 0) throw new ArgumentException("Dialogue requires at least one line.");
        lines = conversation;
        finished = onFinished;
        index = 0;
        openedAt = Time.unscaledTime;
        ShowLine();
    }

    private void ShowLine()
    {
        DialogueLine line = lines[index];
        portrait.sprite = Portrait(line.Speaker, line.Expression);
        portrait.overrideSprite = null;
        portrait.SetAllDirty();
        nameLabel.text = line.Speaker.ToString().ToUpperInvariant();
        message.text = "";
        indicator.enabled = false;
        visible = 0;
        sounded = 0;
        lineStartedAt = Time.unscaledTime;
    }

    private void Update()
    {
        if (lines == null) return;
        float scale = Mathf.SmoothStep(.95f, 1f, Mathf.Clamp01((Time.unscaledTime - openedAt) / .16f));
        card.localScale = Vector3.one * scale;
        if (Time.unscaledTime - lineStartedAt < lines[index].PauseBefore) return;
        visible = Mathf.Min(lines[index].Text.Length, visible + Time.unscaledDeltaTime * LettersPerSecond);
        int count = Mathf.FloorToInt(visible);
        string text = lines[index].Text;
        message.text = text.Substring(0, count);
        for (int i = sounded; i < count; i++)
            if (char.IsLetterOrDigit(text[i]))
            {
                voice.pitch = 0.84f + (int)lines[index].Speaker * .105f;
                voice.PlayOneShot(bleep, CasinoSoundscape.EffectsVolume * VoiceGain);
            }
        sounded = count;
        indicator.enabled = IsComplete;
    }

    public void Advance()
    {
        if (lines == null) return;
        if (!IsComplete)
        {
            visible = lines[index].Text.Length;
            sounded = lines[index].Text.Length;
            message.text = lines[index].Text;
            indicator.enabled = true;
        }
        else if (++index < lines.Length) ShowLine();
        else
        {
            Action callback = finished;
            Stop();
            callback?.Invoke();
        }
    }

    public void Stop()
    {
        lines = null;
        finished = null;
        if (voice != null) voice.Stop();
    }

    private void OnDestroy()
    {
        if (bleep != null) Destroy(bleep);
    }
}
