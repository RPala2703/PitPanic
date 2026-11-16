using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CasinoPortraitExtraction
{
    private const string Source = "Assets/Resources/Portraits/StaffPortraits.png";
    private const string Destination = "Assets/Resources/Portraits/Staff/";
    private static readonly int[] Columns = { 0, 252, 488, 728, 966, 1200, 1452 };
    private static readonly int[] Rows = { 0, 230, 459, 688, 920, 1150 };

    [MenuItem("Pit Panic/Art/Extract Clean Dialogue Portraits")]
    public static void Generate()
    {
        Process(true);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("Extracted 30 clean dialogue portraits from the original staff sheet.");
    }

    public static void Validate() => Process(false);

    private static void Process(bool write)
    {
        Texture2D sheet = new Texture2D(2, 2);
        try
        {
            if (!sheet.LoadImage(File.ReadAllBytes(Source)) || sheet.width != 1452 || sheet.height != 1150)
                throw new InvalidOperationException("The original staff portrait sheet has unexpected dimensions.");
            Color32[] pixels = sheet.GetPixels32();
            foreach (DialogueSpeaker speaker in Enum.GetValues(typeof(DialogueSpeaker)))
            foreach (DialogueExpression expression in Enum.GetValues(typeof(DialogueExpression)))
            {
                int row = (int)speaker;
                int column = (int)expression;
                int left = Columns[column + 1], right = Columns[column] - 1;
                int top = Rows[row + 1], bottom = Rows[row] - 1;
                // This sheet has uneven gutters; coordinates are measured from its top left.
                for (int y = Rows[row]; y < Rows[row + 1]; y++)
                for (int x = Columns[column]; x < Columns[column + 1]; x++)
                    if (pixels[(sheet.height - 1 - y) * sheet.width + x].a != 0)
                    {
                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                        top = Math.Min(top, y);
                        bottom = Math.Max(bottom, y);
                    }
                int width = right - left + 1, height = bottom - top + 1;
                if (width <= 0 || height <= 0 || width > 244 || height > 244)
                    throw new InvalidOperationException($"Invalid portrait crop: {speaker}/{expression}.");
                Color32[] expected = new Color32[256 * 256];
                int offsetX = (256 - width) / 2;
                for (int y = top; y <= bottom; y++)
                for (int x = left; x <= right; x++)
                    expected[(6 + bottom - y) * 256 + offsetX + x - left] =
                        pixels[(sheet.height - 1 - y) * sheet.width + x];
                string path = Destination + $"{speaker}_{expression}.png";
                Texture2D portrait = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                try
                {
                    if (write)
                    {
                        portrait.SetPixels32(expected);
                        portrait.Apply();
                        File.WriteAllBytes(path, portrait.EncodeToPNG());
                    }
                    else
                    {
                        if (!portrait.LoadImage(File.ReadAllBytes(path)) ||
                            portrait.width != 256 || portrait.height != 256)
                            throw new InvalidOperationException("Invalid portrait image: " + path);
                        Color32[] actual = portrait.GetPixels32();
                        for (int i = 0; i < expected.Length; i++)
                            if (!actual[i].Equals(expected[i]))
                                throw new InvalidOperationException($"Portrait differs from its clean source crop: {path}, pixel {i}.");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(portrait); }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(sheet); }
    }
}
