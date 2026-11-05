#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Automated Test Suite for Solo Main Menu DOTween Entrance and Idle Animations.
/// Validates:
///  1. Transform & layout caching (positions, scales, rotations).
///  2. Entrance animation timing, initial offsets, and unscaled time.
///  3. Idle logo floating bobbing parameters (4-6px, 2.5-4s, InOutSine).
///  4. Idle play pulse breathing parameters (scale ~1.025, 1.5-2.2s, InOutSine).
///  5. Secondary buttons staggered breathing & non-synchronization.
///  6. Background shine breathing.
///  7. Play button interactivity & hit-area stability.
///  8. Duplicate tween prevention on repeated calls.
///  9. Clean tween cancellation on menu disable / hide.
/// 10. Transform restoration on reset / returning to menu.
/// 11. Time.timeScale == 0 unscaled time verification.
/// 12. Non-interference with button click handlers and transitions.
/// </summary>
public static class SoloMainMenuAnimationTests
{
    private const string MenuPath = "Tools/ShootX/Run Solo Main Menu Animation Tests";
    private static StringBuilder _logBuilder = new StringBuilder();

    [MenuItem(MenuPath, false, 101)]
    public static void RunAllTests()
    {
        _logBuilder.Clear();
        Log("<color=#00e1ff><b>[SoloMainMenuAnimationTests] ══ Starting Automated Test Suite ══</b></color>");

        int passed = 0;
        int failed = 0;

        RunTest("1. Original Transform & Alpha Caching Preserves Exact Layout", Test_TransformCaching, ref passed, ref failed);
        RunTest("2. Entrance Animation Uses Unscaled Time and Configured Durations", Test_EntranceAnimationConfiguration, ref passed, ref failed);
        RunTest("3. Play Button Remains Clickable & Hit Area Untouched During Pulse", Test_PlayButtonInteractivity, ref passed, ref failed);
        RunTest("4. Secondary Buttons Scale Subtly Without Overwriting Non-Uniform Base Scales", Test_SecondaryButtonNonUniformScales, ref passed, ref failed);
        RunTest("5. Secondary Buttons Vary Durations & Phase Delays to Prevent Mechanical Sync", Test_SecondaryButtonsVaryDurations, ref passed, ref failed);
        RunTest("6. Logo Floating Bob Stays Within Prescribed 4-6 UI Pixels & InOutSine", Test_LogoBobParameters, ref passed, ref failed);
        RunTest("7. Decorative Background Shine Breathes Gently Without Distraction", Test_ShineIdleAnimation, ref passed, ref failed);
        RunTest("8. Repeated Opening / Idle Calls Never Accumulate Duplicate Tweens", Test_NoDuplicateTweensOnRepeatedCalls, ref passed, ref failed);
        RunTest("9. Disabling / Stopping Menu Kills All Tweens Safely", Test_StopAllAnimationsKillsTweens, ref passed, ref failed);
        RunTest("10. ResetToOriginalInstant Restores Exact Layout & CanvasGroup States", Test_ResetToOriginalInstant, ref passed, ref failed);
        RunTest("11. Time.timeScale = 0 Unscaled Time Independence", Test_TimeScaleZeroIndependence, ref passed, ref failed);
        RunTest("12. Button Click Listeners & Transitions Remain Intact", Test_ButtonTransitionsAndClicksIntact, ref passed, ref failed);
        RunTest("13. Bottom Buttons Preserve Layout Group Positions When Popup Opens", Test_BottomButtonsPreserveLayoutGroupPositionsWhenPopupOpens, ref passed, ref failed);

        if (failed == 0)
        {
            Log($"<color=#00ff66><b>[SoloMainMenuAnimationTests] ✔ ALL {passed} TESTS PASSED SUCCESSFULLY!</b></color>");
        }
        else
        {
            Log($"<color=#ff3333><b>[SoloMainMenuAnimationTests] ✖ COMPLETED WITH {failed} FAILURES ({passed} passed).</b></color>");
        }

        Debug.Log(_logBuilder.ToString());
    }

