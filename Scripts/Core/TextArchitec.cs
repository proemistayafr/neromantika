using System.Collections;
using UnityEngine;
using TMPro;

public class TextArchitec
{
    // Fields to store TextMeshPro components
    private TextMeshProUGUI tmpro_ui;
    private TextMeshPro tmpro_world;

    // Property to return the appropriate TextMeshPro component
    public TMP_Text tmpro => tmpro_ui != null ? tmpro_ui : tmpro_world;

    // Properties to manage text content
    public string currentText => tmpro.text;
    public string targetText { get; private set; } = "";
    public string preText { get; private set; } = "";
    private int preTextLength = 0;

    // Property to return the full target text (preText + targetText)
    public string fullTargetText => preText + targetText;

    // Enumeration to represent different text build methods
    public enum BuildMethod { instant, typewriter, fade }
    public BuildMethod buildMethod = BuildMethod.typewriter;

    // Property to get/set text color
    public Color textColor { get { return tmpro.color; } set { tmpro.color = value; } }

    // Property to calculate the effective animation speed
    public float speed { get { return baseSpeed * speedMultiplier; } set { speedMultiplier = value; } }
    private const float baseSpeed = 1;
    private float speedMultiplier = 1;

    // Property to determine the number of characters to reveal per cycle
    public int charactersPerCycle { get { return speed <= 2f ? characterMultiplier : speed <= 2.5f ? characterMultiplier * 2 : characterMultiplier * 3; } }
    private int characterMultiplier = 1;

    // Flag to speed up the animation
    public bool hurryUp = false;

    // Constructor for UI text
    public TextArchitec(TextMeshProUGUI tmpro_ui)
    {
        this.tmpro_ui = tmpro_ui;
    }

    // Constructor for world space text
    public TextArchitec(TextMeshPro tmpro_world)
    {
        this.tmpro_world = tmpro_world;
    }

    // Coroutine to start building text
    public Coroutine Build(string text)
    {
        preText = "";
        targetText = text;

        // Stop any ongoing text building process
        Stop();

        // Start the text building process
        buildProcess = tmpro.StartCoroutine(Building());
        return buildProcess;
    }

    // Coroutine to append text to the existing text
    public Coroutine Append(string text)
    {
        preText = tmpro.text;
        targetText = text;

        // Stop any ongoing text building process
        Stop();

        // Start the text building process
        buildProcess = tmpro.StartCoroutine(Building());
        return buildProcess;
    }

    // Coroutine to handle the text building process
    private Coroutine buildProcess = null;

    // Property to check if text is currently being built
    public bool isBuilding => buildProcess != null;

    // Method to stop the text building process
    public void Stop()
    {
        if (!isBuilding)
            return;

        tmpro.StopCoroutine(buildProcess);
        buildProcess = null;
    }

    // Coroutine to handle the actual text animation
    IEnumerator Building()
    {
        // Prepare the text based on the selected build method
        Prepare();

        switch (buildMethod)
        {
            case BuildMethod.typewriter:
                yield return Build_Typewriter();
                break;
            case BuildMethod.fade:
                yield return Build_Fade();
                break;
        }

        // Animation completed, perform necessary actions
        OnComplete();
    }

    // Method to handle actions after animation completion
    private void OnComplete()
    {
        buildProcess = null;
        hurryUp = false;
    }

    // Method to force complete the text animation instantly
    public void ForceComplete()
    {
        switch (buildMethod)
        {
            case BuildMethod.typewriter:
                tmpro.maxVisibleCharacters = tmpro.textInfo.characterCount;
                break;
            case BuildMethod.fade:
                tmpro.ForceMeshUpdate();
                break;
        }

        Stop();
        OnComplete();
    }

    // Method to prepare the text for animation based on the selected build method
    private void Prepare()
    {
        switch (buildMethod)
        {
            case BuildMethod.instant:
                Prepare_Instant();
                break;
            case BuildMethod.typewriter:
                Prepare_Typewriter();
                break;
            case BuildMethod.fade:
                Prepare_Fade();
                break;
        }
    }

