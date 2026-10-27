using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Shootx.Store;

namespace Shootx.Store.Editor
{
    public static class ShopAndMultiplayerAccessUIEditor
    {
        [MenuItem("Tools/Shootx UI/Validate Redesigned UI", false, 2)]
        public static void ValidateUI()
        {
            var shop = UnityEngine.Object.FindFirstObjectByType<ShopPopupController>();
            if (shop != null)
            {
                Debug.Log("[ShootX UI Validation] SHOP POPUP detected and active.");
            }
            else
            {
                Debug.LogWarning("[ShootX UI Validation] SHOP POPUP not found in active scene.");
            }

            var modal = UnityEngine.Object.FindFirstObjectByType<MultiplayerAccessGateModal>();
            if (modal != null)
            {
                Debug.Log("[ShootX UI Validation] MULTIPLAYER ACCESS MODAL detected and active.");
            }
            else
            {
                Debug.LogWarning("[ShootX UI Validation] MULTIPLAYER ACCESS MODAL not found in active scene.");
            }
        }
    }
}