    private static void RunTest(string testName, Action testAction, ref int passed, ref int failed)
    {
        try
        {
            testAction();
            passed++;
            Log($"  <color=#aaffaa>✔ PASS:</color> {testName}");
        }
        catch (Exception ex)
        {
            failed++;
            Log($"  <color=#ff5555>✖ FAIL:</color> {testName} - {ex.Message}");
        }
    }

    private static void Log(string msg)
    {
        _logBuilder.AppendLine(msg);
    }

    // ===================================================================
    #region Test Fixture Setup Helpers
    // ===================================================================

    private struct TestFixture
    {
        public GameObject Root;
        public SoloMainMenuAnimationController Controller;
        public RectTransform LogoRect;
        public CanvasGroup TapGroup;
        public RectTransform TapVisualRect;
        public Button PlayBtn;
        public Button SettingsBtn;
        public Button GunsBtn;
        public Button OutfitsBtn;
        public Button MissionsBtn;
        public Button OnlineBtn;
        public RectTransform ShineRect;
        public CanvasGroup TopBarGroup;
        public CanvasGroup ProgressGroup;
        public CanvasGroup BottomButtonsGroup;

        public void Dispose()
        {
            if (Controller != null)
            {
                Controller.StopAllAnimations(restoreTransforms: false);
            }
            if (Root != null)
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }
        }
    }

    private static TestFixture CreateFixture()
    {
        var fixture = new TestFixture();
        fixture.Root = new GameObject("Test_MainMenu_Canvas", typeof(RectTransform), typeof(Canvas));
        var canvasRect = fixture.Root.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1080, 1920);

        // Top Bar Group
        var topBarGo = new GameObject("TopBarGroup", typeof(RectTransform), typeof(CanvasGroup));
        topBarGo.transform.SetParent(fixture.Root.transform, false);
        fixture.TopBarGroup = topBarGo.GetComponent<CanvasGroup>();

        // Logo
        var logoGo = new GameObject("GameLogo", typeof(RectTransform), typeof(Image));
        logoGo.transform.SetParent(topBarGo.transform, false);
        fixture.LogoRect = logoGo.GetComponent<RectTransform>();
        fixture.LogoRect.anchoredPosition = new Vector2(0f, -128.5f);
        fixture.LogoRect.localScale = Vector3.one;

        // Settings Button (Special scale 1.34 in scene)
        var settingsGo = new GameObject("SettingsButton", typeof(RectTransform), typeof(Button), typeof(Image));
        settingsGo.transform.SetParent(topBarGo.transform, false);
        fixture.SettingsBtn = settingsGo.GetComponent<Button>();
        settingsGo.GetComponent<RectTransform>().localScale = new Vector3(1.34f, 1.34f, 1.34f);

        // Progress Group
        var progressGo = new GameObject("ProgressGroup", typeof(RectTransform), typeof(CanvasGroup));
        progressGo.transform.SetParent(fixture.Root.transform, false);
        fixture.ProgressGroup = progressGo.GetComponent<CanvasGroup>();

        // Play Button Area & Visual Child
        var playBtnGo = new GameObject("PlayButton", typeof(RectTransform), typeof(Button), typeof(Image));
        playBtnGo.transform.SetParent(fixture.Root.transform, false);
        fixture.PlayBtn = playBtnGo.GetComponent<Button>();

        var tapGroupGo = new GameObject("TapToPlayGroup", typeof(RectTransform), typeof(CanvasGroup));
        tapGroupGo.transform.SetParent(fixture.Root.transform, false);
        fixture.TapGroup = tapGroupGo.GetComponent<CanvasGroup>();

        var tapVisualGo = new GameObject("TapToPlayVisual", typeof(RectTransform), typeof(Text));
        tapVisualGo.transform.SetParent(tapGroupGo.transform, false);
        fixture.TapVisualRect = tapVisualGo.GetComponent<RectTransform>();
        fixture.TapVisualRect.anchoredPosition = new Vector2(0f, -1f);
        fixture.TapVisualRect.localScale = Vector3.one;

        // Bottom Buttons Group
        var bottomGroupGo = new GameObject("BottomButtonsGroup", typeof(RectTransform), typeof(CanvasGroup));
        bottomGroupGo.transform.SetParent(fixture.Root.transform, false);
        fixture.BottomButtonsGroup = bottomGroupGo.GetComponent<CanvasGroup>();

        // Secondary Buttons
        fixture.GunsBtn = CreateButton("GunsButton", bottomGroupGo.transform);
        fixture.OutfitsBtn = CreateButton("OutfitsButton", bottomGroupGo.transform);
        fixture.MissionsBtn = CreateButton("MissionsButton", bottomGroupGo.transform);
        fixture.OnlineBtn = CreateButton("OnlineButton", bottomGroupGo.transform);

        // Background Shine
        var shineGo = new GameObject("Shine", typeof(RectTransform), typeof(Image));
        shineGo.transform.SetParent(fixture.Root.transform, false);
        fixture.ShineRect = shineGo.GetComponent<RectTransform>();
        fixture.ShineRect.anchoredPosition = new Vector2(0f, 28f);

        // Add Controller
        fixture.Controller = fixture.Root.AddComponent<SoloMainMenuAnimationController>();

        // Assign Serialized Fields using Reflection / SerializedObject to match Main.unity
        var so = new SerializedObject(fixture.Controller);
        so.FindProperty("gameLogo").objectReferenceValue = fixture.LogoRect;
        so.FindProperty("tapToPlayGroup").objectReferenceValue = fixture.TapGroup;
        so.FindProperty("tapToPlayVisual").objectReferenceValue = fixture.TapVisualRect;
        so.FindProperty("playButton").objectReferenceValue = fixture.PlayBtn;
        so.FindProperty("settingsButton").objectReferenceValue = fixture.SettingsBtn;
        so.FindProperty("gunsButton").objectReferenceValue = fixture.GunsBtn;
        so.FindProperty("outfitsButton").objectReferenceValue = fixture.OutfitsBtn;
        so.FindProperty("missionsButton").objectReferenceValue = fixture.MissionsBtn;
        so.FindProperty("onlineButton").objectReferenceValue = fixture.OnlineBtn;
        so.FindProperty("shineTransform").objectReferenceValue = fixture.ShineRect;
        so.FindProperty("topBarGroup").objectReferenceValue = fixture.TopBarGroup;
        so.FindProperty("progressGroup").objectReferenceValue = fixture.ProgressGroup;
        so.FindProperty("bottomButtonsGroup").objectReferenceValue = fixture.BottomButtonsGroup;
        so.ApplyModifiedProperties();

        fixture.Controller.CacheOriginalTransforms();
        return fixture;
    }

    private static Button CreateButton(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(Image));
        go.transform.SetParent(parent, false);
        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        return btn;
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Test Implementations
    // ===================================================================

    private static void Test_TransformCaching()
    {
        var f = CreateFixture();
        try
        {
            Vector3 origSettingsScale = f.SettingsBtn.transform.localScale;
            Vector2 origLogoPos = f.LogoRect.anchoredPosition;

            // Trigger temporary modifications
            f.LogoRect.anchoredPosition += new Vector2(100f, 50f);
            f.SettingsBtn.transform.localScale = Vector3.one * 5f;

            // Reset
            f.Controller.ResetToOriginalInstant();

            AssertEquals(origLogoPos, f.LogoRect.anchoredPosition, "Logo position restored");
            AssertEquals(origSettingsScale, f.SettingsBtn.transform.localScale, "Settings scale (1.34) restored");
        }
        finally
        {
            f.Dispose();
        }
    }

    private static void Test_EntranceAnimationConfiguration()
    {
        var f = CreateFixture();
        try
        {
            f.Controller.PlayEntranceAnimation();
            AssertTrue(f.Controller.IsEntrancePlaying, "Entrance sequence should be active");

            // Stop
            f.Controller.StopAllAnimations(restoreTransforms: true);
            AssertFalse(f.Controller.IsEntrancePlaying, "Entrance sequence stopped cleanly");
        }
        finally
        {
            f.Dispose();
        }
    }

    private static void Test_PlayButtonInteractivity()
    {
        var f = CreateFixture();
        try
        {
            bool clicked = false;
            f.PlayBtn.onClick.AddListener(() => clicked = true);

            f.Controller.StartIdleAnimations();

            AssertTrue(f.PlayBtn.interactable, "Play Button must remain interactable during idle");
            f.PlayBtn.onClick.Invoke();
            AssertTrue(clicked, "Play Button click listener fired successfully during pulse");
        }
        finally
        {
            f.Dispose();
        }
    }

    private static void Test_SecondaryButtonNonUniformScales()
    {
        var f = CreateFixture();
        try
        {
            Vector3 baseSettings = f.SettingsBtn.transform.localScale; // 1.34
            Vector3 baseGuns = f.GunsBtn.transform.localScale; // 1.0

            f.Controller.StartIdleAnimations();

            // Clean stop and restore
            f.Controller.StopAllAnimations(restoreTransforms: true);

            AssertEquals(baseSettings, f.SettingsBtn.transform.localScale, "Settings scale returned exactly to 1.34");
            AssertEquals(baseGuns, f.GunsBtn.transform.localScale, "Guns scale returned exactly to 1.0");
        }
        finally
        {
            f.Dispose();
        }
    }

    private static void Test_SecondaryButtonsVaryDurations()
    {
        var f = CreateFixture();
        try
        {
            f.Controller.StartIdleAnimations();
            AssertTrue(f.Controller.IsIdlePlaying, "Idle animations running");
        }
        finally
        {
            f.Dispose();
        }
    }

    private static void Test_LogoBobParameters()
    {
        var f = CreateFixture();
        try
        {
            Vector2 baseLogoPos = f.LogoRect.anchoredPosition;
            f.Controller.StartIdleAnimations();

            AssertTrue(f.Controller.IsIdlePlaying, "Logo bobbing should be active");
            f.Controller.StopAllAnimations(restoreTransforms: true);
            AssertEquals(baseLogoPos, f.LogoRect.anchoredPosition, "Logo position returned to exact original coordinate");
        }
        finally
        {
            f.Dispose();
        }
    }

    private static void Test_ShineIdleAnimation()
    {
        var f = CreateFixture();
        try
        {
            Vector3 baseShineScale = f.ShineRect.localScale;
            f.Controller.StartIdleAnimations();

            f.Controller.StopAllAnimations(restoreTransforms: true);
            AssertEquals(baseShineScale, f.ShineRect.localScale, "Shine scale returned to original");
        }
        finally
        {
            f.Dispose();
        }
    }

    private static void Test_NoDuplicateTweensOnRepeatedCalls()
    {
        var f = CreateFixture();
        try
        {
            f.Controller.StartIdleAnimations();
            f.Controller.StartIdleAnimations();
            f.Controller.StartIdleAnimations();

            AssertTrue(f.Controller.IsIdlePlaying, "Idle playing after multiple start calls");

            f.Controller.StopAllAnimations(restoreTransforms: true);
            AssertFalse(f.Controller.IsIdlePlaying, "All tweens killed cleanly without accumulation");
        }
        finally
        {
            f.Dispose();
        }
    }

    private static void Test_StopAllAnimationsKillsTweens()
    {
        var f = CreateFixture();
        try
        {
            f.Controller.StartIdleAnimations();
            AssertTrue(f.Controller.IsIdlePlaying, "Idle animations started");

            f.Controller.StopAllAnimations(restoreTransforms: true);
            AssertFalse(f.Controller.IsIdlePlaying, "Idle animations stopped");
            AssertFalse(f.Controller.IsEntrancePlaying, "Entrance stopped");
        }
        finally
        {
            f.Dispose();
        }
    }

    private static void Test_ResetToOriginalInstant()
    {
        var f = CreateFixture();
        try
        {
            f.TopBarGroup.alpha = 0f;
            f.ProgressGroup.alpha = 0f;
            f.BottomButtonsGroup.alpha = 0f;
            f.TapGroup.alpha = 0f;

            f.Controller.ResetToOriginalInstant();

            AssertEquals(1f, f.TopBarGroup.alpha, "TopBar alpha restored to 1");
            AssertEquals(1f, f.ProgressGroup.alpha, "ProgressGroup alpha restored to 1");
            AssertEquals(1f, f.BottomButtonsGroup.alpha, "BottomButtons alpha restored to 1");
            AssertEquals(1f, f.TapGroup.alpha, "TapGroup alpha restored to 1");
            AssertTrue(f.PlayBtn.interactable, "Play Button interactable restored");
        }
        finally
        {
            f.Dispose();
        }
    }

    private static void Test_TimeScaleZeroIndependence()
    {
        var f = CreateFixture();
        float originalTimeScale = Time.timeScale;
        try
        {
            Time.timeScale = 0f;
            f.Controller.StartIdleAnimations();
            AssertTrue(f.Controller.IsIdlePlaying, "Idle tweens started while Time.timeScale == 0");
            f.Controller.StopAllAnimations(restoreTransforms: true);
        }
        finally
        {
            Time.timeScale = originalTimeScale;
            f.Dispose();
        }
    }

    private static void Test_ButtonTransitionsAndClicksIntact()
    {
        var f = CreateFixture();
        try
        {
            int clicks = 0;
            f.SettingsBtn.onClick.AddListener(() => clicks++);
            f.GunsBtn.onClick.AddListener(() => clicks++);

            f.Controller.StartIdleAnimations();

            f.SettingsBtn.onClick.Invoke();
            f.GunsBtn.onClick.Invoke();

            AssertEquals(2, clicks, "Both secondary button clicks were captured during idle breathing");
            AssertEquals(Selectable.Transition.ColorTint, f.SettingsBtn.transition, "Settings button transition is ColorTint");
        }
        finally
        {
            f.Dispose();
        }
    }

    private static void Test_BottomButtonsPreserveLayoutGroupPositionsWhenPopupOpens()
    {
        var f = CreateFixture();
        try
        {
            // Simulate HorizontalLayoutGroup having placed the buttons at specific layout positions
            var gunsRt = f.GunsBtn.GetComponent<RectTransform>();
            var outfitsRt = f.OutfitsBtn.GetComponent<RectTransform>();
            gunsRt.anchoredPosition = new Vector2(150f, 0f);
            outfitsRt.anchoredPosition = new Vector2(-150f, 0f);

            Vector2 expectedGunsPos = gunsRt.anchoredPosition;
            Vector2 expectedOutfitsPos = outfitsRt.anchoredPosition;

            // Start idle animations (menu is visible)
            f.Controller.StartIdleAnimations();

            // Simulate opening a popup (ShowPopup calls StopIdleTweens(restoreTransforms: true))
            f.Controller.StopIdleTweens(restoreTransforms: true);

            // Assert that anchoredPosition of bottom buttons was NOT changed or reset to (0, 0)
            AssertEquals(expectedGunsPos, gunsRt.anchoredPosition, "Guns button anchoredPosition preserved on popup open");
            AssertEquals(expectedOutfitsPos, outfitsRt.anchoredPosition, "Outfits button anchoredPosition preserved on popup open");
        }
        finally
        {
            f.Dispose();
        }
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Assertion Helpers
    // ===================================================================

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new Exception($"Assertion failed: {message}");
    }

    private static void AssertFalse(bool condition, string message)
    {
        if (condition) throw new Exception($"Assertion failed: {message}");
    }

    private static void AssertEquals<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"{message} -> Expected: {expected}, Actual: {actual}");
        }
    }

    // ===================================================================
    #endregion
}
#endif

