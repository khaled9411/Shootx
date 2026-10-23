using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Editor utility to construct the MultiplayerMenu and MultiplayerLobby scene hierarchies.
/// Run from the menu: Tools → Multiplayer → Setup Scenes.
/// This creates the GameObjects, components, and wires the serialized references.
/// It does NOT modify Auth, Main, or MainMultiplayer scenes.
/// </summary>
public static class MultiplayerSceneSetupEditor
{
    private const string MenuPath = "Tools/Multiplayer/";

    [InitializeOnLoadMethod]
    private static void AutoSetupScenesIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            string menuPath = "Assets/_Multiplayer/Scenes/MultiplayerMenu.unity";
            string lobbyPath = "Assets/_Multiplayer/Scenes/MultiplayerLobby.unity";
            if (!System.IO.File.Exists(menuPath) || !System.IO.File.Exists(lobbyPath))
            {
                Debug.Log("[MPSetup] Multiplayer scenes missing. Automatically generating...");
                SetupAllNoDialog();
            }
        };
    }

    // ═══════════════════════════════════════════════════════════
    // Menu Items
    // ═══════════════════════════════════════════════════════════

    [MenuItem(MenuPath + "Setup All Multiplayer Scenes", false, 100)]
    public static void SetupAll()
    {
        if (!EditorUtility.DisplayDialog("Setup Multiplayer Scenes",
            "This will create/overwrite the MultiplayerMenu and MultiplayerLobby scene hierarchies.\n\n" +
            "Make sure you have saved any work first.\n\nProceed?",
            "Yes", "Cancel"))
            return;

        SetupAllNoDialog();

        EditorUtility.DisplayDialog("Complete",
            "Both scenes and the MultiplayerNetworkRoot prefab have been set up and added to Build Settings.\n\n" +
            "See MULTIPLAYER_SETUP_GUIDE_AR.md for full details.",
            "OK");
    }

    [MenuItem(MenuPath + "Build Scenes Automatically (No Dialog)", false, 105)]
    public static void SetupAllNoDialog()
    {
        EditorSceneManager.SaveOpenScenes();
        SetupMultiplayerMenuScene();
        SetupMultiplayerLobbyScene();
        SetupMainMultiplayerSpawner();
        SetupMatchManagementInMainMultiplayer();
        AddScenesToBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MPSetup] Multiplayer scenes and prefabs setup complete!");
    }

    [MenuItem(MenuPath + "Add Scenes to Build Settings", false, 106)]
    public static void AddScenesToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        string[] requiredScenes = new string[]
        {
            "Assets/Scenes/Auth.unity",
            "Assets/Scenes/Main.unity",
            "Assets/_Multiplayer/Scenes/MultiplayerMenu.unity",
            "Assets/_Multiplayer/Scenes/MultiplayerLobby.unity",
            "Assets/_Multiplayer/Scenes/MainMultiplayer.unity"
        };

        bool changed = false;
        foreach (var scenePath in requiredScenes)
        {
            if (!scenes.Exists(s => s.path == scenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                changed = true;
                Debug.Log($"[MPSetup] Added {scenePath} to Build Settings.");
            }
        }

        if (changed)
        {
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[MPSetup] Build Settings updated successfully!");
        }
    }

    [MenuItem(MenuPath + "Setup MultiplayerMenu Scene Only", false, 101)]
    public static void SetupMenuOnly()
    {
        SetupMultiplayerMenuScene();
        EditorUtility.DisplayDialog("Complete", "MultiplayerMenu scene has been set up.", "OK");
    }

    [MenuItem(MenuPath + "Setup MultiplayerLobby Scene Only", false, 102)]
    public static void SetupLobbyOnly()
    {
        SetupMultiplayerLobbyScene();
        EditorUtility.DisplayDialog("Complete", "MultiplayerLobby scene has been set up.", "OK");
    }

    // ═══════════════════════════════════════════════════════════
    // MultiplayerMenu Scene
    // ═══════════════════════════════════════════════════════════

    public static void SetupMultiplayerMenuScene()
    {
        string scenePath = "Assets/_Multiplayer/Scenes/MultiplayerMenu.unity";

        // Create new scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // ── Persistent roots ──
        var networkRoot = CreateGameObject("MultiplayerNetworkRoot");
        networkRoot.AddComponent<MultiplayerNetworkRoot>();

        var transport = networkRoot.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        var nm = networkRoot.AddComponent<Unity.Netcode.NetworkManager>();

        var nmSO = new SerializedObject(nm);
        var netConfigProp = nmSO.FindProperty("NetworkConfig");
        if (netConfigProp != null)
        {
            var transportProp = netConfigProp.FindPropertyRelative("NetworkTransport");
            if (transportProp != null) transportProp.objectReferenceValue = transport;

            var sceneMgmtProp = netConfigProp.FindPropertyRelative("EnableSceneManagement");
            if (sceneMgmtProp != null) sceneMgmtProp.boolValue = true;

            var tickRateProp = netConfigProp.FindPropertyRelative("TickRate");
            if (tickRateProp != null) tickRateProp.intValue = 30;

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Multiplayer/Players/Prefabs/Player.prefab");
            if (playerPrefab != null)
            {
                var playerPrefabProp = netConfigProp.FindPropertyRelative("PlayerPrefab");
                if (playerPrefabProp != null) playerPrefabProp.objectReferenceValue = playerPrefab;
            }

            var connApprovalProp = netConfigProp.FindPropertyRelative("ConnectionApproval");
            if (connApprovalProp != null) connApprovalProp.boolValue = true;

            var prefabsList = AssetDatabase.LoadAssetAtPath<Unity.Netcode.NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (prefabsList != null)
            {
                var prefabsListProp = netConfigProp.FindPropertyRelative("Prefabs.NetworkPrefabsLists");
                if (prefabsListProp != null)
                {
                    prefabsListProp.arraySize = 1;
                    prefabsListProp.GetArrayElementAtIndex(0).objectReferenceValue = prefabsList;
                }
            }
        }
        nmSO.ApplyModifiedPropertiesWithoutUndo();

        // Save as prefab in Assets/_Multiplayer/Prefabs
        string prefabFolder = "Assets/_Multiplayer/Prefabs";
        if (!AssetDatabase.IsValidFolder(prefabFolder))
        {
            System.IO.Directory.CreateDirectory(prefabFolder);
            AssetDatabase.Refresh();
        }
        string netRootPrefabPath = $"{prefabFolder}/MultiplayerNetworkRoot.prefab";
        PrefabUtility.SaveAsPrefabAsset(networkRoot, netRootPrefabPath);
        Debug.Log($"[MPSetup] MultiplayerNetworkRoot prefab saved: {netRootPrefabPath}");

        var sessionMgr = CreateGameObject("MultiplayerSessionManager");
        sessionMgr.AddComponent<MultiplayerSessionManager>();

        // ── EventSystem (if not present) ──
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var es = CreateGameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // ── Canvas ──
        var canvas = CreateCanvas("MenuCanvas");
        var canvasRT = canvas.GetComponent<RectTransform>();

        // ── Menu UI Controller ──
        var menuUI = canvas.AddComponent<MultiplayerMenuUI>();

        // ─── MAIN MENU PANEL ───
        var mainMenuPanel = CreatePanel(canvasRT, "MainMenuPanel");
        var mainMenuCG = mainMenuPanel.GetComponent<CanvasGroup>();
        var mainMenuRT = mainMenuPanel.GetComponent<RectTransform>();

        var playerNameLabel = CreateTMPText(mainMenuRT, "PlayerNameText", "Player Name", 24, TextAlignmentOptions.Center);
        SetAnchors(playerNameLabel, 0.2f, 0.88f, 0.8f, 0.95f);

        var quickMatchBtn = CreateButton(mainMenuRT, "QuickMatchButton", "Quick Match");
        SetAnchors(quickMatchBtn, 0.2f, 0.72f, 0.8f, 0.82f);

        var browseBtn = CreateButton(mainMenuRT, "BrowseLobbiesButton", "Browse Lobbies");
        SetAnchors(browseBtn, 0.2f, 0.58f, 0.8f, 0.68f);

        var createPublicBtn = CreateButton(mainMenuRT, "CreatePublicButton", "Create Public Lobby");
        SetAnchors(createPublicBtn, 0.2f, 0.44f, 0.8f, 0.54f);

        var createPrivateBtn = CreateButton(mainMenuRT, "CreatePrivateButton", "Create Private Lobby");
        SetAnchors(createPrivateBtn, 0.2f, 0.30f, 0.8f, 0.40f);

        var joinCodeBtn = CreateButton(mainMenuRT, "JoinByCodeButton", "Join by Code");
        SetAnchors(joinCodeBtn, 0.2f, 0.16f, 0.8f, 0.26f);

        var backBtn = CreateButton(mainMenuRT, "BackButton", "Back");
        SetAnchors(backBtn, 0.3f, 0.04f, 0.7f, 0.12f);

        // ─── BROWSER PANEL ───
        var browserPanel = CreatePanel(canvasRT, "BrowserPanel");
        var browserCG = browserPanel.GetComponent<CanvasGroup>();
        var browserRT = browserPanel.GetComponent<RectTransform>();
        browserCG.alpha = 0f;

        var browserTitle = CreateTMPText(browserRT, "BrowserTitle", "Public Lobbies", 28, TextAlignmentOptions.Center);
        SetAnchors(browserTitle, 0.1f, 0.88f, 0.9f, 0.95f);

        var refreshBtn = CreateButton(browserRT, "RefreshButton", "Refresh");
        SetAnchors(refreshBtn, 0.7f, 0.88f, 0.95f, 0.95f);

        var browserBackBtn = CreateButton(browserRT, "BrowserBackButton", "Back");
        SetAnchors(browserBackBtn, 0.3f, 0.03f, 0.7f, 0.10f);

        // Scroll view for lobby list
        var scrollArea = CreateGameObject("LobbyListScrollArea", browserRT);
        var scrollAreaRT = scrollArea.GetComponent<RectTransform>();
        SetAnchors(scrollAreaRT, 0.05f, 0.12f, 0.95f, 0.85f);
        scrollAreaRT.offsetMin = Vector2.zero;
        scrollAreaRT.offsetMax = Vector2.zero;

        var lobbyListContent = CreateGameObject("LobbyListContent", scrollAreaRT);
        var contentRT = lobbyListContent.GetComponent<RectTransform>();
        SetAnchors(contentRT, 0f, 0f, 1f, 1f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;
        var vlg = lobbyListContent.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        var csf = lobbyListContent.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var emptyListText = CreateTMPText(browserRT, "EmptyListText", "No lobbies available", 20, TextAlignmentOptions.Center);
        SetAnchors(emptyListText, 0.1f, 0.40f, 0.9f, 0.55f);
        emptyListText.gameObject.SetActive(false);

        var browserLoading = CreateGameObject("BrowserLoadingIndicator", browserRT);
        SetAnchors(browserLoading.GetComponent<RectTransform>(), 0.45f, 0.45f, 0.55f, 0.55f);
        browserLoading.SetActive(false);

        // ─── JOIN CODE PANEL ───
        var joinCodePanel = CreatePanel(canvasRT, "JoinCodePanel");
        var joinCodeCG = joinCodePanel.GetComponent<CanvasGroup>();
        var joinCodeRT = joinCodePanel.GetComponent<RectTransform>();
        joinCodeCG.alpha = 0f;

        var joinCodeTitle = CreateTMPText(joinCodeRT, "JoinCodeTitle", "Join by Code", 28, TextAlignmentOptions.Center);
        SetAnchors(joinCodeTitle, 0.1f, 0.70f, 0.9f, 0.80f);

        var codeInput = CreateInputField(joinCodeRT, "CodeInput", "Enter lobby code...");
        SetAnchors(codeInput.GetComponent<RectTransform>(), 0.15f, 0.50f, 0.85f, 0.62f);

        var joinConfirmBtn = CreateButton(joinCodeRT, "JoinCodeConfirmButton", "Join");
        SetAnchors(joinConfirmBtn, 0.25f, 0.35f, 0.75f, 0.45f);

        var joinCodeBackBtn = CreateButton(joinCodeRT, "JoinCodeBackButton", "Back");
        SetAnchors(joinCodeBackBtn, 0.3f, 0.20f, 0.7f, 0.28f);

        // ─── CREATE PANEL ───
        var createPanel = CreatePanel(canvasRT, "CreatePanel");
        var createCG = createPanel.GetComponent<CanvasGroup>();
        var createPanelRT = createPanel.GetComponent<RectTransform>();
        createCG.alpha = 0f;

        var createTitle = CreateTMPText(createPanelRT, "CreateTitleText", "Create Lobby", 28, TextAlignmentOptions.Center);
        SetAnchors(createTitle, 0.1f, 0.70f, 0.9f, 0.80f);

        var nameInput = CreateInputField(createPanelRT, "LobbyNameInput", "Lobby name (optional)...");
        SetAnchors(nameInput.GetComponent<RectTransform>(), 0.15f, 0.50f, 0.85f, 0.62f);

        var createConfirmBtn = CreateButton(createPanelRT, "CreateConfirmButton", "Create");
        SetAnchors(createConfirmBtn, 0.25f, 0.35f, 0.75f, 0.45f);

        var createBackBtn = CreateButton(createPanelRT, "CreateBackButton", "Back");
        SetAnchors(createBackBtn, 0.3f, 0.20f, 0.7f, 0.28f);

        // ─── LOADING PANEL ───
        var loadingPanel = CreatePanel(canvasRT, "LoadingPanel");
        var loadingCG = loadingPanel.GetComponent<CanvasGroup>();
        loadingCG.alpha = 0f;
        var loadingRT = loadingPanel.GetComponent<RectTransform>();

        var loadingText = CreateTMPText(loadingRT, "LoadingText", "Loading...", 22, TextAlignmentOptions.Center);
        SetAnchors(loadingText, 0.2f, 0.45f, 0.8f, 0.55f);

        var spinnerGO = CreateGameObject("LoadingSpinner", loadingRT);
        var spinnerRT = spinnerGO.GetComponent<RectTransform>();
        SetAnchors(spinnerRT, 0.45f, 0.55f, 0.55f, 0.65f);
        spinnerRT.offsetMin = Vector2.zero;
        spinnerRT.offsetMax = Vector2.zero;
        var spinnerImg = spinnerGO.AddComponent<Image>();
        spinnerImg.color = Color.white;

        // ─── NOTIFICATION PANEL ───
        var notifPanel = CreatePanel(canvasRT, "NotificationPanel");
        var notifCG = notifPanel.GetComponent<CanvasGroup>();
        var notifRT = notifPanel.GetComponent<RectTransform>();
        notifCG.alpha = 0f;
        SetAnchors(notifRT, 0.1f, 0.35f, 0.9f, 0.65f);

        var notifBG = notifPanel.GetComponent<Image>();
        if (notifBG) notifBG.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

        var notifText = CreateTMPText(notifRT, "NotificationText", "", 20, TextAlignmentOptions.Center);
        SetAnchors(notifText, 0.1f, 0.35f, 0.9f, 0.75f);

        var notifDismissBtn = CreateButton(notifRT, "NotificationDismissButton", "OK");
        SetAnchors(notifDismissBtn, 0.3f, 0.10f, 0.7f, 0.30f);

        // ─── WIRE SERIALIZED REFERENCES ───
        var so = new SerializedObject(menuUI);

        SetSerializedRef(so, "mainMenuPanel", mainMenuCG);
        SetSerializedRef(so, "mainMenuRect", mainMenuRT);
        SetSerializedRef(so, "quickMatchButton", quickMatchBtn.GetComponent<Button>());
        SetSerializedRef(so, "browseLobbiesButton", browseBtn.GetComponent<Button>());
        SetSerializedRef(so, "createPublicButton", createPublicBtn.GetComponent<Button>());
        SetSerializedRef(so, "createPrivateButton", createPrivateBtn.GetComponent<Button>());
        SetSerializedRef(so, "joinByCodeButton", joinCodeBtn.GetComponent<Button>());
        SetSerializedRef(so, "backButton", backBtn.GetComponent<Button>());
        SetSerializedRef(so, "playerNameText", playerNameLabel.GetComponent<TextMeshProUGUI>());

        SetSerializedRef(so, "browserPanel", browserCG);
        SetSerializedRef(so, "browserPanelRect", browserRT);
        SetSerializedRef(so, "refreshButton", refreshBtn.GetComponent<Button>());
        SetSerializedRef(so, "browserBackButton", browserBackBtn.GetComponent<Button>());
        SetSerializedRef(so, "lobbyListContent", contentRT.transform);
        SetSerializedRef(so, "emptyListText", emptyListText.GetComponent<TextMeshProUGUI>());
        SetSerializedRef(so, "browserLoadingIndicator", browserLoading);

        SetSerializedRef(so, "joinCodePanel", joinCodeCG);
        SetSerializedRef(so, "joinCodePanelRect", joinCodeRT);
        SetSerializedRef(so, "codeInput", codeInput);
        SetSerializedRef(so, "joinCodeConfirmButton", joinConfirmBtn.GetComponent<Button>());
        SetSerializedRef(so, "joinCodeBackButton", joinCodeBackBtn.GetComponent<Button>());

        SetSerializedRef(so, "createPanel", createCG);
        SetSerializedRef(so, "createPanelRect", createPanelRT);
        SetSerializedRef(so, "lobbyNameInput", nameInput);
        SetSerializedRef(so, "createConfirmButton", createConfirmBtn.GetComponent<Button>());
        SetSerializedRef(so, "createBackButton", createBackBtn.GetComponent<Button>());
        SetSerializedRef(so, "createTitleText", createTitle.GetComponent<TextMeshProUGUI>());

        SetSerializedRef(so, "loadingPanel", loadingCG);
        SetSerializedRef(so, "loadingText", loadingText.GetComponent<TextMeshProUGUI>());
        SetSerializedRef(so, "loadingSpinner", spinnerRT);

        SetSerializedRef(so, "notificationPanel", notifCG);
        SetSerializedRef(so, "notificationRect", notifRT);
        SetSerializedRef(so, "notificationText", notifText.GetComponent<TextMeshProUGUI>());
        SetSerializedRef(so, "notificationDismissButton", notifDismissBtn.GetComponent<Button>());

        so.ApplyModifiedPropertiesWithoutUndo();

        // ── Create lobby entry prefab placeholder ──
        CreateLobbyEntryPrefab();

        // Save scene
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[MPSetup] MultiplayerMenu scene saved: {scenePath}");
    }

    // ═══════════════════════════════════════════════════════════
    // MultiplayerLobby Scene
    // ═══════════════════════════════════════════════════════════

    public static void SetupMultiplayerLobbyScene()
    {
        string scenePath = "Assets/_Multiplayer/Scenes/MultiplayerLobby.unity";

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // EventSystem
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var es = CreateGameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // Canvas
        var canvas = CreateCanvas("LobbyCanvas");
        var canvasRT = canvas.GetComponent<RectTransform>();

        // Lobby UI Controller
        var lobbyUI = canvas.AddComponent<MultiplayerLobbyUI>();

        // ─── HEADER PANEL ───
        var headerPanel = CreatePanel(canvasRT, "HeaderPanel");
        var headerCG = headerPanel.GetComponent<CanvasGroup>();
        var headerRT = headerPanel.GetComponent<RectTransform>();
        SetAnchors(headerRT, 0.05f, 0.85f, 0.95f, 0.98f);
        var headerBG = headerPanel.GetComponent<Image>();
        if (headerBG) headerBG.color = new Color(0.12f, 0.14f, 0.18f, 0.9f);

        var lobbyNameText = CreateTMPText(headerRT, "LobbyNameText", "Lobby Name", 26, TextAlignmentOptions.Left);
        SetAnchors(lobbyNameText, 0.05f, 0.5f, 0.6f, 1f);

        var playerCountText = CreateTMPText(headerRT, "PlayerCountText", "0/4", 22, TextAlignmentOptions.Right);
        SetAnchors(playerCountText, 0.7f, 0.5f, 0.95f, 1f);

        var hostIndicatorText = CreateTMPText(headerRT, "HostIndicatorText", "Waiting Room", 18, TextAlignmentOptions.Left);
        SetAnchors(hostIndicatorText, 0.05f, 0f, 0.6f, 0.5f);

        // ─── CODE PANEL ───
        var codePanel = CreatePanel(canvasRT, "CodePanel");
        var codeCG = codePanel.GetComponent<CanvasGroup>();
        var codePanelRT = codePanel.GetComponent<RectTransform>();
        SetAnchors(codePanelRT, 0.05f, 0.76f, 0.95f, 0.84f);
        var codeBG = codePanel.GetComponent<Image>();
        if (codeBG) codeBG.color = new Color(0.15f, 0.17f, 0.22f, 0.85f);

        var codeLabel = CreateTMPText(codePanelRT, "CodeLabel", "Lobby Code:", 16, TextAlignmentOptions.Left);
        SetAnchors(codeLabel, 0.05f, 0.1f, 0.3f, 0.9f);

        var lobbyCodeText = CreateTMPText(codePanelRT, "LobbyCodeText", "------", 24, TextAlignmentOptions.Center);
        SetAnchors(lobbyCodeText, 0.3f, 0.1f, 0.7f, 0.9f);
        lobbyCodeText.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        var copyBtn = CreateButton(codePanelRT, "CopyCodeButton", "Copy");
        SetAnchors(copyBtn, 0.72f, 0.15f, 0.95f, 0.85f);
        var copyBtnText = copyBtn.GetComponentInChildren<TextMeshProUGUI>();

        // ─── PLAYER SLOTS ───
        var slotsContainer = CreateGameObject("PlayerSlotsContainer", canvasRT);
        var slotsRT = slotsContainer.GetComponent<RectTransform>();
        SetAnchors(slotsRT, 0.05f, 0.25f, 0.95f, 0.74f);
        slotsRT.offsetMin = Vector2.zero;
        slotsRT.offsetMax = Vector2.zero;
        var slotsVLG = slotsContainer.AddComponent<VerticalLayoutGroup>();
        slotsVLG.spacing = 8f;
        slotsVLG.padding = new RectOffset(0, 0, 5, 5);
        slotsVLG.childForceExpandWidth = true;
        slotsVLG.childForceExpandHeight = true;
        slotsVLG.childControlWidth = true;
        slotsVLG.childControlHeight = true;

        PlayerSlotUI[] slotComponents = new PlayerSlotUI[4];
        for (int i = 0; i < 4; i++)
        {
            slotComponents[i] = CreatePlayerSlot(slotsRT, i);
        }

        // ─── CONTROLS PANEL ───
        var controlsPanel = CreatePanel(canvasRT, "ControlsPanel");
        var controlsCG = controlsPanel.GetComponent<CanvasGroup>();
        var controlsRT2 = controlsPanel.GetComponent<RectTransform>();
        SetAnchors(controlsRT2, 0.05f, 0.03f, 0.95f, 0.22f);
        var controlsBG = controlsPanel.GetComponent<Image>();
        if (controlsBG) controlsBG.color = new Color(0.1f, 0.12f, 0.16f, 0.9f);

        var readyBtn = CreateButton(controlsRT2, "ReadyButton", "Ready");
        SetAnchors(readyBtn, 0.05f, 0.55f, 0.45f, 0.90f);
        var readyBtnComp = readyBtn.GetComponent<Button>();
        var readyBtnText = readyBtn.GetComponentInChildren<TextMeshProUGUI>();

        var startBtn = CreateButton(controlsRT2, "StartMatchButton", "Start Match");
        SetAnchors(startBtn, 0.05f, 0.55f, 0.45f, 0.90f);
        var startBtnComp = startBtn.GetComponent<Button>();
        var startBtnText = startBtn.GetComponentInChildren<TextMeshProUGUI>();
        startBtnComp.interactable = false;

        var leaveBtn = CreateButton(controlsRT2, "LeaveButton", "Leave Lobby");
        SetAnchors(leaveBtn, 0.55f, 0.55f, 0.95f, 0.90f);

        // Status text at bottom of controls
        var statusLabel = CreateTMPText(controlsRT2, "StatusLabel", "", 16, TextAlignmentOptions.Center);
        SetAnchors(statusLabel, 0.1f, 0.05f, 0.9f, 0.45f);

        // ─── STATUS PANEL (overlay) ───
        var statusPanel = CreatePanel(canvasRT, "StatusPanel");
        var statusCG = statusPanel.GetComponent<CanvasGroup>();
        statusCG.alpha = 0f;
        var statusPanelRT = statusPanel.GetComponent<RectTransform>();

        var statusBG = statusPanel.GetComponent<Image>();
        if (statusBG) statusBG.color = new Color(0f, 0f, 0f, 0.7f);

        var statusText = CreateTMPText(statusPanelRT, "StatusText", "Loading...", 22, TextAlignmentOptions.Center);
        SetAnchors(statusText, 0.2f, 0.45f, 0.8f, 0.55f);

        var spinnerGO = CreateGameObject("LoadingSpinner", statusPanelRT);
        var spinnerRT = spinnerGO.GetComponent<RectTransform>();
        SetAnchors(spinnerRT, 0.45f, 0.55f, 0.55f, 0.65f);
        spinnerRT.offsetMin = Vector2.zero;
        spinnerRT.offsetMax = Vector2.zero;
        var spinnerImg = spinnerGO.AddComponent<Image>();
        spinnerImg.color = Color.white;

        // ─── NOTIFICATION PANEL ───
        var notifPanel = CreatePanel(canvasRT, "NotificationPanel");
        var notifCG = notifPanel.GetComponent<CanvasGroup>();
        var notifRT = notifPanel.GetComponent<RectTransform>();
        notifCG.alpha = 0f;
        SetAnchors(notifRT, 0.1f, 0.35f, 0.9f, 0.65f);

        var notifBG = notifPanel.GetComponent<Image>();
        if (notifBG) notifBG.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

        var notifText = CreateTMPText(notifRT, "NotificationText", "", 20, TextAlignmentOptions.Center);
        SetAnchors(notifText, 0.1f, 0.35f, 0.9f, 0.75f);

        var notifDismissBtn = CreateButton(notifRT, "NotificationDismissButton", "OK");
        SetAnchors(notifDismissBtn, 0.3f, 0.10f, 0.7f, 0.30f);

        // ─── WIRE SERIALIZED REFERENCES ───
        var so = new SerializedObject(lobbyUI);

        SetSerializedRef(so, "headerPanel", headerCG);
        SetSerializedRef(so, "headerRect", headerRT);
        SetSerializedRef(so, "lobbyNameText", lobbyNameText.GetComponent<TextMeshProUGUI>());
        SetSerializedRef(so, "playerCountText", playerCountText.GetComponent<TextMeshProUGUI>());
        SetSerializedRef(so, "hostIndicatorText", hostIndicatorText.GetComponent<TextMeshProUGUI>());

        SetSerializedRef(so, "codePanel", codeCG);
        SetSerializedRef(so, "codePanelRect", codePanelRT);
        SetSerializedRef(so, "lobbyCodeText", lobbyCodeText.GetComponent<TextMeshProUGUI>());
        SetSerializedRef(so, "copyCodeButton", copyBtn.GetComponent<Button>());
        SetSerializedRef(so, "copyButtonText", copyBtnText);

        // Player slots array
        var slotsProp = so.FindProperty("playerSlots");
        slotsProp.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotComponents[i];
        }

        SetSerializedRef(so, "controlsPanel", controlsCG);
        SetSerializedRef(so, "controlsRect", controlsRT2);
        SetSerializedRef(so, "readyButton", readyBtnComp);
        SetSerializedRef(so, "readyButtonText", readyBtnText);
        SetSerializedRef(so, "startMatchButton", startBtnComp);
        SetSerializedRef(so, "startMatchButtonText", startBtnText);
        SetSerializedRef(so, "leaveButton", leaveBtn.GetComponent<Button>());

        SetSerializedRef(so, "statusPanel", statusCG);
        SetSerializedRef(so, "statusText", statusText.GetComponent<TextMeshProUGUI>());
        SetSerializedRef(so, "loadingSpinner", spinnerRT);

        SetSerializedRef(so, "notificationPanel", notifCG);
        SetSerializedRef(so, "notificationRect", notifRT);
        SetSerializedRef(so, "notificationText", notifText.GetComponent<TextMeshProUGUI>());
        SetSerializedRef(so, "notificationDismissButton", notifDismissBtn.GetComponent<Button>());

        so.ApplyModifiedPropertiesWithoutUndo();

        // Save
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[MPSetup] MultiplayerLobby scene saved: {scenePath}");
    }

    // ═══════════════════════════════════════════════════════════
    // Player Slot Creation
    // ═══════════════════════════════════════════════════════════

    private static PlayerSlotUI CreatePlayerSlot(RectTransform parent, int index)
    {
        var slotGO = new GameObject($"PlayerSlot_{index + 1}");
        slotGO.transform.SetParent(parent, false);

        var slotRT = slotGO.AddComponent<RectTransform>();
        var slotLE = slotGO.AddComponent<LayoutElement>();
        slotLE.preferredHeight = 80f;

        var slotBG = slotGO.AddComponent<Image>();
        slotBG.color = new Color(0.15f, 0.15f, 0.2f, 0.4f);

        var slotCG = slotGO.AddComponent<CanvasGroup>();
        var slotUI = slotGO.AddComponent<PlayerSlotUI>();

        // Occupied content container
        var occupied = new GameObject("OccupiedContent");
        occupied.transform.SetParent(slotRT, false);
        var occRT = occupied.AddComponent<RectTransform>();
        SetAnchors(occRT, 0f, 0f, 1f, 1f);
        occRT.offsetMin = Vector2.zero;
        occRT.offsetMax = Vector2.zero;

        var nameText = CreateTMPText(occRT, "PlayerNameText", $"Player {index + 1}", 20, TextAlignmentOptions.Left);
        SetAnchors(nameText, 0.05f, 0.2f, 0.5f, 0.8f);

        var hostBadge = new GameObject("HostBadge");
        hostBadge.transform.SetParent(occRT, false);
        var hostRT = hostBadge.AddComponent<RectTransform>();
        SetAnchors(hostRT, 0.5f, 0.25f, 0.65f, 0.75f);
        hostRT.offsetMin = Vector2.zero;
        hostRT.offsetMax = Vector2.zero;
        var hostBadgeTmp = hostBadge.AddComponent<TextMeshProUGUI>();
        hostBadgeTmp.text = "HOST";
        hostBadgeTmp.fontSize = 14;
        hostBadgeTmp.alignment = TextAlignmentOptions.Center;
        hostBadgeTmp.color = new Color(1f, 0.8f, 0.2f, 1f);
        hostBadge.SetActive(false);

        var readyIndicator = new GameObject("ReadyIndicator");
        readyIndicator.transform.SetParent(occRT, false);
        var readyRT = readyIndicator.AddComponent<RectTransform>();
        SetAnchors(readyRT, 0.75f, 0.25f, 0.95f, 0.75f);
        readyRT.offsetMin = Vector2.zero;
        readyRT.offsetMax = Vector2.zero;
        var readyTmp = readyIndicator.AddComponent<TextMeshProUGUI>();
        readyTmp.text = "READY";
        readyTmp.fontSize = 16;
        readyTmp.alignment = TextAlignmentOptions.Center;
        readyTmp.color = new Color(0.3f, 0.9f, 0.4f, 1f);
        readyIndicator.SetActive(false);

        var notReadyIndicator = new GameObject("NotReadyIndicator");
        notReadyIndicator.transform.SetParent(occRT, false);
        var notReadyRT = notReadyIndicator.AddComponent<RectTransform>();
        SetAnchors(notReadyRT, 0.75f, 0.25f, 0.95f, 0.75f);
        notReadyRT.offsetMin = Vector2.zero;
        notReadyRT.offsetMax = Vector2.zero;
        var notReadyTmp = notReadyIndicator.AddComponent<TextMeshProUGUI>();
        notReadyTmp.text = "NOT READY";
        notReadyTmp.fontSize = 14;
        notReadyTmp.alignment = TextAlignmentOptions.Center;
        notReadyTmp.color = new Color(0.8f, 0.3f, 0.3f, 1f);

        // Empty content container
        var empty = new GameObject("EmptyContent");
        empty.transform.SetParent(slotRT, false);
        var emptyRT = empty.AddComponent<RectTransform>();
        SetAnchors(emptyRT, 0f, 0f, 1f, 1f);
        emptyRT.offsetMin = Vector2.zero;
        emptyRT.offsetMax = Vector2.zero;
        empty.SetActive(false);

        var emptyText = CreateTMPText(emptyRT, "EmptyText", "Waiting for player...", 18, TextAlignmentOptions.Center);
        SetAnchors(emptyText, 0.1f, 0.1f, 0.9f, 0.9f);
        emptyText.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Italic;
        emptyText.GetComponent<TextMeshProUGUI>().color = new Color(1f, 1f, 1f, 0.3f);

        // Wire PlayerSlotUI serialized fields
        var slotSO = new SerializedObject(slotUI);
        SetSerializedRef(slotSO, "slotCanvasGroup", slotCG);
        SetSerializedRef(slotSO, "slotRect", slotRT);
        SetSerializedRef(slotSO, "slotBackground", slotBG);
        SetSerializedRef(slotSO, "occupiedContent", occupied);
        SetSerializedRef(slotSO, "playerNameText", nameText.GetComponent<TextMeshProUGUI>());
        SetSerializedRef(slotSO, "hostBadge", hostBadge);
        SetSerializedRef(slotSO, "readyIndicator", readyIndicator);
        SetSerializedRef(slotSO, "notReadyIndicator", notReadyIndicator);
        SetSerializedRef(slotSO, "emptyContent", empty);
        SetSerializedRef(slotSO, "emptyText", emptyText.GetComponent<TextMeshProUGUI>());
        slotSO.ApplyModifiedPropertiesWithoutUndo();

        return slotUI;
    }

    // ═══════════════════════════════════════════════════════════
    // Lobby Entry Prefab
    // ═══════════════════════════════════════════════════════════

    private static void CreateLobbyEntryPrefab()
    {
        string prefabPath = "Assets/_Multiplayer/Scripts/Lobby/UI/LobbyEntryPrefab.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            Debug.Log("[MPSetup] LobbyEntryPrefab already exists, skipping.");
            return;
        }

        var entryGO = new GameObject("LobbyEntry");
        var entryRT = entryGO.AddComponent<RectTransform>();
        entryRT.sizeDelta = new Vector2(0, 70);

        var entryLE = entryGO.AddComponent<LayoutElement>();
        entryLE.preferredHeight = 70f;

        var entryBG = entryGO.AddComponent<Image>();
        entryBG.color = new Color(0.18f, 0.2f, 0.25f, 0.8f);

        var entryUI = entryGO.AddComponent<LobbyEntryUI>();

        // Lobby name
        var nameGO = new GameObject("LobbyNameText");
        nameGO.transform.SetParent(entryRT, false);
        var nameRT = nameGO.AddComponent<RectTransform>();
        SetAnchors(nameRT, 0.05f, 0.5f, 0.5f, 0.95f);
        nameRT.offsetMin = Vector2.zero;
        nameRT.offsetMax = Vector2.zero;
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text = "Lobby Name";
        nameTMP.fontSize = 18;
        nameTMP.alignment = TextAlignmentOptions.Left;

        // Player count
        var countGO = new GameObject("PlayerCountText");
        countGO.transform.SetParent(entryRT, false);
        var countRT = countGO.AddComponent<RectTransform>();
        SetAnchors(countRT, 0.05f, 0.05f, 0.3f, 0.5f);
        countRT.offsetMin = Vector2.zero;
        countRT.offsetMax = Vector2.zero;
        var countTMP = countGO.AddComponent<TextMeshProUGUI>();
        countTMP.text = "0/4";
        countTMP.fontSize = 14;
        countTMP.alignment = TextAlignmentOptions.Left;
        countTMP.color = new Color(0.7f, 0.7f, 0.7f, 1f);

        // Status text
        var statusGO = new GameObject("StatusText");
        statusGO.transform.SetParent(entryRT, false);
        var statusRT = statusGO.AddComponent<RectTransform>();
        SetAnchors(statusRT, 0.5f, 0.05f, 0.7f, 0.5f);
        statusRT.offsetMin = Vector2.zero;
        statusRT.offsetMax = Vector2.zero;
        var statusTMP = statusGO.AddComponent<TextMeshProUGUI>();
        statusTMP.text = "Open";
        statusTMP.fontSize = 14;
        statusTMP.alignment = TextAlignmentOptions.Center;
        statusTMP.color = new Color(0.4f, 0.9f, 0.5f, 1f);

        // Join button
        var joinBtnGO = CreateButtonRaw(entryRT, "JoinButton", "Join");
        SetAnchors(joinBtnGO.GetComponent<RectTransform>(), 0.72f, 0.15f, 0.95f, 0.85f);
        var joinBtnText = joinBtnGO.GetComponentInChildren<TextMeshProUGUI>();

        // Wire entry UI
        var entrySO = new SerializedObject(entryUI);
        SetSerializedRef(entrySO, "lobbyNameText", nameTMP);
        SetSerializedRef(entrySO, "playerCountText", countTMP);
        SetSerializedRef(entrySO, "statusText", statusTMP);
        SetSerializedRef(entrySO, "joinButton", joinBtnGO.GetComponent<Button>());
        SetSerializedRef(entrySO, "joinButtonText", joinBtnText);
        entrySO.ApplyModifiedPropertiesWithoutUndo();

        // Save prefab
        var prefabDir = System.IO.Path.GetDirectoryName(prefabPath);
        if (!AssetDatabase.IsValidFolder(prefabDir))
        {
            // Ensure directory exists
            System.IO.Directory.CreateDirectory(prefabDir);
            AssetDatabase.Refresh();
        }

        PrefabUtility.SaveAsPrefabAsset(entryGO, prefabPath);
        Object.DestroyImmediate(entryGO);
        Debug.Log($"[MPSetup] LobbyEntryPrefab saved: {prefabPath}");

        // Assign to menu UI if present
        var menuUI = Object.FindAnyObjectByType<MultiplayerMenuUI>();
        if (menuUI != null)
        {
            var menuSO = new SerializedObject(menuUI);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            SetSerializedRef(menuSO, "lobbyEntryPrefab", prefab);
            menuSO.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Helper Methods
    // ═══════════════════════════════════════════════════════════

    private static GameObject CreateGameObject(string name, Transform parent = null)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static GameObject CreateCanvas(string name)
    {
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();

        return go;
    }

    private static GameObject CreatePanel(RectTransform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.12f, 0.16f, 0.95f);

        var cg = go.AddComponent<CanvasGroup>();

        return go;
    }

    private static RectTransform CreateButton(RectTransform parent, string name, string text)
    {
        var go = CreateButtonRaw(parent, name, text);
        return go.GetComponent<RectTransform>();
    }

    private static GameObject CreateButtonRaw(RectTransform parent, string name, string text)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.2f, 0.4f);
        rt.anchorMax = new Vector2(0.8f, 0.6f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.22f, 0.28f, 0.36f, 1f);

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(0.22f, 0.28f, 0.36f, 1f);
        colors.highlightedColor = new Color(0.28f, 0.35f, 0.45f, 1f);
        colors.pressedColor = new Color(0.18f, 0.22f, 0.28f, 1f);
        colors.disabledColor = new Color(0.15f, 0.17f, 0.2f, 0.5f);
        btn.colors = colors;

        // Button text
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(rt, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(8, 4);
        textRT.offsetMax = new Vector2(-8, -4);

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 20;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return go;
    }

    private static RectTransform CreateTMPText(RectTransform parent, string name, string text,
        float fontSize, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;

        return rt;
    }

    private static TMP_InputField CreateInputField(RectTransform parent, string name, string placeholder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0.3f);
        rt.anchorMax = new Vector2(0.9f, 0.7f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.09f, 0.12f, 1f);

        // Text area
        var textArea = new GameObject("Text Area");
        textArea.transform.SetParent(rt, false);
        var areaRT = textArea.AddComponent<RectTransform>();
        areaRT.anchorMin = Vector2.zero;
        areaRT.anchorMax = Vector2.one;
        areaRT.offsetMin = new Vector2(10, 5);
        areaRT.offsetMax = new Vector2(-10, -5);
        textArea.AddComponent<RectMask2D>();

        // Input text
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(areaRT, false);
        var textRT2 = textGO.AddComponent<RectTransform>();
        textRT2.anchorMin = Vector2.zero;
        textRT2.anchorMax = Vector2.one;
        textRT2.offsetMin = Vector2.zero;
        textRT2.offsetMax = Vector2.zero;
        var inputTMP = textGO.AddComponent<TextMeshProUGUI>();
        inputTMP.fontSize = 20;
        inputTMP.color = Color.white;

        // Placeholder
        var phGO = new GameObject("Placeholder");
        phGO.transform.SetParent(areaRT, false);
        var phRT = phGO.AddComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero;
        phRT.anchorMax = Vector2.one;
        phRT.offsetMin = Vector2.zero;
        phRT.offsetMax = Vector2.zero;
        var phTMP = phGO.AddComponent<TextMeshProUGUI>();
        phTMP.text = placeholder;
        phTMP.fontSize = 20;
        phTMP.fontStyle = FontStyles.Italic;
        phTMP.color = new Color(1f, 1f, 1f, 0.3f);

        // Configure TMP_InputField
        var inputField = go.AddComponent<TMP_InputField>();
        inputField.textViewport = areaRT;
        inputField.textComponent = inputTMP;
        inputField.placeholder = phTMP;
        inputField.characterLimit = 30;

        return inputField;
    }

    private static void SetAnchors(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetAnchors(Component comp, float xMin, float yMin, float xMax, float yMax)
    {
        var rt = comp as RectTransform;
        if (rt == null) rt = comp.GetComponent<RectTransform>();
        SetAnchors(rt, xMin, yMin, xMax, yMax);
    }

    private static void SetSerializedRef(SerializedObject so, string fieldName, Object value)
    {
        var prop = so.FindProperty(fieldName);
        if (prop != null)
        {
            prop.objectReferenceValue = value;
        }
        else
        {
            Debug.LogWarning($"[MPSetup] Could not find serialized field: {fieldName}");
        }
    }

    [MenuItem(MenuPath + "Setup MainMultiplayer Spawner", false, 110)]
    public static void SetupMainMultiplayerSpawner()
    {
        string scenePath = "Assets/_Multiplayer/Scenes/MainMultiplayer.unity";
        if (!System.IO.File.Exists(scenePath))
        {
            Debug.LogWarning($"[MPSetup] Scene not found: {scenePath}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var existing = Object.FindAnyObjectByType<MultiplayerSpawnManager>();
        if (existing == null)
        {
            var spawnerGO = CreateGameObject("MultiplayerSpawnManager");
            var spawner = spawnerGO.AddComponent<MultiplayerSpawnManager>();

            var spawnPoints = new Transform[4];
            var p1 = CreateGameObject("SpawnPoint_1", spawnerGO.transform);
            p1.transform.position = new Vector3(-10f, 0.5f, -5f);
            p1.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            spawnPoints[0] = p1.transform;

            var p2 = CreateGameObject("SpawnPoint_2", spawnerGO.transform);
            p2.transform.position = new Vector3(0f, 0.5f, 4f);
            p2.transform.rotation = Quaternion.Euler(0f, 225f, 0f);
            spawnPoints[1] = p2.transform;

            var p3 = CreateGameObject("SpawnPoint_3", spawnerGO.transform);
            p3.transform.position = new Vector3(-10f, 0.5f, 4f);
            p3.transform.rotation = Quaternion.Euler(0f, 135f, 0f);
            spawnPoints[2] = p3.transform;

            var p4 = CreateGameObject("SpawnPoint_4", spawnerGO.transform);
            p4.transform.position = new Vector3(0f, 0.5f, -5f);
            p4.transform.rotation = Quaternion.Euler(0f, 315f, 0f);
            spawnPoints[3] = p4.transform;

            var spawnerSO = new SerializedObject(spawner);
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Multiplayer/Players/Prefabs/Player.prefab");
            var playerPrefabProp = spawnerSO.FindProperty("playerPrefab");
            if (playerPrefabProp != null && playerPrefab != null)
                playerPrefabProp.objectReferenceValue = playerPrefab;

            var spawnPointsProp = spawnerSO.FindProperty("spawnPoints");
            if (spawnPointsProp != null)
            {
                spawnPointsProp.arraySize = 4;
                for (int i = 0; i < 4; i++)
                    spawnPointsProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
            }
            spawnerSO.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MPSetup] MultiplayerSpawnManager setup complete in MainMultiplayer!");
        }
        else
        {
            Debug.Log("[MPSetup] MultiplayerSpawnManager already exists in MainMultiplayer.");
        }
    }

    [MenuItem(MenuPath + "Setup Match Management in MainMultiplayer", false, 111)]
    public static void SetupMatchManagementInMainMultiplayer()
    {
        string scenePath = "Assets/_Multiplayer/Scenes/MainMultiplayer.unity";
        if (!System.IO.File.Exists(scenePath))
        {
            Debug.LogWarning($"[MPSetup] Scene not found: {scenePath}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // 1. Ensure MatchUI instance exists
        var existingHUD = Object.FindAnyObjectByType<MatchHUD>();
        if (existingHUD == null)
        {
            var matchUIPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Multiplayer/Prefabs/MatchUI.prefab");
            if (matchUIPrefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(matchUIPrefab);
                instance.name = "MatchUI";
                Debug.Log("[MPSetup] Instantiated MatchUI prefab in MainMultiplayer.");
            }
        }

        // 2. Ensure MatchManagerBootstrapper exists
        var existingBootstrapper = Object.FindAnyObjectByType<MatchManagerBootstrapper>();
        if (existingBootstrapper == null)
        {
            var bootGO = CreateGameObject("MatchBootstrapper");
            var boot = bootGO.AddComponent<MatchManagerBootstrapper>();

            var bootSO = new SerializedObject(boot);

            var matchUIPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Multiplayer/Prefabs/MatchUI.prefab");
            var matchManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Multiplayer/Prefabs/MatchManager.prefab");
            var matchConfig = AssetDatabase.LoadAssetAtPath<MatchConfig>("Assets/_Multiplayer/Scripts/Match/DefaultMatchConfig.asset");

            SetSerializedRef(bootSO, "matchUIPrefab", matchUIPrefab);
            SetSerializedRef(bootSO, "matchManagerPrefab", matchManagerPrefab);
            SetSerializedRef(bootSO, "matchConfig", matchConfig);

            var mapPrefabsProp = bootSO.FindProperty("mapPrefabs");
            if (mapPrefabsProp != null)
            {
                mapPrefabsProp.arraySize = 4;
                string[] mapPaths = new string[]
                {
                    "Assets/_Multiplayer/Levels/PVP Levels/PVP Map 1.prefab",
                    "Assets/_Multiplayer/Levels/PVP Levels/PVP Map 2.prefab",
                    "Assets/_Multiplayer/Levels/PVP Levels/PVP Map 3.prefab",
                    "Assets/_Multiplayer/Levels/PVP Levels/PVP Map 4.prefab"
                };

                for (int i = 0; i < 4; i++)
                {
                    var mapAsset = AssetDatabase.LoadAssetAtPath<GameObject>(mapPaths[i]);
                    mapPrefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = mapAsset;
                }
            }

            var maskProp = bootSO.FindProperty("spawnObstacleMask");
            if (maskProp != null)
            {
                maskProp.intValue = 1; // Default layer
            }

            bootSO.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[MPSetup] Created MatchBootstrapper in MainMultiplayer.");
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[MPSetup] Match management setup complete in MainMultiplayer!");
    }
}
#endif