    // Method to prepare the text for instant display
    private void Prepare_Instant()
    {
        tmpro.color = tmpro.color;
        tmpro.text = fullTargetText;
        tmpro.ForceMeshUpdate();
        tmpro.maxVisibleCharacters = tmpro.textInfo.characterCount;
    }

    // Method to prepare the text for typewriter-style animation
    private void Prepare_Typewriter()
    {
        tmpro.color = tmpro.color;
        tmpro.maxVisibleCharacters = 0;
        tmpro.text = preText;

        if (preText != "")
        {
            tmpro.ForceMeshUpdate();
            tmpro.maxVisibleCharacters = tmpro.textInfo.characterCount;
        }

        tmpro.text += targetText;
        tmpro.ForceMeshUpdate();
    }

    // Method to prepare the text for fade-in animation
    private void Prepare_Fade()
    {
        tmpro.text = preText;
        if (preText != "")
        {
            tmpro.ForceMeshUpdate();
            preTextLength = tmpro.textInfo.characterCount;
        }
        else
            preTextLength = 0;

        tmpro.text += targetText;
        tmpro.maxVisibleCharacters = int.MaxValue;
        tmpro.ForceMeshUpdate();

        TMP_TextInfo textInfo = tmpro.textInfo;

        Color colorVisible = new Color(textColor.r, textColor.g, textColor.b, 1);
        Color colorHidden = new Color(textColor.r, textColor.g, textColor.b, 0);

        Color32[] vertexColors = textInfo.meshInfo[textInfo.characterInfo[0].materialReferenceIndex].colors32;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];

            if (!charInfo.isVisible)
                continue;

            if (i < preTextLength)
            {
                for (int v = 0; v < 4; v++)
                    vertexColors[charInfo.vertexIndex + v] = colorVisible;
            }
            else
            {
                for (int v = 0; v < 4; v++)
                    vertexColors[charInfo.vertexIndex + v] = colorHidden;
            }
        }

        tmpro.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    // Coroutine for typewriter-style text animation
    private IEnumerator Build_Typewriter()
    {
        while (tmpro.maxVisibleCharacters < tmpro.textInfo.characterCount)
        {
            tmpro.maxVisibleCharacters += hurryUp ? charactersPerCycle * 5 : charactersPerCycle;

            yield return new WaitForSeconds(0.015f / speed);
        }
    }

    // Coroutine for fade-in text animation
    private IEnumerator Build_Fade()
    {
        int minRange = preTextLength;
        int maxRange = minRange + 1;

        byte alphaThreshold = 15;

        TMP_TextInfo textInfo = tmpro.textInfo;

        Color32[] vertexColors = textInfo.meshInfo[textInfo.characterInfo[0].materialReferenceIndex].colors32;
        float[] alphas = new float[textInfo.characterCount];

        while (true)
        {
            float fadeSpeed = ((hurryUp ? charactersPerCycle * 5 : charactersPerCycle) * speed) * 4f;

            for (int i = minRange; i < maxRange; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];

                if (!charInfo.isVisible)
                    continue;

                int vertexIndex = textInfo.characterInfo[i].vertexIndex;
                alphas[i] = Mathf.MoveTowards(alphas[i], 255, fadeSpeed);

                for (int v = 0; v < 4; v++)
                    vertexColors[charInfo.vertexIndex + v].a = (byte)alphas[i];

                if (alphas[i] >= 255)
                    minRange++;
            }

            tmpro.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);

            bool lastCharacterIsInvisible = !textInfo.characterInfo[maxRange - 1].isVisible;

            if (alphas[maxRange - 1] > alphaThreshold || lastCharacterIsInvisible)
            {
                if (maxRange < textInfo.characterCount)
                    maxRange++;
                else if (alphas[maxRange - 1] >= 255 || lastCharacterIsInvisible)
                    break;
            }
            yield return new WaitForEndOfFrame();
        }
    }
}

















