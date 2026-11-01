using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tools → Sky Bot → Rebuild Mobile UI
// Makes the Main Menu, HUD, Pause and Win screens fit every phone:
//   * Canvas Scaler: Scale With Screen Size, 1920 x 1080, Match = 1 (landscape game)
//   * every element anchored to its corner / edge / center, panels stretched
//   * buttons and text inside a SafeArea panel, background art full screen
//   * touch targets >= 120 x 120 px, text >= 36 px on a dark plate
// The tool is safe to run again: it only moves and resizes the existing objects
// and creates the SafeArea / Pause objects when they are missing.
public static class MobileUIBuilder
{
    // ---------- style: one look for the whole game ----------
    static readonly Color Navy      = new Color(0.07f, 0.10f, 0.20f, 0.82f);   // plates
    static readonly Color NavySolid = new Color(0.06f, 0.09f, 0.18f, 0.96f);   // settings / help
    static readonly Color Amber     = new Color(1.00f, 0.69f, 0.13f, 1f);      // main action
    static readonly Color Coral     = new Color(0.95f, 0.36f, 0.27f, 1f);      // fire / quit
    static readonly Color Steel     = new Color(0.20f, 0.29f, 0.47f, 1f);      // secondary
    static readonly Color Ink       = new Color(0.08f, 0.09f, 0.14f, 1f);      // text on amber
    static readonly Color Dim       = new Color(0f, 0f, 0f, 0.72f);            // pause overlay

    static Sprite Rounded => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    static Sprite Circle  => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

    const string MenuScene = "Assets/Scenes/Main menu.unity";
    const string WinScene  = "Assets/Scenes/Win.unity";
    static readonly string[] LevelScenes =
    {
        "Assets/Scenes/Level1.unity",
        "Assets/Scenes/Level 2.unity",
        "Assets/Scenes/Level3_Boss.unity",
    };

    [MenuItem("Tools/Sky Bot/Rebuild Mobile UI")]
    public static void RebuildAll()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        BuildMenu(EditorSceneManager.OpenScene(MenuScene));
        foreach (string path in LevelScenes)
            BuildLevel(EditorSceneManager.OpenScene(path));
        BuildWin(EditorSceneManager.OpenScene(WinScene));

