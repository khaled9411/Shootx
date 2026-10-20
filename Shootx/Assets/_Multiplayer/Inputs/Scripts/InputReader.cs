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

    public Vector2 aimPostion { get; private set; }

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
}
