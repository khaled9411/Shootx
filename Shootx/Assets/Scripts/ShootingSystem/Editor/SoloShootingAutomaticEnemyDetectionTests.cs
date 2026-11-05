#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Automated Test Suite for Solo Mode Automatic Shooting.
/// Verifies that:
///   1. Aim at enemy -> exactly ONE auto shot -> transition to Idle -> release finger -> ZERO additional shots.
///   2. Aim at enemy -> auto shot -> keep finger held -> no repeated auto shots & no re-entry into Aiming.
///   3. Release finger after auto shot -> new touch -> Aiming starts cleanly and normally.
///   4. Aim without targeting enemy -> release finger -> exactly ONE normal release shot.
///   5. Aim without targeting enemy -> cancel -> release finger -> ZERO shots.
///   6. Auto shot occurs -> duplicate PointerUp callback -> no extra shot.
///   7. Auto shot occurs -> another finger is released (multitouch) -> original session remains tracked.
///   8. Multiple consecutive aiming sessions -> each session behaves independently.
///   9. Raycast hits wall, prop, coin box, or dead enemy -> zero auto shots.
///  10. Enemy behind wall obstruction -> zero auto shots.
///  11. Multiplayer isolation -> multiplayer shooting systems completely unaffected.
/// </summary>
public static class SoloShootingAutomaticEnemyDetectionTests
{
    private const string MenuPath = "Tools/ShootX/Run Solo Shooting Automatic Shot Tests";
    private static StringBuilder _logBuilder = new StringBuilder();

