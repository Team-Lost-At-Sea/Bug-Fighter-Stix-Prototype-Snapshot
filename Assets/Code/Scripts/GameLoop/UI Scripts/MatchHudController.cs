using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MatchHudController : MonoBehaviour
{
    private static Sprite fallbackHudSprite;

    [Header("Health")]
    [SerializeField]
    private Image player1HealthFill;

    [SerializeField]
    private Image player2HealthFill;

    [Header("Text")]
    [SerializeField]
    private TMP_Text timerLabel;

    [SerializeField]
    private TMP_Text roundScoreLabel;

    [SerializeField]
    private TMP_Text centerBannerLabel;

    [SerializeField]
    private TMP_Text player1ResultLabel;

    [SerializeField]
    private TMP_Text player2ResultLabel;

    private int maxHealth = 1000;
    private bool useRoundTimer = true;
    private bool initialized;

    public void Initialize(MatchConfig matchConfig)
    {
        maxHealth = matchConfig != null ? Mathf.Max(1, matchConfig.roundStartHealth) : 1000;
        useRoundTimer = matchConfig == null || matchConfig.useRoundTimer;
        EnsureHudReferences();
        ConfigureHealthFillImages();
        initialized = true;
        HideOutcomeLabels();
    }

    public void Render(Simulation simulation)
    {
        if (simulation == null)
            return;

        if (!initialized)
            Initialize(null);

        EnsureHudReferences();
        ConfigureHealthFillImages();
        float maxHealthSafe = Mathf.Max(1f, maxHealth);
        if (player1HealthFill != null)
            player1HealthFill.fillAmount = Mathf.Clamp01(simulation.Player1.Health / maxHealthSafe);
        if (player2HealthFill != null)
            player2HealthFill.fillAmount = Mathf.Clamp01(simulation.Player2.Health / maxHealthSafe);

        if (roundScoreLabel != null)
            roundScoreLabel.text = $"P1 {simulation.Player1RoundWins} - {simulation.Player2RoundWins} P2   R{simulation.CurrentRoundNumber}";

        if (timerLabel != null)
            timerLabel.text = BuildTimerLabel(simulation);

        UpdateOutcomeLabels(simulation);
    }

    private string BuildTimerLabel(Simulation simulation)
    {
        if (!useRoundTimer || !simulation.IsRoundTimerEnabled)
            return "--";

        int framesRemaining = Mathf.Max(0, simulation.RoundTimerFramesRemaining);
        int ticksPerSecond = Mathf.Max(1, SimulationTime.TicksPerSecond);
        int secondsRemaining = Mathf.CeilToInt(framesRemaining / (float)ticksPerSecond);
        return secondsRemaining.ToString("00");
    }

    private void UpdateOutcomeLabels(Simulation simulation)
    {
        if (simulation.RoundPhase == RoundPhase.Fighting)
        {
            HideOutcomeLabels();
            return;
        }

        if (centerBannerLabel != null)
        {
            if (simulation.RoundPhase == RoundPhase.MatchOver)
            {
                centerBannerLabel.gameObject.SetActive(true);
                centerBannerLabel.text = simulation.MatchWinner == MatchWinner.Player1
                    ? "PLAYER 1 WINS"
                    : "PLAYER 2 WINS";
            }
            else
            {
                centerBannerLabel.gameObject.SetActive(true);
                switch (simulation.LastRoundEndType)
                {
                    case RoundEndType.KO:
                        centerBannerLabel.text = "KO";
                        break;
                    case RoundEndType.TimeOut:
                        centerBannerLabel.text = "TIME OUT";
                        break;
                    default:
                        centerBannerLabel.text = "DRAW";
                        break;
                }
            }
        }

        if (simulation.RoundPhase == RoundPhase.MatchOver)
        {
            if (player1ResultLabel != null)
            {
                player1ResultLabel.gameObject.SetActive(true);
                player1ResultLabel.text = simulation.MatchWinner == MatchWinner.Player1 ? "WIN" : "LOSE";
            }

            if (player2ResultLabel != null)
            {
                player2ResultLabel.gameObject.SetActive(true);
                player2ResultLabel.text = simulation.MatchWinner == MatchWinner.Player2 ? "WIN" : "LOSE";
            }
        }
        else
        {
            if (player1ResultLabel != null)
                player1ResultLabel.gameObject.SetActive(false);
            if (player2ResultLabel != null)
                player2ResultLabel.gameObject.SetActive(false);
        }
    }

    private void HideOutcomeLabels()
    {
        if (centerBannerLabel != null)
            centerBannerLabel.gameObject.SetActive(false);
        if (player1ResultLabel != null)
            player1ResultLabel.gameObject.SetActive(false);
        if (player2ResultLabel != null)
            player2ResultLabel.gameObject.SetActive(false);
    }

    private void EnsureHudReferences()
    {
        bool allAssigned = player1HealthFill != null
            && player2HealthFill != null
            && timerLabel != null
            && roundScoreLabel != null
            && centerBannerLabel != null
            && player1ResultLabel != null
            && player2ResultLabel != null;
        if (allAssigned)
            return;

        bool anyAssigned = player1HealthFill != null
            || player2HealthFill != null
            || timerLabel != null
            || roundScoreLabel != null
            || centerBannerLabel != null
            || player1ResultLabel != null
            || player2ResultLabel != null;
        if (anyAssigned)
            return;

        GameObject root = new GameObject("MatchHudCanvas");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = RenderOrder.UI.Hud;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();

        CreateHealthBar(root.transform, true, out player1HealthFill);
        CreateHealthBar(root.transform, false, out player2HealthFill);
        timerLabel = CreateLabel(root.transform, "TimerLabel", new Vector2(0.5f, 1f), new Vector2(0f, -32f), 46, TextAlignmentOptions.Center, Color.white);
        roundScoreLabel = CreateLabel(root.transform, "RoundScoreLabel", new Vector2(0.5f, 1f), new Vector2(0f, -76f), 30, TextAlignmentOptions.Center, Color.white);
        centerBannerLabel = CreateLabel(root.transform, "CenterBannerLabel", new Vector2(0.5f, 0.58f), Vector2.zero, 72, TextAlignmentOptions.Center, new Color(1f, 0.9f, 0.2f, 1f));
        player1ResultLabel = CreateLabel(root.transform, "Player1ResultLabel", new Vector2(0.2f, 0.82f), Vector2.zero, 48, TextAlignmentOptions.Center, Color.white);
        player2ResultLabel = CreateLabel(root.transform, "Player2ResultLabel", new Vector2(0.8f, 0.82f), Vector2.zero, 48, TextAlignmentOptions.Center, Color.white);
    }

    private static void CreateHealthBar(Transform root, bool player1Side, out Image fillImage)
    {
        GameObject backgroundObject = new GameObject(player1Side ? "P1HealthBackground" : "P2HealthBackground");
        backgroundObject.transform.SetParent(root, false);
        Image backgroundImage = backgroundObject.AddComponent<Image>();
        EnsureImageSprite(backgroundImage);
        backgroundImage.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        RectTransform backgroundRect = backgroundImage.rectTransform;
        backgroundRect.anchorMin = player1Side ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
        backgroundRect.anchorMax = player1Side ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
        backgroundRect.pivot = player1Side ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
        backgroundRect.anchoredPosition = player1Side ? new Vector2(28f, -20f) : new Vector2(-28f, -20f);
        backgroundRect.sizeDelta = new Vector2(420f, 28f);

        GameObject fillObject = new GameObject(player1Side ? "P1HealthFill" : "P2HealthFill");
        fillObject.transform.SetParent(backgroundObject.transform, false);
        fillImage = fillObject.AddComponent<Image>();
        EnsureImageSprite(fillImage);
        fillImage.color = player1Side ? new Color(0.08f, 0.82f, 0.18f, 1f) : new Color(0.8f, 0.2f, 0.2f, 1f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = player1Side ? 0 : 1;
        fillImage.fillAmount = 1f;
        RectTransform fillRect = fillImage.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);
    }

    private void ConfigureHealthFillImages()
    {
        ConfigureHealthFillImage(player1HealthFill, true);
        ConfigureHealthFillImage(player2HealthFill, false);
    }

    private static void ConfigureHealthFillImage(Image fillImage, bool player1Side)
    {
        if (fillImage == null)
            return;

        EnsureImageSprite(fillImage);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = player1Side ? 0 : 1;
    }

    private static void EnsureImageSprite(Image image)
    {
        if (image == null || image.sprite != null)
            return;

        if (fallbackHudSprite == null)
        {
            Texture2D texture = Texture2D.whiteTexture;
            fallbackHudSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
        }

        image.sprite = fallbackHudSprite;
    }

    private static TMP_Text CreateLabel(
        Transform root,
        string objectName,
        Vector2 anchor,
        Vector2 anchoredPosition,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color
    )
    {
        GameObject labelObject = new GameObject(objectName);
        labelObject.transform.SetParent(root, false);
        TMP_Text label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = string.Empty;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(900f, 120f);
        return label;
    }
}
