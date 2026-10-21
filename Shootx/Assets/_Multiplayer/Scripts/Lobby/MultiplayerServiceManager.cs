using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// Centralized initialization for Unity Gaming Services and Authentication.
/// Ensures services are initialized only once and handles concurrent requests safely.
/// This is NOT a MonoBehaviour — call its static methods from any lobby script.
/// </summary>
public static class MultiplayerServiceManager
{
    private static bool _isInitializing;
    private static bool _isInitialized;
    private static TaskCompletionSource<bool> _initTcs;

    /// <summary>
    /// Initialize Unity Services and sign in anonymously if needed.
    /// Safe to call multiple times — only the first call does real work.
    /// </summary>
    public static async Task<bool> EnsureInitializedAsync()
    {
        // Already done
        if (_isInitialized && AuthenticationService.Instance.IsSignedIn)
            return true;

        // Another call is already in progress — wait for it
        if (_isInitializing && _initTcs != null)
            return await _initTcs.Task;

        _isInitializing = true;
        _initTcs = new TaskCompletionSource<bool>();

        try
        {
            // Initialize UGS Core
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                Debug.Log("[MPServices] Initializing Unity Services...");
                await UnityServices.InitializeAsync();
                Debug.Log("[MPServices] Unity Services initialized.");
            }

            // Sign in anonymously
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("[MPServices] Signing in anonymously...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[MPServices] Signed in. PlayerId: {AuthenticationService.Instance.PlayerId}");
            }

            _isInitialized = true;
            _initTcs.TrySetResult(true);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MPServices] Initialization failed: {ex.Message}");
            _initTcs.TrySetResult(false);
            return false;
        }
        finally
        {
            _isInitializing = false;
        }
    }

    /// <summary>
    /// Returns the current Authentication player ID, or empty if not signed in.
    /// </summary>
    public static string GetPlayerId()
    {
        try
        {
            if (AuthenticationService.Instance.IsSignedIn)
                return AuthenticationService.Instance.PlayerId;
        }
        catch { }
        return string.Empty;
    }
}