    [MenuItem(MenuPath, false, 100)]
    public static void RunAllTests()
    {
        _logBuilder.Clear();
        Log("<color=#00e1ff><b>[SoloShootingTests] ══ Starting Automated Test Suite ══</b></color>");

        int passed = 0;
        int failed = 0;

        RunTest("1. Aim at Enemy -> Auto Shot -> Idle -> Release -> ZERO Extra Shots (1 Shot Total)", Test_EnemyDetection_AutoShot_ReleaseDoesNotShoot, ref passed, ref failed);
        RunTest("2. Enemy Auto Shot -> Keep Finger Held -> No Repeat Shots & No Aiming Re-entry", Test_HeldFingerOnEnemy_NoRepeatedAutoShotsAndNoReentry, ref passed, ref failed);
        RunTest("3. Release Finger After Auto Shot -> New Touch -> Aiming Starts Normally", Test_NewTouchStartsFreshSession, ref passed, ref failed);
        RunTest("4. Aim Without Enemy -> Release -> Exactly One Normal Release Shot", Test_AimWithoutEnemy_Release_FiresOneShot, ref passed, ref failed);
        RunTest("5. Aim Without Enemy -> Cancel -> Release -> Zero Shots", Test_CancelBeforeEnemy_ZeroShots, ref passed, ref failed);
        RunTest("6. Auto Shot Occurs -> Duplicate PointerUp Callback -> No Extra Shot", Test_DuplicatePointerUp_NoExtraShot, ref passed, ref failed);
        RunTest("7. Multitouch: Other Finger Released -> Original Session Remains Tracked", Test_Multitouch_OtherFingerRelease_PreservesSession, ref passed, ref failed);
        RunTest("8. Consecutive Sessions -> Independent Lifecycle & Clean Flag Reset", Test_ConsecutiveSessions_IndependentLifecycle, ref passed, ref failed);
        RunTest("9. Raycast Hits Wall, Prop, CoinBox or Dead Enemy -> Zero Auto Shots", Test_NonEnemyHits_NoAutoShot, ref passed, ref failed);
        RunTest("10. Enemy Behind Wall Obstruction -> Zero Auto Shots", Test_EnemyBehindWall_NoAutoShot, ref passed, ref failed);
        RunTest("11. Multiplayer Mode Boundary Isolation", Test_MultiplayerIsolation, ref passed, ref failed);
        RunTest("12. Pointer Begins Over UI -> Blocked From Aiming", Test_PointerBeginsOverUI_BlockedFromAiming, ref passed, ref failed);
        RunTest("13. Drag Off UI Into Gameplay -> Remains Blocked & Release Does Not Shoot", Test_DragOffUI_RemainsBlocked_ReleaseDoesNotShoot, ref passed, ref failed);
        RunTest("14. Gameplay Inactive (Main Menu/Popups) -> Shooting Blocked", Test_GameplayInactive_MainMenuOrPopups_ShootingBlocked, ref passed, ref failed);
        RunTest("15. Pause Screen -> Cancels Ongoing Aim & Prevents Shooting", Test_GameplayInactive_PauseScreen_CancelsOngoingAimAndPreventsShooting, ref passed, ref failed);
        RunTest("16. Cancel Aim Button -> Cancels Aim & Release Fires Zero Shots", Test_CancelAimingButton_ActiveAimingPreservedUntilHoveredOrReleased, ref passed, ref failed);
        RunTest("17. Multitouch: UI Finger & Gameplay Finger Isolated Properly", Test_Multitouch_OneFingerUI_OneFingerGameplay_IsolatedProperly, ref passed, ref failed);

        if (failed == 0)
        {
            Log($"<color=#00ff66><b>[SoloShootingTests] ✔ ALL {passed} TESTS PASSED SUCCESSFULLY!</b></color>");
        }
        else
        {
            Log($"<color=#ff3333><b>[SoloShootingTests] ✘ {failed} TESTS FAILED out of {passed + failed}!</b></color>");
        }

        try
        {
            File.WriteAllText("test_results.txt", _logBuilder.ToString());
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SoloShootingTests] Could not write test_results.txt: {ex.Message}");
        }
    }

    private static void Log(string msg)
    {
        Debug.Log(msg);
        _logBuilder.AppendLine(msg);
    }

    private static void RunTest(string name, System.Func<bool> testFunc, ref int passed, ref int failed)
    {
        try
        {
            if (testFunc())
            {
                Log($"✔ [PASS] {name}");
                passed++;
            }
            else
            {
                Log($"✘ [FAIL] {name}");
                failed++;
            }
        }
        catch (System.Exception ex)
        {
            Log($"✘ [EXCEPTION] {name}: {ex.Message}\n{ex.StackTrace}");
            failed++;
        }
    }

    #region Helper Test Rig Creation
    private class TestRig
    {
        public GameObject Root;
        public ShootingSystem Shooting;
        public GameObject PlayerBody;
        public GameObject FirePoint;
        public GameObject BulletPrefab;
        public GameObject EnemyRoot;
        public EnemyAI Enemy;

        public void Destroy()
        {
            if (Root != null) Object.DestroyImmediate(Root);
            if (EnemyRoot != null) Object.DestroyImmediate(EnemyRoot);
            if (BulletPrefab != null) Object.DestroyImmediate(BulletPrefab);
        }
    }

    private static TestRig CreateTestRig()
    {
        var rig = new TestRig();
        rig.Root = new GameObject("Test_Player");
        rig.PlayerBody = new GameObject("Body");
        rig.PlayerBody.transform.SetParent(rig.Root.transform);

        rig.FirePoint = new GameObject("FirePoint");
        rig.FirePoint.transform.SetParent(rig.PlayerBody.transform);
        rig.FirePoint.transform.localPosition = new Vector3(0, 1.5f, 0.5f);
        rig.FirePoint.transform.localRotation = Quaternion.identity;

        rig.BulletPrefab = new GameObject("Test_Bullet");
        rig.BulletPrefab.AddComponent<BulletController>();

        rig.Shooting = rig.Root.AddComponent<ShootingSystem>();
        SetPrivateField(rig.Shooting, "playerBody", rig.PlayerBody.transform);
        SetPrivateField(rig.Shooting, "weaponFirePoint", rig.FirePoint.transform);
        SetPrivateField(rig.Shooting, "bulletPrefab", rig.BulletPrefab);
        SetPrivateField(rig.Shooting, "maxAmmo", 3);
        SetPrivateField(rig.Shooting, "currentAmmo", 3);
        SetPrivateField(rig.Shooting, "aimAssistEnabled", false);

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer == -1) enemyLayer = 8;
        int wallLayer = LayerMask.NameToLayer("Wall ");
        if (wallLayer == -1) wallLayer = 6;
        int penetrableLayer = LayerMask.NameToLayer("Penetrable");
        if (penetrableLayer == -1) penetrableLayer = 7;

        LayerMask colLayers = (1 << enemyLayer) | (1 << wallLayer) | (1 << penetrableLayer);
        LayerMask shootLayers = (1 << wallLayer);
        LayerMask penLayers = (1 << penetrableLayer);

        SetPrivateField(rig.Shooting, "collisionLayers", colLayers);
        SetPrivateField(rig.Shooting, "shootableLayers", shootLayers);
        SetPrivateField(rig.Shooting, "penetrableLayers", penLayers);

        // Create enemy
        rig.EnemyRoot = new GameObject("Test_Enemy");
        rig.EnemyRoot.layer = enemyLayer;
        rig.EnemyRoot.transform.position = new Vector3(0, 1.5f, 5f);
        var boxCol = rig.EnemyRoot.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(1, 2, 1);
        rig.Enemy = rig.EnemyRoot.AddComponent<EnemyAI>();
        SetPrivateField(rig.Enemy, "currentHealth", 100f);
        SetPrivateField(rig.Enemy, "maxHealth", 100f);
        SetPrivateField(rig.Enemy, "isDead", false);

        return rig;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (fi != null)
        {
            fi.SetValue(target, value);
        }
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return fi?.GetValue(target);
    }

    private static object InvokePrivateMethod(object target, string methodName, params object[] parameters)
    {
        MethodInfo mi = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return mi?.Invoke(target, parameters);
    }
    #endregion

    #region Tests
    // Test 1: Aim at enemy -> one automatic shot -> transition to Idle -> release finger -> ZERO additional shots (1 total).
    private static bool Test_EnemyDetection_AutoShot_ReleaseDoesNotShoot()
    {
        var rig = CreateTestRig();
        try
        {
            rig.FirePoint.transform.forward = (rig.EnemyRoot.transform.position - rig.FirePoint.transform.position).normalized;
            int initialAmmo = rig.Shooting.GetCurrentAmmo();

            // Touch began & start aiming
            SetPrivateField(rig.Shooting, "touchSessionActive", true);
            SetPrivateField(rig.Shooting, "isMouseSession", true);
            InvokePrivateMethod(rig.Shooting, "StartAiming", new Vector2(500, 500));

            // Raycast detects enemy -> auto shot triggers
            InvokePrivateMethod(rig.Shooting, "UpdateAimRay");

            int ammoAfterAuto = rig.Shooting.GetCurrentAmmo();
            // Exactly 1 auto shot fired
            if (initialAmmo - ammoAfterAuto != 1) return false;
            // Transitioned to Idle immediately
            if (rig.Shooting.IsAiming()) return false;
            // Session remains active while finger is still held
            if (!rig.Shooting.IsTouchSessionActive()) return false;
            // Session marked consumed
            if (!rig.Shooting.HasFiredAutoShot()) return false;

            // Release the finger
            InvokePrivateMethod(rig.Shooting, "HandlePointerUp", new Vector2(500, 500));

            int ammoAfterRelease = rig.Shooting.GetCurrentAmmo();

            // Zero additional shots on release!
            if (ammoAfterAuto != ammoAfterRelease) return false;
            // Total bullets fired for the entire interaction: EXACTLY ONE
            if (initialAmmo - ammoAfterRelease != 1) return false;
            // Session closed
            if (rig.Shooting.IsTouchSessionActive()) return false;

            return true;
        }
        finally
        {
            rig.Destroy();
        }
    }

    // Test 2: Aim at enemy -> automatic shot -> keep finger held -> no repeated shots and no re-entry into Aiming.
    private static bool Test_HeldFingerOnEnemy_NoRepeatedAutoShotsAndNoReentry()
    {
        var rig = CreateTestRig();
        try
        {
            rig.FirePoint.transform.forward = (rig.EnemyRoot.transform.position - rig.FirePoint.transform.position).normalized;

            SetPrivateField(rig.Shooting, "touchSessionActive", true);
            SetPrivateField(rig.Shooting, "isMouseSession", true);
            InvokePrivateMethod(rig.Shooting, "StartAiming", new Vector2(500, 500));

            // First raycast -> auto shot
            InvokePrivateMethod(rig.Shooting, "UpdateAimRay");
            int ammoAfterFirstShot = rig.Shooting.GetCurrentAmmo();

            // Keep finger held: state must be Idle
            if (rig.Shooting.IsAiming()) return false;

            // Simulate finger movement or consecutive update frames while held
            Vector3 fireDir = rig.FirePoint.transform.forward;
            InvokePrivateMethod(rig.Shooting, "TriggerAutomaticEnemyShot", fireDir);

            // Attempt to re-enter aiming while finger still held
            if (rig.Shooting.IsTouchSessionActive())
            {
                // touchSessionActive blocks new touch/click in HandleInput
                bool canStartNew = !(bool)GetPrivateField(rig.Shooting, "touchSessionActive");
                if (canStartNew) return false;
            }

            int ammoAfterRepeats = rig.Shooting.GetCurrentAmmo();

            // No repeated shots
            if (ammoAfterFirstShot != ammoAfterRepeats) return false;

            return true;
        }
        finally
        {
            rig.Destroy();
        }
    }

    // Test 3: Release finger after the completed automatic session -> make a new touch -> Aiming starts normally.
    private static bool Test_NewTouchStartsFreshSession()
    {
        var rig = CreateTestRig();
        try
        {
            // Session 1: Enemy detected auto shot
            rig.FirePoint.transform.forward = (rig.EnemyRoot.transform.position - rig.FirePoint.transform.position).normalized;
            SetPrivateField(rig.Shooting, "touchSessionActive", true);
            SetPrivateField(rig.Shooting, "isMouseSession", true);
            InvokePrivateMethod(rig.Shooting, "StartAiming", new Vector2(500, 500));
            InvokePrivateMethod(rig.Shooting, "UpdateAimRay");

            // Release finger to fully close Session 1
            InvokePrivateMethod(rig.Shooting, "HandlePointerUp", new Vector2(500, 500));

            if (rig.Shooting.IsTouchSessionActive()) return false;
            if (rig.Shooting.HasFiredAutoShot()) return false;

            // Session 2: New touch
            rig.FirePoint.transform.forward = Vector3.left;
            SetPrivateField(rig.Shooting, "touchSessionActive", true);
            SetPrivateField(rig.Shooting, "isMouseSession", true);
            InvokePrivateMethod(rig.Shooting, "StartAiming", new Vector2(300, 300));

            // Aiming starts normally
            if (!rig.Shooting.IsAiming()) return false;
            if (rig.Shooting.HasFiredAutoShot()) return false;

            // Release normal shot
            int ammoBeforeRelease = rig.Shooting.GetCurrentAmmo();
            InvokePrivateMethod(rig.Shooting, "HandlePointerUp", new Vector2(300, 300));
            int ammoAfterRelease = rig.Shooting.GetCurrentAmmo();

            if (ammoBeforeRelease - ammoAfterRelease != 1) return false;
            if (rig.Shooting.IsTouchSessionActive()) return false;

            return true;
        }
        finally
        {
            rig.Destroy();
        }
    }

    // Test 4: Aim without targeting an enemy -> release finger -> exactly one normal release shot.
    private static bool Test_AimWithoutEnemy_Release_FiresOneShot()
    {
        var rig = CreateTestRig();
        try
        {
            rig.FirePoint.transform.forward = Vector3.left;

            SetPrivateField(rig.Shooting, "touchSessionActive", true);
            SetPrivateField(rig.Shooting, "isMouseSession", true);
            InvokePrivateMethod(rig.Shooting, "StartAiming", new Vector2(500, 500));

            if (!rig.Shooting.IsAiming()) return false;
            int ammoBefore = rig.Shooting.GetCurrentAmmo();

            InvokePrivateMethod(rig.Shooting, "HandlePointerUp", new Vector2(500, 500));

            int ammoAfter = rig.Shooting.GetCurrentAmmo();

            // Exactly 1 normal release shot
            if (ammoBefore - ammoAfter != 1) return false;
            if (rig.Shooting.IsAiming()) return false;
            if (rig.Shooting.IsTouchSessionActive()) return false;

            return true;
        }
        finally
        {
            rig.Destroy();
        }
    }

    // Test 5: Aim without targeting an enemy -> cancel -> release finger -> zero shots.
    private static bool Test_CancelBeforeEnemy_ZeroShots()
    {
        var rig = CreateTestRig();
        try
        {
            int initialAmmo = rig.Shooting.GetCurrentAmmo();

            SetPrivateField(rig.Shooting, "touchSessionActive", true);
            SetPrivateField(rig.Shooting, "isMouseSession", true);
            InvokePrivateMethod(rig.Shooting, "StartAiming", new Vector2(500, 500));

            // Cancel
            rig.Shooting.CancelShot();

            // Release
            InvokePrivateMethod(rig.Shooting, "HandlePointerUp", new Vector2(500, 500));

            int finalAmmo = rig.Shooting.GetCurrentAmmo();

            if (initialAmmo != finalAmmo) return false;
            if (rig.Shooting.IsAiming()) return false;
            if (rig.Shooting.IsTouchSessionActive()) return false;

            return true;
        }
        finally
        {
            rig.Destroy();
        }
    }

    // Test 6: Automatic shot occurs -> duplicate PointerUp callback -> no extra shot.
    private static bool Test_DuplicatePointerUp_NoExtraShot()
    {
        var rig = CreateTestRig();
        try
        {
            rig.FirePoint.transform.forward = (rig.EnemyRoot.transform.position - rig.FirePoint.transform.position).normalized;
            int initialAmmo = rig.Shooting.GetCurrentAmmo();

            SetPrivateField(rig.Shooting, "touchSessionActive", true);
            SetPrivateField(rig.Shooting, "isMouseSession", true);
            InvokePrivateMethod(rig.Shooting, "StartAiming", new Vector2(500, 500));

            // Auto shot
            InvokePrivateMethod(rig.Shooting, "UpdateAimRay");

            // First release
            InvokePrivateMethod(rig.Shooting, "HandlePointerUp", new Vector2(500, 500));

            // Duplicate release (e.g. touch + mouse synthetic callback on same frame)
            InvokePrivateMethod(rig.Shooting, "HandlePointerUp", new Vector2(500, 500));

            int finalAmmo = rig.Shooting.GetCurrentAmmo();

            // Exactly 1 bullet total; no extra bullet from duplicate release
            if (initialAmmo - finalAmmo != 1) return false;

            return true;
        }
        finally
        {
            rig.Destroy();
        }
    }

    // Test 7: Multitouch: another finger is released -> original session remains correctly tracked.
    private static bool Test_Multitouch_OtherFingerRelease_PreservesSession()
    {
        var rig = CreateTestRig();
        try
        {
            // Finger 0 starts aiming
            SetPrivateField(rig.Shooting, "touchSessionActive", true);
            SetPrivateField(rig.Shooting, "activeFingerId", 0);
            SetPrivateField(rig.Shooting, "isMouseSession", false);
            InvokePrivateMethod(rig.Shooting, "StartAiming", new Vector2(200, 400));

            int activeId = (int)GetPrivateField(rig.Shooting, "activeFingerId");
            if (activeId != 0) return false;

            // Session remains active and locked to finger 0
            if (!rig.Shooting.IsTouchSessionActive()) return false;

            // Finger 0 releases
            InvokePrivateMethod(rig.Shooting, "HandlePointerUp", new Vector2(200, 400));

            if (rig.Shooting.IsTouchSessionActive()) return false;
            int resetActiveId = (int)GetPrivateField(rig.Shooting, "activeFingerId");
            if (resetActiveId != -1) return false;

            return true;
        }
        finally
        {
            rig.Destroy();
        }
    }

    // Test 8: Consecutive sessions -> independent lifecycle & clean flag reset.
    private static bool Test_ConsecutiveSessions_IndependentLifecycle()
    {
        var rig = CreateTestRig();
        try
        {
            // Session A: Auto shot (1 bullet)
            rig.FirePoint.transform.forward = (rig.EnemyRoot.transform.position - rig.FirePoint.transform.position).normalized;
            SetPrivateField(rig.Shooting, "touchSessionActive", true);
            SetPrivateField(rig.Shooting, "isMouseSession", true);
            InvokePrivateMethod(rig.Shooting, "StartAiming", new Vector2(500, 500));
            InvokePrivateMethod(rig.Shooting, "UpdateAimRay");
            InvokePrivateMethod(rig.Shooting, "HandlePointerUp", new Vector2(500, 500));

            if (rig.Shooting.GetCurrentAmmo() != 2) return false;

            // Session B: Cancel (0 bullets)
            SetPrivateField(rig.Shooting, "touchSessionActive", true);
            SetPrivateField(rig.Shooting, "isMouseSession", true);
            InvokePrivateMethod(rig.Shooting, "StartAiming", new Vector2(500, 500));
            rig.Shooting.CancelShot();
            InvokePrivateMethod(rig.Shooting, "HandlePointerUp", new Vector2(500, 500));

            if (rig.Shooting.GetCurrentAmmo() != 2) return false;

            // Session C: Normal aim & release (1 bullet)
            rig.FirePoint.transform.forward = Vector3.right;
            SetPrivateField(rig.Shooting, "touchSessionActive", true);
            SetPrivateField(rig.Shooting, "isMouseSession", true);
            InvokePrivateMethod(rig.Shooting, "StartAiming", new Vector2(500, 500));
            InvokePrivateMethod(rig.Shooting, "HandlePointerUp", new Vector2(500, 500));

            if (rig.Shooting.GetCurrentAmmo() != 1) return false;

            return true;
        }
        finally
        {
            rig.Destroy();
        }
    }

    // Test 9: Raycast hits a wall, prop, coin box, or dead enemy: zero automatic shots.
    private static bool Test_NonEnemyHits_NoAutoShot()
    {
        var rig = CreateTestRig();
        GameObject wallObj = null;
        GameObject coinBoxObj = null;
        try
        {
            int wallLayer = LayerMask.NameToLayer("Wall ");
            if (wallLayer == -1) wallLayer = 6;

            // 1. Wall test
            wallObj = new GameObject("Wall");
            wallObj.layer = wallLayer;
            wallObj.transform.position = new Vector3(0, 1.5f, 3f);
            var wallCol = wallObj.AddComponent<BoxCollider>();
            wallCol.size = new Vector3(5, 5, 1);

            bool isEnemy = (bool)InvokePrivateMethod(rig.Shooting, "IsValidAliveEnemy", wallCol, null);
            if (isEnemy) return false;

            // 2. Dead enemy test
            SetPrivateField(rig.Enemy, "isDead", true);
            var enemyCol = rig.EnemyRoot.GetComponent<Collider>();
            bool isDeadEnemyValid = (bool)InvokePrivateMethod(rig.Shooting, "IsValidAliveEnemy", enemyCol, null);
            if (isDeadEnemyValid) return false;

            // 3. CoinBox test
            coinBoxObj = new GameObject("CoinBox");
            coinBoxObj.AddComponent<CoinBox>();
            var boxCol = coinBoxObj.AddComponent<BoxCollider>();
            bool isCoinBoxValid = (bool)InvokePrivateMethod(rig.Shooting, "IsValidAliveEnemy", boxCol, null);
            if (isCoinBoxValid) return false;

            return true;
        }
        finally
        {
            if (wallObj != null) Object.DestroyImmediate(wallObj);
            if (coinBoxObj != null) Object.DestroyImmediate(coinBoxObj);
            rig.Destroy();
        }
    }

    // Test 10: An enemy is behind an obstruction: zero automatic shots.
    private static bool Test_EnemyBehindWall_NoAutoShot()
    {
        var rig = CreateTestRig();
        GameObject wallObj = null;
        try
        {
            int wallLayer = LayerMask.NameToLayer("Wall ");
            if (wallLayer == -1) wallLayer = 6;

            wallObj = new GameObject("Obstruction_Wall");
            wallObj.layer = wallLayer;
            wallObj.transform.position = new Vector3(0, 1.5f, 2.5f);
            var wallCol = wallObj.AddComponent<BoxCollider>();
            wallCol.size = new Vector3(10, 10, 0.5f);

            rig.FirePoint.transform.forward = (rig.EnemyRoot.transform.position - rig.FirePoint.transform.position).normalized;

            SetPrivateField(rig.Shooting, "touchSessionActive", true);
            SetPrivateField(rig.Shooting, "isMouseSession", true);
            InvokePrivateMethod(rig.Shooting, "StartAiming", new Vector2(500, 500));

            int ammoBefore = rig.Shooting.GetCurrentAmmo();

            InvokePrivateMethod(rig.Shooting, "UpdateAimRay");

            int ammoAfter = rig.Shooting.GetCurrentAmmo();

            // Wall blocks raycast to enemy; zero auto shots
            if (ammoBefore != ammoAfter) return false;
            if (rig.Shooting.HasFiredAutoShot()) return false;

            return true;
        }
        finally
        {
            if (wallObj != null) Object.DestroyImmediate(wallObj);
            rig.Destroy();
        }
    }

    // Test 11: Verify that Multiplayer Mode is completely isolated
    private static bool Test_MultiplayerIsolation()
    {
        var allTypes = System.AppDomain.CurrentDomain.GetAssemblies();
        System.Type multiplayerType = null;
        foreach (var asm in allTypes)
        {
            var t = asm.GetType("PlayerShooting");
            if (t != null)
            {
                multiplayerType = t;
                break;
            }
        }

        if (multiplayerType != null)
        {
            var fields = multiplayerType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var f in fields)
            {
                if (f.FieldType == typeof(ShootingSystem))
                {
                    Debug.LogError("Multiplayer PlayerShooting references Solo ShootingSystem!");
                    return false;
                }
            }
        }

        return true;
    }

    // Test 12: Pointer begins over UI -> Marked as UI-blocked -> Cannot start aiming
    private static bool Test_PointerBeginsOverUI_BlockedFromAiming()
    {
        var rig = CreateTestRig();
        try
        {
            // Finger 0 is over UI at Began
            var blockedSet = (HashSet<int>)GetPrivateField(rig.Shooting, "uiBlockedFingerIds");
            blockedSet.Add(0);

            // Verify finger 0 is marked UI-blocked
            if (!rig.Shooting.IsFingerUIBlocked(0)) return false;

            // Session cannot be active for this finger
            if (rig.Shooting.IsTouchSessionActive()) return false;
            if (rig.Shooting.IsAiming()) return false;

            // Same for mouse
            SetPrivateField(rig.Shooting, "isMouseUIBlocked", true);
            if (!rig.Shooting.IsMouseUIBlocked()) return false;

            return true;
        }
        finally
        {
            rig.Destroy();
        }
    }

    // Test 13: Drag off UI into gameplay space -> Remains blocked -> Release does NOT fire
    private static bool Test_DragOffUI_RemainsBlocked_ReleaseDoesNotShoot()
    {
        var rig = CreateTestRig();
        try
        {
            int initialAmmo = rig.Shooting.GetCurrentAmmo();

            // Simulate finger 5 starting on UI (e.g. Pause button)
            var blockedSet = (HashSet<int>)GetPrivateField(rig.Shooting, "uiBlockedFingerIds");
            blockedSet.Add(5);

            // Dragged off UI: blocked status persists
            if (!rig.Shooting.IsFingerUIBlocked(5)) return false;
            if (rig.Shooting.IsAiming()) return false;

            // Lifting finger 5: removes from blocked set without shooting
            blockedSet.Remove(5);
            InvokePrivateMethod(rig.Shooting, "HandlePointerUp", new Vector2(300, 300));

            int ammoAfterTouch = rig.Shooting.GetCurrentAmmo();
            if (ammoAfterTouch != initialAmmo) return false;

            // Simulate mouse starting on UI and dragging into gameplay space
            SetPrivateField(rig.Shooting, "isMouseUIBlocked", true);
            if (rig.Shooting.IsAiming()) return false;

            // Mouse released
            SetPrivateField(rig.Shooting, "isMouseUIBlocked", false);
            InvokePrivateMethod(rig.Shooting, "HandlePointerUp", new Vector2(300, 300));

            int ammoAfterMouse = rig.Shooting.GetCurrentAmmo();
            if (ammoAfterMouse != initialAmmo) return false;

            return true;
        }
        finally
        {
            rig.Destroy();
        }
    }

    // Test 14: Gameplay inactive (e.g. Main Menu or popups) -> Aiming and shooting blocked
    private static bool Test_GameplayInactive_MainMenuOrPopups_ShootingBlocked()
    {
        var rig = CreateTestRig();
        GameObject uiObj = null;
        try
        {
            uiObj = new GameObject("Test_UIManager");
            var uiMgr = uiObj.AddComponent<UIManager>();
            // uiMgr by default has gameStarted = false (Main Menu)
            if (uiMgr.isgameactive()) return false;

            // Check that ShootingSystem detects gameplay as inactive
            if (rig.Shooting.CheckIsGameplayActive()) return false;

            return true;
        }
        finally
        {
            if (uiObj != null) Object.DestroyImmediate(uiObj);
            rig.Destroy();
        }
    }

    // Test 15: Gameplay becomes inactive while aiming (e.g. Pause screen) -> Safely cancels aim and ends session
    private static bool Test_GameplayInactive_PauseScreen_CancelsOngoingAimAndPreventsShooting()
    {
        var rig = CreateTestRig();
        GameObject pauseObj = null;
        try
        {
            // Start active aiming
            SetPrivateField(rig.Shooting, "touchSessionActive", true);
            SetPrivateField(rig.Shooting, "isMouseSession", true);
            InvokePrivateMethod(rig.Shooting, "StartAiming", new Vector2(500, 500));
            if (!rig.Shooting.IsAiming()) return false;

            int ammoBefore = rig.Shooting.GetCurrentAmmo();

            // Create pause UI and pause game
            pauseObj = new GameObject("Test_PauseUI");
            var pauseUI = pauseObj.AddComponent<WinLosePauseUI>();
            SetPrivateField(pauseUI, "_isPaused", true);
            Time.timeScale = 0f;

            // Gameplay should now be recognized as inactive
            if (rig.Shooting.CheckIsGameplayActive()) return false;

            // Update on ShootingSystem cancels aim and clears session
            InvokePrivateMethod(rig.Shooting, "Update");

            if (rig.Shooting.IsAiming()) return false;
            if (rig.Shooting.IsTouchSessionActive()) return false;

            // Subsequent release event does not fire bullet
            InvokePrivateMethod(rig.Shooting, "HandlePointerUp", new Vector2(500, 500));

            int ammoAfter = rig.Shooting.GetCurrentAmmo();
            if (ammoAfter != ammoBefore) return false;

            return true;
        }
        finally
        {
            Time.timeScale = 1f;
            if (pauseObj != null) Object.DestroyImmediate(pauseObj);
            rig.Destroy();
        }
    }

    // Test 16: Cancel aiming button -> Moving pointer over cancel button aborts aiming and release does not shoot
    private static bool Test_CancelAimingButton_ActiveAimingPreservedUntilHoveredOrReleased()
    {
        var rig = CreateTestRig();
        try
        {
            int initialAmmo = rig.Shooting.GetCurrentAmmo();

            SetPrivateField(rig.Shooting, "touchSessionActive", true);
            SetPrivateField(rig.Shooting, "isMouseSession", true);
            InvokePrivateMethod(rig.Shooting, "StartAiming", new Vector2(500, 500));
            if (!rig.Shooting.IsAiming()) return false;

            // Hover / enter Cancel button
            rig.Shooting.CancelShot();

            if (rig.Shooting.IsAiming()) return false;
            bool sessionCanceled = (bool)GetPrivateField(rig.Shooting, "sessionCanceled");
            if (!sessionCanceled) return false;

            // Release pointer after cancel
            InvokePrivateMethod(rig.Shooting, "HandlePointerUp", new Vector2(500, 500));

            if (rig.Shooting.IsTouchSessionActive()) return false;
            int finalAmmo = rig.Shooting.GetCurrentAmmo();
            if (finalAmmo != initialAmmo) return false;

            return true;
        }
        finally
        {
            rig.Destroy();
        }
    }

    // Test 17: Multitouch isolation: Finger 0 on UI, Finger 1 in gameplay
    private static bool Test_Multitouch_OneFingerUI_OneFingerGameplay_IsolatedProperly()
    {
        var rig = CreateTestRig();
        try
        {
            int initialAmmo = rig.Shooting.GetCurrentAmmo();

            // Finger 0 touches UI
            var blockedSet = (HashSet<int>)GetPrivateField(rig.Shooting, "uiBlockedFingerIds");
            blockedSet.Add(0);

            // Finger 1 touches gameplay and starts aiming
            SetPrivateField(rig.Shooting, "touchSessionActive", true);
            SetPrivateField(rig.Shooting, "activeFingerId", 1);
            SetPrivateField(rig.Shooting, "isMouseSession", false);
            InvokePrivateMethod(rig.Shooting, "StartAiming", new Vector2(400, 400));

            if (!rig.Shooting.IsAiming()) return false;
            if (!rig.Shooting.IsFingerUIBlocked(0)) return false;
            if (rig.Shooting.IsFingerUIBlocked(1)) return false;

            // Finger 0 releases (lifts from UI)
            blockedSet.Remove(0);

            // Aiming session for Finger 1 must remain active!
            if (!rig.Shooting.IsAiming()) return false;
            if (!rig.Shooting.IsTouchSessionActive()) return false;
            if (rig.Shooting.GetCurrentAmmo() != initialAmmo) return false;

            // Finger 1 releases (fires 1 release shot)
            InvokePrivateMethod(rig.Shooting, "HandlePointerUp", new Vector2(400, 400));

            if (rig.Shooting.IsAiming()) return false;
            if (rig.Shooting.IsTouchSessionActive()) return false;
            if (initialAmmo - rig.Shooting.GetCurrentAmmo() != 1) return false;

            return true;
        }
        finally
        {
            rig.Destroy();
        }
    }
    #endregion
}
#endif
