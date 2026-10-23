using System;
using UnityEngine;
using UnityEngine.InputSystem;
using static Controls;

[CreateAssetMenu(fileName = "NewInputReader", menuName = "Input/InputReader", order = 1)]
public class InputReader : ScriptableObject, IPlayerActions
{
    public event Action<Vector2> moveEvent;
    public event Action<bool> primaryFireEvent;

    /// <summary>Fires once when the melee button is pressed (performed phase only).</summary>
    public event Action meleeEvent;

    /// <summary>Fires once when the dash action is requested (performed phase only).</summary>
    public event Action dashEvent;

    // ─── Mobile Control Events ──────────────────────────────────
    /// <summary>Fires continuously while aiming on mobile with the current 3D world aim direction.</summary>
    public event Action<Vector3> mobileAimDirectionEvent;

    /// <summary>Fires when the aim joystick transitions between armed (true) and disarmed/canceled (false).</summary>
    public event Action<bool> mobileAimStateChangedEvent;

    /// <summary>Fires exactly once on a valid armed release with the final 3D world aim direction.</summary>
    public event Action<Vector3> mobileFireEvent;

    public Vector2 aimPostion { get; private set; }
    public Vector3 MobileAimDirection { get; private set; }
    public bool IsMobileAiming { get; private set; }
    public bool IsMobileArmed { get; private set; }

    private Controls controls;

    private void OnEnable()
    {
        if (controls == null)
        {
            controls = new Controls();
            controls.Player.SetCallbacks(this);
        }
        controls.Enable();
    }

    private void OnDisable()
    {
        controls.Disable();
        ResetMobileInput();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveEvent?.Invoke(context.ReadValue<Vector2>());
    }

    public void OnPrimaryFire(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            primaryFireEvent?.Invoke(true);
        }
        else if (context.canceled)
        {
            primaryFireEvent?.Invoke(false);
        }
    }

    public void OnAim(InputAction.CallbackContext context)
    {
        aimPostion = context.ReadValue<Vector2>();
    }

    /// <summary>
    /// Fires once on button press. No need for a bool — melee is always a single instant action.
    /// </summary>
    public void OnMelee(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            meleeEvent?.Invoke();
        }
    }

    /// <summary>
    /// Fires once on button press (performed phase only) to trigger dash.
    /// </summary>
    public void OnDash(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            dashEvent?.Invoke();
        }
    }

    // ─── Mobile Control Ingestion API ────────────────────────────

    /// <summary>
    /// Feeds movement input from the virtual joystick directly into the existing moveEvent.
    /// </summary>
    public void SetMoveInput(Vector2 moveInput)
    {
        moveEvent?.Invoke(moveInput);
    }

    /// <summary>
    /// Updates the aim screen position (preserves compatibility with systems reading aimPostion).
    /// </summary>
    public void SetAimPosition(Vector2 screenPosition)
    {
        aimPostion = screenPosition;
    }

    /// <summary>
    /// Updates the mobile 3D world aim direction and arming state.
    /// </summary>
    public void SetMobileAim(Vector3 worldDirection, bool isAiming, bool isArmed)
    {
        MobileAimDirection = worldDirection;
        IsMobileAiming = isAiming;

        bool armStateChanged = (IsMobileArmed != isArmed);
        IsMobileArmed = isArmed;

        mobileAimDirectionEvent?.Invoke(worldDirection);

        if (armStateChanged)
        {
            mobileAimStateChangedEvent?.Invoke(isArmed);
        }
    }

    /// <summary>
    /// Requests a single authoritative shot from a valid mobile joystick release.
    /// </summary>
    public void RequestMobileFire(Vector3 worldDirection)
    {
        mobileFireEvent?.Invoke(worldDirection);
    }

    /// <summary>
    /// Triggers one melee action from the mobile melee button.
    /// </summary>
    public void TriggerMelee()
    {
        meleeEvent?.Invoke();
    }

    /// <summary>
    /// Triggers one dash action from the mobile dash button.
    /// </summary>
    public void TriggerDash()
    {
        dashEvent?.Invoke();
    }

    /// <summary>
    /// Resets all mobile inputs safely to prevent stuck walking, aiming, or firing.
    /// </summary>
    public void ResetMobileInput()
    {
        moveEvent?.Invoke(Vector2.zero);
        if (IsMobileAiming || IsMobileArmed)
        {
            IsMobileAiming = false;
            IsMobileArmed = false;
            mobileAimStateChangedEvent?.Invoke(false);
            mobileAimDirectionEvent?.Invoke(Vector3.zero);
        }
    }
}

