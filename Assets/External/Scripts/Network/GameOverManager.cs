using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(PhotonView))]
public class GameOverManager : MonoBehaviourPunCallbacks
{
    [Header("Scene")]
    [SerializeField] private string titleSceneName = "Title";

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    [Header("UI Text")]
    [SerializeField] private string winnerSuffix = " Wins!";
    [SerializeField] private string noWinnerMessage = "Game Over";
    [SerializeField] private string returnButtonLabel = "Return To Title";

    [Header("UI Style")]
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.82f);
    [SerializeField] private Color winnerTextColor = Color.white;
    [SerializeField] private Color buttonColor = new Color(1f, 1f, 1f, 0.92f);
    [SerializeField] private Color buttonTextColor = Color.black;
    [SerializeField] private Vector2 panelSize = new Vector2(720f, 360f);
    [SerializeField] private int sortingOrder = 5000;

    private readonly HashSet<PlayerHealth> trackedHealths = new();

    private bool isGameEnded;
    private bool isLeavingRoom;
    private Canvas gameOverCanvas;
    private GameObject panelRoot;
    private TMP_Text winnerText;
    private Button returnButton;

    private void Awake()
    {
        if (!enableDebugLogs)
            return;

        Debug.Log(
            $"[GameOverManager:{name}] Awake | scene={gameObject.scene.name} | hasPhotonView={photonView != null} | viewId={(photonView != null ? photonView.ViewID : -1)} | inRoom={PhotonNetwork.InRoom}",
            this);
    }

    public override void OnEnable()
    {
        base.OnEnable();

        PlayerHealth.Registered += HandlePlayerHealthRegistered;
        PlayerHealth.Unregistered += HandlePlayerHealthUnregistered;
        TrackExistingPlayerHealths();
    }

    public override void OnDisable()
    {
        base.OnDisable();

        PlayerHealth.Registered -= HandlePlayerHealthRegistered;
        PlayerHealth.Unregistered -= HandlePlayerHealthUnregistered;
        UntrackAllHealths();
        ResetLocalPauseState();
    }

    private void HandlePlayerHealthRegistered(PlayerHealth playerHealth)
    {
        RegisterHealth(playerHealth);
    }

    private void HandlePlayerHealthUnregistered(PlayerHealth playerHealth)
    {
        UnregisterHealth(playerHealth);
    }

    private void HandleHealthChanged(PlayerHealth playerHealth, HealthChangedEventArgs args)
    {
        if (isGameEnded || playerHealth == null || !args.IsDead || args.WasDead)
            return;

        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
            return;

        int defeatedActorNumber = playerHealth.OwnerActorNumber;
        int winnerActorNumber = defeatedActorNumber;
        string winnerName = ResolveWinnerName(winnerActorNumber);

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[GameOverManager:{name}] HandleHealthChanged -> defeated={playerHealth.name} actor={defeatedActorNumber} winnerActor={winnerActorNumber} winnerName={winnerName} | localIsMaster={PhotonNetwork.IsMasterClient} | photonViewId={(photonView != null ? photonView.ViewID : -1)}",
                this);
        }

        BroadcastGameOver(winnerActorNumber, defeatedActorNumber, winnerName);
    }

    private string ResolveWinnerName(int winnerActorNumber)
    {
        if (winnerActorNumber > 0 && PhotonNetwork.CurrentRoom != null)
        {
            Player player = PhotonNetwork.CurrentRoom.GetPlayer(winnerActorNumber);
            if (player != null && !string.IsNullOrWhiteSpace(player.NickName))
                return player.NickName;
        }

        foreach (PlayerHealth trackedHealth in trackedHealths)
        {
            if (trackedHealth == null || trackedHealth.OwnerActorNumber != winnerActorNumber)
                continue;

            PhotonView view = trackedHealth.GetComponent<PhotonView>();
            if (view != null && view.Owner != null && !string.IsNullOrWhiteSpace(view.Owner.NickName))
                return view.Owner.NickName;
        }

        return string.Empty;
    }

    private void BroadcastGameOver(int winnerActorNumber, int defeatedActorNumber, string winnerName)
    {
        if (PhotonNetwork.InRoom)
        {
            if (photonView == null || photonView.ViewID == 0)
            {
                Debug.LogWarning(
                    $"[GameOverManager:{name}] Cannot send {nameof(RPC_AnnounceGameOver)} because PhotonView is invalid. " +
                    $"viewId={(photonView != null ? photonView.ViewID : -1)} | scene={gameObject.scene.name} | activeInHierarchy={gameObject.activeInHierarchy} | " +
                    $"winnerActor={winnerActorNumber} | defeatedActor={defeatedActorNumber} | inRoom={PhotonNetwork.InRoom} | isMaster={PhotonNetwork.IsMasterClient}. " +
                    "This usually means the component was added at runtime or the scene object PhotonView is not registered.",
                    this);
                return;
            }

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[GameOverManager:{name}] Broadcasting {nameof(RPC_AnnounceGameOver)} via viewId={photonView.ViewID} | winnerActor={winnerActorNumber} | defeatedActor={defeatedActorNumber} | winnerName={winnerName}",
                    this);
            }

            photonView.RPC(
                nameof(RPC_AnnounceGameOver),
                RpcTarget.All,
                winnerActorNumber,
                defeatedActorNumber,
                winnerName ?? string.Empty);
            return;
        }

        RPC_AnnounceGameOver(winnerActorNumber, defeatedActorNumber, winnerName ?? string.Empty);
    }

    [PunRPC]
    private void RPC_AnnounceGameOver(int winnerActorNumber, int defeatedActorNumber, string winnerName)
    {
        if (isGameEnded)
            return;

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[GameOverManager:{name}] RPC_AnnounceGameOver received | localActor={(PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1)} | winnerActor={winnerActorNumber} | defeatedActor={defeatedActorNumber} | winnerName={winnerName}",
                this);
        }

        isGameEnded = true;
        Time.timeScale = 0f;

        EnsureRuntimeUi();
        ShowWinnerText(winnerActorNumber, defeatedActorNumber, winnerName);
        SetCursorVisible(true);
    }

    public void ReturnToTitle()
    {
        if (isLeavingRoom)
            return;

        isLeavingRoom = true;
        ResetLocalPauseState();

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            return;
        }

        SceneManager.LoadScene(titleSceneName);
    }

    public override void OnLeftRoom()
    {
        ResetLocalPauseState();
        isLeavingRoom = false;

        if (!string.IsNullOrWhiteSpace(titleSceneName))
        {
            SceneManager.LoadScene(titleSceneName);
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        ResetLocalPauseState();
        isLeavingRoom = false;
    }

    private void EnsureRuntimeUi()
    {
        if (gameOverCanvas != null)
        {
            if (panelRoot != null)
                panelRoot.SetActive(true);
            return;
        }

        TMP_FontAsset fontAsset = TMP_Settings.defaultFontAsset;

        GameObject canvasObject = new GameObject("RuntimeGameOverCanvas");
        gameOverCanvas = canvasObject.AddComponent<Canvas>();
        gameOverCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        gameOverCanvas.sortingOrder = sortingOrder;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        panelRoot = panelObject;

        RectTransform panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = panelSize;

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = panelColor;

        GameObject winnerTextObject = new GameObject("WinnerText");
        winnerTextObject.transform.SetParent(panelObject.transform, false);
        winnerText = winnerTextObject.AddComponent<TextMeshProUGUI>();
        winnerText.font = fontAsset;
        winnerText.alignment = TextAlignmentOptions.Center;
        winnerText.fontSize = 42f;
        winnerText.color = winnerTextColor;

        RectTransform winnerRect = winnerText.rectTransform;
        winnerRect.anchorMin = new Vector2(0.1f, 0.45f);
        winnerRect.anchorMax = new Vector2(0.9f, 0.85f);
        winnerRect.offsetMin = Vector2.zero;
        winnerRect.offsetMax = Vector2.zero;

        GameObject buttonObject = new GameObject("ReturnButton");
        buttonObject.transform.SetParent(panelObject.transform, false);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = buttonColor;
        returnButton = buttonObject.AddComponent<Button>();
        returnButton.targetGraphic = buttonImage;
        returnButton.onClick.AddListener(ReturnToTitle);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.3f, 0.12f);
        buttonRect.anchorMax = new Vector2(0.7f, 0.3f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        GameObject buttonTextObject = new GameObject("Label");
        buttonTextObject.transform.SetParent(buttonObject.transform, false);
        TMP_Text buttonText = buttonTextObject.AddComponent<TextMeshProUGUI>();
        buttonText.font = fontAsset;
        buttonText.text = returnButtonLabel;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.fontSize = 28f;
        buttonText.color = buttonTextColor;

        RectTransform buttonTextRect = buttonText.rectTransform;
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;
    }

    private void ShowWinnerText(int winnerActorNumber, int defeatedActorNumber, string winnerName)
    {
        if (winnerText == null)
            return;

        string resolvedWinnerName = !string.IsNullOrWhiteSpace(winnerName)
            ? winnerName
            : ResolveWinnerName(winnerActorNumber);

        if (!string.IsNullOrWhiteSpace(resolvedWinnerName))
        {
            winnerText.text = resolvedWinnerName + winnerSuffix;
            return;
        }

        if (defeatedActorNumber > 0 && PhotonNetwork.CurrentRoom != null)
        {
            Player defeatedPlayer = PhotonNetwork.CurrentRoom.GetPlayer(defeatedActorNumber);
            if (defeatedPlayer != null && !string.IsNullOrWhiteSpace(defeatedPlayer.NickName))
            {
                winnerText.text = defeatedPlayer.NickName + " Defeated";
                return;
            }
        }

        winnerText.text = noWinnerMessage;
    }

    private void TrackExistingPlayerHealths()
    {
        PlayerHealth[] healths = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        for (int i = 0; i < healths.Length; i++)
        {
            RegisterHealth(healths[i]);
        }
    }

    private void RegisterHealth(PlayerHealth playerHealth)
    {
        if (playerHealth == null || !trackedHealths.Add(playerHealth))
            return;

        playerHealth.HealthChanged += HandleHealthChanged;
    }

    private void UnregisterHealth(PlayerHealth playerHealth)
    {
        if (playerHealth == null || !trackedHealths.Remove(playerHealth))
            return;

        playerHealth.HealthChanged -= HandleHealthChanged;
    }

    private void UntrackAllHealths()
    {
        foreach (PlayerHealth trackedHealth in trackedHealths)
        {
            if (trackedHealth != null)
                trackedHealth.HealthChanged -= HandleHealthChanged;
        }

        trackedHealths.Clear();
    }

    private void ResetLocalPauseState()
    {
        Time.timeScale = 1f;
    }

    private void SetCursorVisible(bool visible)
    {
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
    }
}