        EditorSceneManager.OpenScene(MenuScene);
        Debug.Log("[Sky Bot] Mobile UI rebuilt: Main menu, 3 levels (HUD + Pause), Win.");
    }

    // =====================================================================
    // MAIN MENU: title, Play, Settings, How to play, Quit
    // =====================================================================
    static void BuildMenu(Scene scene)
    {
        Canvas canvas = FindCanvas(scene);
        SetupScaler(canvas);
        RectTransform root = canvas.transform as RectTransform;

        // background art stays full screen, behind everything
        RectTransform menuPanel = Child(root, "MainMenuPanel");
        RectTransform bg = MakeBackground(root, menuPanel.GetComponent<Image>());

        // MainMenuPanel is switched on/off by MenuButtons, so it stays a full-screen
        // (now transparent) panel; its buttons go into its own SafeArea
        Stretch(menuPanel);
        Image mp = menuPanel.GetComponent<Image>();
        mp.color = new Color(0, 0, 0, 0.25f);
        mp.sprite = null;
        RectTransform safe = SafeAreaIn(menuPanel);

        TextMeshProUGUI title = MakeText(safe, "Title", "SKY BOT", 120, FontStyles.Bold, Color.white);
        Place(title.rectTransform, Anchor.TopCenter, new Vector2(0, -70), new Vector2(1200, 150));
        TextMeshProUGUI tag = MakeText(safe, "Tagline", "Run. Jump. Shoot. Beat the boss.", 40, FontStyles.Italic, Amber);
        Place(tag.rectTransform, Anchor.TopCenter, new Vector2(0, -215), new Vector2(1200, 60));

        // name field above Play
        RectTransform field = Grab(menuPanel, safe, "Field_Name");
        Place(field, Anchor.Center, new Vector2(0, 150), new Vector2(640, 110));
        StyleInputField(field.GetComponent<TMP_InputField>());

        Button play = Grab(menuPanel, safe, "Btn_Start").GetComponent<Button>();
        Place(play.transform as RectTransform, Anchor.Center, new Vector2(0, 0), new Vector2(640, 150));
        StyleButton(play, "PLAY", Amber, Ink, 64);

        // secondary actions in one row under Play
        Button settings = Grab(menuPanel, safe, "Btn_Settings").GetComponent<Button>();
        Place(settings.transform as RectTransform, Anchor.Center, new Vector2(-380, -175), new Vector2(350, 130));
        StyleButton(settings, "SETTINGS", Steel, Color.white, 40);

        Button help = Grab(menuPanel, safe, "Btn_Help").GetComponent<Button>();
        Place(help.transform as RectTransform, Anchor.Center, new Vector2(0, -175), new Vector2(350, 130));
        StyleButton(help, "HOW TO PLAY", Steel, Color.white, 38);

        Button quit = Grab(menuPanel, safe, "Btn_Quit").GetComponent<Button>();
        Place(quit.transform as RectTransform, Anchor.Center, new Vector2(380, -175), new Vector2(350, 130));
        StyleButton(quit, "QUIT", Coral, Color.white, 44);

        BuildSettingsPanel(root);
        BuildHelpPanel(root);

        bg.SetAsFirstSibling();
        Save(scene);
    }

    static void BuildSettingsPanel(RectTransform root)
    {
        RectTransform panel = Child(root, "SettingsPanel");
        Stretch(panel);
        Image img = panel.GetComponent<Image>();
        img.sprite = null; img.color = NavySolid;
        RectTransform safe = SafeAreaIn(panel);

        TextMeshProUGUI title = MakeText(safe, "Title", "SETTINGS", 80, FontStyles.Bold, Color.white);
        Place(title.rectTransform, Anchor.TopCenter, new Vector2(0, -50), new Vector2(900, 110));

        // left column: labels, right column: controls
        StyleToggle(Grab(panel, safe, "Toggle_Fullscreen"), "Fullscreen", new Vector2(0, 230));
        StyleToggle(Grab(panel, safe, "Toggle_Music"), "Music", new Vector2(0, 100));

        RectTransform colorLabel = Grab(panel, safe, "Ball color");
        Label(colorLabel, "Hero color", new Vector2(-230, -30));
        RectTransform dropdown = Grab(panel, safe, "DropdownColor");
        Place(dropdown, Anchor.Center, new Vector2(230, -30), new Vector2(420, 110));
        StyleDropdown(dropdown.GetComponent<TMP_Dropdown>());

        RectTransform volLabel = Grab(panel, safe, "MusicVolume");
        Label(volLabel, "Music volume", new Vector2(-230, -160));
        RectTransform slider = Grab(panel, safe, "Slider");
        Place(slider, Anchor.Center, new Vector2(230, -160), new Vector2(420, 120));
        StyleSlider(slider.GetComponent<Slider>());

        Button back = Grab(panel, safe, "Btn_Back").GetComponent<Button>();
        Place(back.transform as RectTransform, Anchor.BottomCenter, new Vector2(0, 50), new Vector2(420, 130));
        StyleButton(back, "BACK", Amber, Ink, 48);
    }

    static void BuildHelpPanel(RectTransform root)
    {
        RectTransform panel = Child(root, "HelpPanel");
        Stretch(panel);
        Image img = panel.GetComponent<Image>();
        img.sprite = null; img.color = NavySolid;
        RectTransform safe = SafeAreaIn(panel);

        TextMeshProUGUI title = MakeText(safe, "Title", "HOW TO PLAY", 80, FontStyles.Bold, Color.white);
        Place(title.rectTransform, Anchor.TopCenter, new Vector2(0, -50), new Vector2(900, 110));

        RectTransform scroll = Grab(panel, safe, "Scroll View");
        Place(scroll, Anchor.Center, new Vector2(0, 10), new Vector2(1300, 560));
        scroll.GetComponent<Image>().color = Navy;
        ScrollRect sr = scroll.GetComponent<ScrollRect>();
        sr.horizontal = false;
        if (sr.horizontalScrollbar != null) sr.horizontalScrollbar.gameObject.SetActive(false);
        sr.horizontalScrollbar = null;
        if (sr.viewport != null) { sr.viewport.offsetMin = new Vector2(30, 20); sr.viewport.offsetMax = new Vector2(-30, -20); }

        TextMeshProUGUI body = sr.content.GetComponentInChildren<TextMeshProUGUI>(true);
        VerticalLayoutGroup vl = sr.content.GetComponent<VerticalLayoutGroup>();
        if (vl != null) { vl.childControlWidth = true; vl.childForceExpandWidth = true; vl.childControlHeight = true; }
        body.text =
            "<b>Goal</b>: collect every coin, then reach the gate. Beat the boss in level 3.\n\n" +
            "<b>Joystick</b> (bottom-left): run.\n" +
            "<b>JUMP</b> (bottom-right): jump over pits and rocks.\n" +
            "<b>FIRE</b>: shoot where the hero is looking.\n" +
            "<b>II</b> (top-right): pause the game.\n\n" +
            "You have 3 hearts. Falling rocks, spikes and enemies take one. " +
            "Pick up a heart to heal. Watch the timer!";
        body.fontSize = 38;
        // the text is short, so it simply fills the window (no scrolling needed)
        sr.vertical = false;
        if (sr.verticalScrollbar != null) sr.verticalScrollbar.gameObject.SetActive(false);
        sr.verticalScrollbar = null;
        if (vl != null) vl.enabled = false;
        ContentSizeFitter fit = sr.content.GetComponent<ContentSizeFitter>();
        if (fit != null) fit.enabled = false;
        Stretch(sr.content);
        Stretch(body.rectTransform);
        body.color = Color.white;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.textWrappingMode = TextWrappingModes.Normal;

        Button close = Grab(panel, safe, "Btn_CloseHelp").GetComponent<Button>();
        Place(close.transform as RectTransform, Anchor.BottomCenter, new Vector2(0, 50), new Vector2(420, 130));
        StyleButton(close, "BACK", Amber, Ink, 48);
    }

    // =====================================================================
    // LEVEL: HUD (hearts, score, timer, pause, joystick, JUMP, FIRE) + Pause
    // =====================================================================
    static void BuildLevel(Scene scene)
    {
        Canvas canvas = FindCanvas(scene);
        SetupScaler(canvas);
        RectTransform root = canvas.transform as RectTransform;
        RectTransform safe = SafeAreaIn(root);
        safe.name = "SafeArea";

        // --- top-left: hearts, score, player name ---
        for (int i = 0; i < 3; i++)
        {
            string n = i == 0 ? "hp" : "hp (" + i + ")";
            RectTransform heart = Grab(root, safe, n, n);
            Place(heart, Anchor.TopLeft, new Vector2(36 + i * 112, -30), new Vector2(100, 100));
        }

        RectTransform oldScorePlate = Child(safe, "ScorePlate");
        RectTransform coins = Grab(root, safe, "Text (TMP)", "ScoreText");
        RectTransform coinPlate = Plate(safe, "ScorePlate", Anchor.TopLeft, new Vector2(36, -142), new Vector2(430, 76));
        FillParent(Move(coins, coinPlate), 24);
        StyleHudText(coins.GetComponent<TextMeshProUGUI>(), "Coins: 0 / 0", 44, TextAlignmentOptions.MidlineLeft);
        coinPlate.gameObject.SetActive(oldScorePlate != null ? oldScorePlate.gameObject.activeSelf : coins.gameObject.activeSelf);
        coins.gameObject.SetActive(true);

        RectTransform playerName = Grab(root, safe, "Text_PlayerName", "Text_PlayerName");
        bool hasScore = coinPlate.gameObject.activeSelf;
        RectTransform namePlate = Plate(safe, "NamePlate", Anchor.TopLeft,
                                        new Vector2(36, hasScore ? -230 : -142), new Vector2(430, 64));
        FillParent(Move(playerName, namePlate), 24);
        StyleHudText(playerName.GetComponent<TextMeshProUGUI>(), "Player", 36, TextAlignmentOptions.MidlineLeft);

        // --- top-center: timer (levels 1-2) or boss bar (level 3) ---
        RectTransform oldTimerPlate = Child(safe, "TimerPlate");
        RectTransform timer = Grab(root, safe, "TimerText ", "TimerText ");
        RectTransform timerPlate = Plate(safe, "TimerPlate", Anchor.TopCenter, new Vector2(0, -30), new Vector2(320, 86));
        FillParent(Move(timer, timerPlate), 10);
        StyleHudText(timer.GetComponent<TextMeshProUGUI>(), "Time: 60", 48, TextAlignmentOptions.Center);
        timerPlate.gameObject.SetActive(oldTimerPlate != null ? oldTimerPlate.gameObject.activeSelf : timer.gameObject.activeSelf);
        timer.gameObject.SetActive(true);

        RectTransform bossLabel = Child(root, "BossLabel", true) ?? Child(safe, "BossLabel");
        if (bossLabel != null)
        {
            RectTransform bossPlate = Plate(safe, "BossPlate", Anchor.TopCenter, new Vector2(0, -16), new Vector2(820, 136));
            bossPlate.SetSiblingIndex(0);
            Move(bossLabel, safe);
            Place(bossLabel, Anchor.TopCenter, new Vector2(0, -26), new Vector2(700, 56));
            StyleHudText(bossLabel.GetComponent<TextMeshProUGUI>(), "BOSS", 44, TextAlignmentOptions.Center);
            RectTransform bossHp = Grab(root, safe, "BossHP", "BossHP");
            Place(bossHp, Anchor.TopCenter, new Vector2(0, -92), new Vector2(760, 40));
        }

        // --- center: win / message text ---
        RectTransform win = Grab(root, safe, "WinText ", "WinText ");
        Place(win, Anchor.Center, new Vector2(0, 120), new Vector2(1200, 200));
        TextMeshProUGUI winTmp = win.GetComponent<TextMeshProUGUI>();
        StyleHudText(winTmp, "", 72, TextAlignmentOptions.Center);
        winTmp.fontStyle = FontStyles.Bold;
        winTmp.color = Amber;

        // --- top-right: pause button (was the old "Menu" button) ---
        RectTransform pauseRt = Child(root, "Btn_Menu", true) ?? Child(safe, "Btn_Pause");
        pauseRt.name = "Btn_Pause";
        Move(pauseRt, safe);
        Place(pauseRt, Anchor.TopRight, new Vector2(-36, -30), new Vector2(130, 130));
        Button pauseBtn = pauseRt.GetComponent<Button>();
        StyleButton(pauseBtn, "II", Navy, Color.white, 64);

        // --- bottom-left: joystick ---
        RectTransform stick = Grab(root, safe, "StickBG", "StickBG");
        Place(stick, Anchor.BottomLeft, new Vector2(70, 60), new Vector2(320, 320));
        Image stickImg = stick.GetComponent<Image>();
        stickImg.sprite = Circle; stickImg.color = new Color(1, 1, 1, 0.22f);
        RectTransform knob = Child(stick, "Knob");
        knob.anchorMin = knob.anchorMax = knob.pivot = new Vector2(0.5f, 0.5f);
        knob.anchoredPosition = Vector2.zero;
        knob.sizeDelta = new Vector2(150, 150);
        Image knobImg = knob.GetComponent<Image>();
        knobImg.sprite = Circle; knobImg.color = new Color(1, 1, 1, 0.85f);
        OnScreenStick os = knob.GetComponent<OnScreenStick>();
        if (os != null) { os.movementRange = 110; EditorUtility.SetDirty(os); }

        // --- bottom-right: JUMP (big, under the thumb) and FIRE ---
        RoundControl(Grab(root, safe, "Btn_Jump", "Btn_Jump"),
                     new Vector2(-60, 60), 230, Amber, "JUMP", Ink, 48);
        RoundControl(Grab(root, safe, "Btn_Fire", "Btn_Fire"),
                     new Vector2(-320, 90), 190, Coral, "FIRE", Color.white, 44);

        // --- Pause panel (full screen, on top) ---
        PauseMenu pm = Ensure<PauseMenu>(canvas);
        RectTransform pausePanel = Child(root, "PausePanel") ?? NewRect("PausePanel", root);
        Stretch(pausePanel);
        Image dim = Ensure<Image>(pausePanel);
        dim.sprite = null; dim.color = Dim; dim.raycastTarget = true;   // blocks taps on the HUD
        RectTransform pSafe = SafeAreaIn(pausePanel);

        RectTransform card = Child(pSafe, "Card") ?? NewRect("Card", pSafe);
        Place(card, Anchor.Center, Vector2.zero, new Vector2(760, 760));
        Image cardImg = Ensure<Image>(card);
        cardImg.sprite = Rounded; cardImg.type = Image.Type.Sliced; cardImg.color = NavySolid;

        TextMeshProUGUI pTitle = MakeText(card, "Title", "PAUSED", 96, FontStyles.Bold, Color.white);
        Place(pTitle.rectTransform, Anchor.TopCenter, new Vector2(0, -50), new Vector2(700, 130));

        Button resume = MakeButton(card, "Btn_Resume", "RESUME", Amber, Ink, 56, new Vector2(0, 40), new Vector2(560, 140));
        Button restart = MakeButton(card, "Btn_Restart", "RESTART", Steel, Color.white, 52, new Vector2(0, -130), new Vector2(560, 130));
        Button menu = MakeButton(card, "Btn_ToMenu", "MENU", Steel, Color.white, 52, new Vector2(0, -280), new Vector2(560, 130));

        pm.pausePanel = pausePanel.gameObject;
        pm.pauseButton = pauseRt.gameObject;
        Wire(pauseBtn, pm.Pause);
        Wire(resume, pm.Resume);
        Wire(restart, pm.Restart);
        Wire(menu, pm.ToMenu);
        EditorUtility.SetDirty(pm);

        pausePanel.SetAsLastSibling();
        pausePanel.gameObject.SetActive(false);
        Save(scene);
    }

    // =====================================================================
    // WIN: title, Play again, Menu
    // =====================================================================
    static void BuildWin(Scene scene)
    {
        Canvas canvas = FindCanvas(scene);
        SetupScaler(canvas);
        RectTransform root = canvas.transform as RectTransform;
        RectTransform panel = Child(root, "MainMenuPanel");
        RectTransform bg = MakeBackground(root, panel.GetComponent<Image>());
        Stretch(panel);
        Image pi = panel.GetComponent<Image>();
        pi.sprite = null; pi.color = new Color(0, 0, 0, 0.25f);
        RectTransform safe = SafeAreaIn(panel);

        RectTransform title = Grab(panel, safe, "WinTitle");
        Place(title, Anchor.TopCenter, new Vector2(0, -110), new Vector2(1200, 180));
        TextMeshProUGUI t = title.GetComponent<TextMeshProUGUI>();
        t.text = "YOU WIN!"; t.fontSize = 130; t.fontStyle = FontStyles.Bold; t.color = Amber;
        t.alignment = TextAlignmentOptions.Center;

        Button again = Grab(panel, safe, "Btn_Start").GetComponent<Button>();
        Place(again.transform as RectTransform, Anchor.Center, new Vector2(0, -20), new Vector2(640, 150));
        StyleButton(again, "PLAY AGAIN", Amber, Ink, 60);

        Button menu = Grab(panel, safe, "Btn_Quit").GetComponent<Button>();
        Place(menu.transform as RectTransform, Anchor.Center, new Vector2(0, -200), new Vector2(640, 140));
        StyleButton(menu, "MENU", Steel, Color.white, 52);

        bg.SetAsFirstSibling();
        Save(scene);
    }

    // =====================================================================
    // helpers
    // =====================================================================
    enum Anchor { TopLeft, TopCenter, TopRight, Center, BottomLeft, BottomCenter, BottomRight }

    static Vector2 AnchorPoint(Anchor a)
    {
        switch (a)
        {
            case Anchor.TopLeft:      return new Vector2(0, 1);
            case Anchor.TopCenter:    return new Vector2(0.5f, 1);
            case Anchor.TopRight:     return new Vector2(1, 1);
            case Anchor.BottomLeft:   return new Vector2(0, 0);
            case Anchor.BottomCenter: return new Vector2(0.5f, 0);
            case Anchor.BottomRight:  return new Vector2(1, 0);
            default:                  return new Vector2(0.5f, 0.5f);
        }
    }

    // anchor + pivot on the same point (like Shift + Alt in the anchor menu)
    static void Place(RectTransform rt, Anchor a, Vector2 pos, Vector2 size)
    {
        Vector2 p = AnchorPoint(a);
        rt.anchorMin = rt.anchorMax = rt.pivot = p;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    static void FillParent(RectTransform rt, float padX)
    {
        Stretch(rt);
        rt.offsetMin = new Vector2(padX, 0); rt.offsetMax = new Vector2(-padX, 0);
    }

    static void SetupScaler(Canvas canvas)
    {
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler cs = canvas.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 1f;       // landscape: match the height
        cs.referencePixelsPerUnit = 100;
        EditorUtility.SetDirty(cs);
    }

    static RectTransform SafeAreaIn(RectTransform parent)
    {
        RectTransform safe = Child(parent, "SafeArea") ?? NewRect("SafeArea", parent);
        Stretch(safe);
        if (safe.GetComponent<SafeArea>() == null) safe.gameObject.AddComponent<SafeArea>();
        return safe;
    }

    static RectTransform MakeBackground(RectTransform root, Image source)
    {
        RectTransform bg = Child(root, "Background") ?? NewRect("Background", root);
        Stretch(bg);
        Image img = Ensure<Image>(bg);
        if (source != null && source.sprite != null && AssetDatabase.GetAssetPath(source.sprite).StartsWith("Assets/"))
        {
            img.sprite = source.sprite;
            img.color = Color.white;
            img.preserveAspect = false;
        }
        else
        {
            img.sprite = null;
            img.color = new Color(0.10f, 0.16f, 0.32f, 1f);
        }
        img.raycastTarget = false;
        return bg;
    }

    static RectTransform Plate(RectTransform parent, string name, Anchor a, Vector2 pos, Vector2 size)
    {
        RectTransform plate = Child(parent, name) ?? NewRect(name, parent);
        Place(plate, a, pos, size);
        Image img = Ensure<Image>(plate);
        img.sprite = Rounded; img.type = Image.Type.Sliced; img.color = Navy; img.raycastTarget = false;
        return plate;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static RectTransform Move(RectTransform rt, Transform parent)
    {
        if (rt == null) throw new System.Exception("[Sky Bot] UI object not found under " + parent.name);
        if (rt.parent != parent) rt.SetParent(parent, false);
        return rt;
    }

    // finds the object either in its old place or already moved into the SafeArea (re-run)
    static RectTransform Grab(Transform oldParent, Transform safe, string name, string newName = null)
    {
        RectTransform rt = Child(oldParent, name) ?? (newName != null ? FindDeep(safe, newName) : Child(safe, name));
        Move(rt, safe);
        if (newName != null) rt.name = newName;
        return rt;
    }

    static T Ensure<T>(Component c) where T : Component
    {
        T t = c.GetComponent<T>();
        if (t == null) t = c.gameObject.AddComponent<T>();
        return t;
    }

    static RectTransform Child(Transform parent, string name, bool optional = false)
    {
        foreach (Transform c in parent)
            if (c.name == name) return c as RectTransform;
        return null;
    }

    static RectTransform FindDeep(Transform parent, string name)
    {
        foreach (RectTransform rt in parent.GetComponentsInChildren<RectTransform>(true))
            if (rt.name == name) return rt;
        return null;
    }

    static Canvas FindCanvas(Scene scene)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            Canvas c = go.GetComponent<Canvas>();
            if (c != null) return c;
        }
        throw new System.Exception("[Sky Bot] No Canvas in " + scene.path);
    }

    static TextMeshProUGUI MakeText(RectTransform parent, string name, string text, float size, FontStyles style, Color color)
    {
        RectTransform rt = Child(parent, name) ?? NewRect(name, parent);
        TextMeshProUGUI t = Ensure<TextMeshProUGUI>(rt);
        t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    static void StyleHudText(TextMeshProUGUI t, string placeholder, float size, TextAlignmentOptions align)
    {
        if (t == null) return;
        if (!string.IsNullOrEmpty(placeholder) && (string.IsNullOrEmpty(t.text) || t.text == "New Text" || t.text.StartsWith("Монеты")))
            t.text = placeholder;
        if (placeholder == "") t.text = "";
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
    }

    static void Label(RectTransform rt, string text, Vector2 pos)
    {
        Place(rt, Anchor.Center, pos, new Vector2(420, 100));
        TextMeshProUGUI t = rt.GetComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = 44; t.color = Color.white;
        t.alignment = TextAlignmentOptions.MidlineRight;
        t.textWrappingMode = TextWrappingModes.NoWrap;
    }

    static void StyleButton(Button b, string label, Color bg, Color fg, float size)
    {
        Image img = b.GetComponent<Image>();
        img.sprite = Rounded; img.type = Image.Type.Sliced; img.color = bg;
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
        cb.pressedColor = new Color(0.70f, 0.70f, 0.70f);
        cb.selectedColor = Color.white;
        b.colors = cb;

        TextMeshProUGUI t = b.GetComponentInChildren<TextMeshProUGUI>(true);
        if (t == null) t = MakeText(b.transform as RectTransform, "Text (TMP)", label, size, FontStyles.Bold, fg);
        Stretch(t.rectTransform);
        t.text = label; t.fontSize = size; t.fontStyle = FontStyles.Bold; t.color = fg;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        EditorUtility.SetDirty(b);
    }

    static Button MakeButton(RectTransform parent, string name, string label, Color bg, Color fg, float size, Vector2 pos, Vector2 dim)
    {
        RectTransform rt = Child(parent, name) ?? NewRect(name, parent);
        Place(rt, Anchor.Center, pos, dim);
        if (rt.GetComponent<Image>() == null) rt.gameObject.AddComponent<Image>();
        Button b = Ensure<Button>(rt);
        StyleButton(b, label, bg, fg, size);
        return b;
    }

    static void RoundControl(RectTransform rt, Vector2 pos, float size, Color bg, string label, Color fg, float fontSize)
    {
        Place(rt, Anchor.BottomRight, pos, new Vector2(size, size));
        Image img = rt.GetComponent<Image>();
        img.sprite = Circle; img.type = Image.Type.Simple; img.color = new Color(bg.r, bg.g, bg.b, 0.9f);
        TextMeshProUGUI t = rt.GetComponentInChildren<TextMeshProUGUI>(true);
        Stretch(t.rectTransform);
        t.text = label; t.fontSize = fontSize; t.fontStyle = FontStyles.Bold; t.color = fg;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
    }

    static void StyleInputField(TMP_InputField f)
    {
        Image img = f.GetComponent<Image>();
        img.sprite = Rounded; img.type = Image.Type.Sliced; img.color = new Color(1, 1, 1, 0.95f);
        if (f.textViewport != null) { f.textViewport.offsetMin = new Vector2(30, 10); f.textViewport.offsetMax = new Vector2(-30, -10); }
        foreach (TextMeshProUGUI t in f.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            t.fontSize = 48; t.alignment = TextAlignmentOptions.Center;
            t.color = t == f.placeholder ? new Color(0.3f, 0.3f, 0.35f, 0.8f) : Ink;
        }
        if (f.placeholder is TextMeshProUGUI ph) ph.text = "Your name...";
        f.pointSize = 48;
        EditorUtility.SetDirty(f);
    }

    static void StyleToggle(RectTransform rt, string label, Vector2 pos)
    {
        Place(rt, Anchor.Center, new Vector2(pos.x + 150, pos.y), new Vector2(560, 110));
        Toggle tg = rt.GetComponent<Toggle>();
        RectTransform box = Child(rt, "Background");
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0, 0.5f);
        box.anchoredPosition = Vector2.zero; box.sizeDelta = new Vector2(100, 100); box.localScale = Vector3.one;
        Image boxImg = box.GetComponent<Image>(); boxImg.sprite = Rounded; boxImg.type = Image.Type.Sliced;
        RectTransform check = Child(box, "Checkmark");
        check.anchorMin = check.anchorMax = check.pivot = new Vector2(0.5f, 0.5f);
        check.anchoredPosition = Vector2.zero; check.sizeDelta = new Vector2(70, 70); check.localScale = Vector3.one;
        RectTransform lab = Child(rt, "Label");
        lab.anchorMin = Vector2.zero; lab.anchorMax = Vector2.one; lab.pivot = new Vector2(0.5f, 0.5f);
        lab.offsetMin = new Vector2(130, 0); lab.offsetMax = Vector2.zero; lab.localScale = Vector3.one;
        Text legacy = lab.GetComponent<Text>();
        if (legacy != null)
        {
            legacy.text = label; legacy.fontSize = 48; legacy.color = Color.white;
            legacy.alignment = TextAnchor.MiddleLeft;
            legacy.horizontalOverflow = HorizontalWrapMode.Overflow;
            legacy.resizeTextForBestFit = false;
        }
        EditorUtility.SetDirty(tg);
    }

    static void StyleDropdown(TMP_Dropdown d)
    {
        Image img = d.GetComponent<Image>(); img.sprite = Rounded; img.type = Image.Type.Sliced; img.color = Color.white;
        d.captionText.fontSize = 44; d.captionText.color = Ink;
        RectTransform cap = d.captionText.rectTransform; cap.offsetMin = new Vector2(30, 0); cap.offsetMax = new Vector2(-80, 0);
        RectTransform arrow = Child(d.transform, "Arrow");
        if (arrow != null) { arrow.anchoredPosition = new Vector2(-45, 0); arrow.sizeDelta = new Vector2(50, 50); }

        RectTransform template = d.template;
        template.sizeDelta = new Vector2(0, 360);
        RectTransform content = FindDeep(template, "Content");
        content.sizeDelta = new Vector2(0, 110);
        RectTransform item = FindDeep(template, "Item");
        item.sizeDelta = new Vector2(0, 110);
        RectTransform mark = FindDeep(item, "Item Checkmark");
        mark.anchoredPosition = new Vector2(40, 0); mark.sizeDelta = new Vector2(50, 50);
        d.itemText.fontSize = 44; d.itemText.color = Ink;
        RectTransform it = d.itemText.rectTransform; it.offsetMin = new Vector2(90, 0); it.offsetMax = new Vector2(-20, 0);
        RectTransform sb = FindDeep(template, "Scrollbar");
        if (sb != null) sb.sizeDelta = new Vector2(40, 0);
        EditorUtility.SetDirty(d);
    }

    static void StyleSlider(Slider s)
    {
        RectTransform bg = Child(s.transform, "Background");
        if (bg != null) bg.GetComponent<Image>().color = new Color(1, 1, 1, 0.25f);
        if (s.fillRect != null) s.fillRect.GetComponent<Image>().color = Amber;
        RectTransform handleArea = Child(s.transform, "Handle Slide Area");
        if (handleArea != null) { handleArea.offsetMin = new Vector2(50, 0); handleArea.offsetMax = new Vector2(-50, 0); }
        RectTransform fillArea = Child(s.transform, "Fill Area");
        if (fillArea != null) { fillArea.offsetMin = new Vector2(10, 0); fillArea.offsetMax = new Vector2(-50, 0); }
        if (s.handleRect != null)
        {
            s.handleRect.sizeDelta = new Vector2(100, 0);
            Image h = s.handleRect.GetComponent<Image>(); h.sprite = Circle; h.color = Color.white;
        }
        EditorUtility.SetDirty(s);
    }

    static void Wire(Button b, UnityAction action)
    {
        for (int i = b.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(b.onClick, i);
        UnityEventTools.AddPersistentListener(b.onClick, action);
        EditorUtility.SetDirty(b);
    }

    static void Save(Scene s)
    {
        EditorSceneManager.MarkSceneDirty(s);
        EditorSceneManager.SaveScene(s);
    }
}
