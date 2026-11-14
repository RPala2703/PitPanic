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
    public DialogueLine(DialogueSpeaker speaker, DialogueExpression expression, string text)
    { Speaker = speaker; Expression = expression; Text = text; }
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

    public bool IsComplete => lines != null && visible >= lines[index].Text.Length;
    public int LineIndex => index;
    public string FullText => lines != null ? lines[index].Text : "";
    public static Sprite Portrait(DialogueSpeaker speaker, DialogueExpression expression)
    {
        if (portraits == null)
        {
            Texture2D sheet = Resources.Load<Texture2D>("Portraits/StaffPortraits");
            if (sheet == null) throw new InvalidOperationException("Missing staff dialogue portrait sheet.");
            portraits = new Sprite[5, 6];
            for (int row = 0; row < 5; row++)
            for (int col = 0; col < 6; col++)
            {
                int left = Mathf.RoundToInt(col * sheet.width / 6f);
                int right = Mathf.RoundToInt((col + 1) * sheet.width / 6f);
                int top = Mathf.RoundToInt(row * sheet.height / 5f);
                int bottom = Mathf.RoundToInt((row + 1) * sheet.height / 5f);
                portraits[row, col] = Sprite.Create(sheet, new Rect(left, sheet.height - bottom,
                    right - left, bottom - top), new Vector2(.5f, .5f), 100f);
            }
        }
        return portraits[(int)speaker, (int)expression];
    }

    public void Configure(RectTransform panelCard, Image portraitImage, Text speakerName, Text text, Text nextIndicator)
    {
        card = panelCard;
        portrait = portraitImage;
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
        nameLabel.text = line.Speaker.ToString().ToUpperInvariant();
        message.text = "";
        indicator.enabled = false;
        visible = 0;
        sounded = 0;
    }

    private void Update()
    {
        if (lines == null) return;
        float scale = Mathf.SmoothStep(.95f, 1f, Mathf.Clamp01((Time.unscaledTime - openedAt) / .16f));
        card.localScale = Vector3.one * scale;
        visible = Mathf.Min(lines[index].Text.Length, visible + Time.unscaledDeltaTime * LettersPerSecond);
        int count = Mathf.FloorToInt(visible);
        string text = lines[index].Text;
        message.text = text.Substring(0, count);
        for (int i = sounded; i < count; i++)
            if (char.IsLetterOrDigit(text[i]))
            {
                voice.pitch = 0.84f + (int)lines[index].Speaker * .105f;
                voice.PlayOneShot(bleep, CasinoSoundscape.EffectsVolume * .32f);
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
