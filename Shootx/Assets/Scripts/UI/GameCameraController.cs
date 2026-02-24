using UnityEngine;
using DG.Tweening;


public class GameCameraController : MonoBehaviour
{
    // ===================================================================
    #region Inspector Fields
    // ===================================================================

    [Header("=== Cameras ===")]
    [SerializeField] private Camera mainCamera;

    [Header("=== Menu Camera Position ===")]
    [SerializeField] private Transform menuCameraAnchor;

    [Header("=== Gameplay Camera Position ===")]
    [SerializeField] private Transform gameCameraAnchor;

    [Header("=== Transition ===")]
    [SerializeField] private float transitionDuration = 1.2f;
    [SerializeField] private Ease transitionEase = Ease.InOutCubic;

    [Header("=== Menu Idle Bob ===")]
    [SerializeField] private bool enableMenuBob = true;
    [SerializeField] private float bobAmplitude = 0.15f;
    [SerializeField] private float bobDuration = 3.0f;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Private State
    // ===================================================================

    private Vector3 menuPos;
    private Quaternion menuRot;
    private Tween bobTween;
    private bool transitioned = false;

    // ===================================================================
    #endregion

    // ===================================================================
    #region Unity Lifecycle
    // ===================================================================

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        if (menuCameraAnchor != null)
        {
            mainCamera.transform.position = menuCameraAnchor.position;
            mainCamera.transform.rotation = menuCameraAnchor.rotation;
        }

        menuPos = mainCamera.transform.position;
        menuRot = mainCamera.transform.rotation;

        if (enableMenuBob) StartMenuBob();
    }

    private void OnEnable()
    {
        UIManager.OnTapToPlay += TransitionToGameCamera;
    }

    private void OnDisable()
    {
        UIManager.OnTapToPlay -= TransitionToGameCamera;
        bobTween?.Kill();
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Menu Idle Bob
    // ===================================================================

    private void StartMenuBob()
    {
        bobTween?.Kill();

        bobTween = mainCamera.transform
            .DOMove(menuPos + Vector3.up * bobAmplitude, bobDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    // ===================================================================
    #endregion

    // ===================================================================
    #region Game Transition
    // ===================================================================

    private void TransitionToGameCamera()
    {
        if (transitioned) return;
        transitioned = true;

        bobTween?.Kill();

        if (gameCameraAnchor == null)
        {
            Debug.LogWarning("[GameCamera] gameCameraAnchor is not assigned!");
            return;
        }

        Sequence seq = DOTween.Sequence();

        seq.Append(mainCamera.transform
            .DOMove(gameCameraAnchor.position, transitionDuration)
            .SetEase(transitionEase));

        seq.Join(mainCamera.transform
            .DORotateQuaternion(gameCameraAnchor.rotation, transitionDuration)
            .SetEase(transitionEase));

        seq.Play();
    }

    public void ReturnToMenuCamera(System.Action onComplete = null)
    {
        transitioned = false;
        DOTween.Kill(mainCamera.transform);

        Sequence seq = DOTween.Sequence();
        seq.Append(mainCamera.transform.DOMove(menuPos, transitionDuration * 0.8f).SetEase(transitionEase));
        seq.Join(mainCamera.transform.DORotateQuaternion(menuRot, transitionDuration * 0.8f).SetEase(transitionEase));
        seq.OnComplete(() =>
        {
            if (enableMenuBob) StartMenuBob();
            onComplete?.Invoke();
        });
        seq.Play();
    }

    // ===================================================================
    #endregion
}