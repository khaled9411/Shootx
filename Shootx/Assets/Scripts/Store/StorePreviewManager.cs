using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shootx.Store
{
    public class StorePreviewManager : MonoBehaviour
    {
        private static StorePreviewManager _instance;
        public static StorePreviewManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<StorePreviewManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("StorePreviewManager");
                        _instance = go.AddComponent<StorePreviewManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("=== Preview Stage Setup ===")]
        private readonly Vector3 _stageOrigin = new Vector3(0f, -500f, 0f);

        private GameObject _stageRoot;
        private Camera _previewCamera;
        private Light _previewLight;
        private GameObject _previewCharacter;
        private PlayerEquipmentApplier _previewApplier;
        private RenderTexture _renderTexture;

        private float _currentRotationY = 180f;
        private Tweener _spinTween;

        public bool IsReady => _previewCharacter != null && _previewCamera != null;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeStage();
        }

        private void Start()
        {

            SceneManager.activeSceneChanged += OnactiveSceneChanged;
        }

        private void OnactiveSceneChanged(Scene preScene, Scene CurrentScene)
        {
            if (CurrentScene.buildIndex == 2)
            {
                Destroy(gameObject);
            }
        }
        private void InitializeStage()
        {
            if (_stageRoot != null) return;

            // Load RenderTexture
            _renderTexture = Resources.Load<RenderTexture>("PlayerPreview");

            // Create Stage Root far away from gameplay
            _stageRoot = new GameObject("StorePreviewStage");
            _stageRoot.transform.position = _stageOrigin;
            DontDestroyOnLoad(_stageRoot);

            // Create Dedicated Preview Camera
            var camGo = new GameObject("PreviewCamera", typeof(Camera));
            camGo.transform.SetParent(_stageRoot.transform, false);
            _previewCamera = camGo.GetComponent<Camera>();
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 1f);
            _previewCamera.orthographic = false;
            _previewCamera.fieldOfView = 36f;
            _previewCamera.nearClipPlane = 0.1f;
            _previewCamera.farClipPlane = 50f;
            _previewCamera.targetTexture = _renderTexture;
            _previewCamera.transform.localPosition = new Vector3(0f, 1.15f, 2.7f);
            _previewCamera.transform.localRotation = Quaternion.Euler(6f, 180f, 0f);

            // Create Preview Light
            //var lightGo = new GameObject("PreviewLight", typeof(Light));
            //lightGo.transform.SetParent(_stageRoot.transform, false);
            //_previewLight = lightGo.GetComponent<Light>();
            //_previewLight.type = LightType.Directional;
            //_previewLight.color = new Color(1f, 0.98f, 0.92f);
            //_previewLight.intensity = 1.3f;
            //lightGo.transform.localPosition = new Vector3(1.5f, 3f, 2f);
            //lightGo.transform.LookAt(_stageRoot.transform.position + Vector3.up * 1f);

            // Instantiate Preview Character from Resources
            GameObject prefab = Resources.Load<GameObject>("Prefabs/Player");
            if (prefab != null)
            {
                _previewCharacter = Instantiate(prefab, _stageRoot.transform);
                _previewCharacter.transform.localPosition = Vector3.zero;
                _previewCharacter.transform.localRotation = Quaternion.Euler(0f, _currentRotationY, 0f);

                // Strip gameplay/physics components to keep it strictly visual
                var shooting = _previewCharacter.GetComponent<ShootingSystem>();
                if (shooting != null) Destroy(shooting);

                var rb = _previewCharacter.GetComponent<Rigidbody>();
                if (rb != null) Destroy(rb);

                var colliders = _previewCharacter.GetComponentsInChildren<Collider>();
                foreach (var col in colliders) Destroy(col);

                var audio = _previewCharacter.GetComponent<AudioSource>();
                if (audio != null) Destroy(audio);

                // Ensure PlayerEquipmentApplier is present
                _previewApplier = _previewCharacter.GetComponent<PlayerEquipmentApplier>();
                if (_previewApplier == null)
                    _previewApplier = _previewCharacter.AddComponent<PlayerEquipmentApplier>();

                // Set Idle animation
                var anim = _previewCharacter.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    anim.Play("Idle");
                }

                // Initial equipment from save
                if (GameDataManager.Instance != null)
                {
                    _previewApplier.ApplyOutfit(GameDataManager.Instance.EquippedOutfitId);
                    _previewApplier.ApplyWeapon(GameDataManager.Instance.EquippedGunId);
                }
            }
            else
            {
                Debug.LogWarning("[StorePreviewManager] Player prefab not found in Resources/Prefabs/Player");
            }

            // Initially pause camera until shop panel opens
            SetCameraActive(false);
        }

        public void SetCameraActive(bool active)
        {
            if (_previewCamera != null) _previewCamera.enabled = active;
            if (_previewLight != null) _previewLight.enabled = active;
        }

        public void ShowOutfitPreview(int outfitId)
        {
            SetCameraActive(true);
            _spinTween?.Kill();

            if (_previewCamera != null)
            {
                _previewCamera.transform.DOLocalMove(new Vector3(0f, 1.15f, 2.75f), 0.3f).SetEase(Ease.OutCubic);
                _previewCamera.transform.DOLocalRotate(new Vector3(6f, 180f, 0f), 0.3f);
            }

            if (_previewApplier != null)
            {
                _previewApplier.ApplyOutfit(outfitId);
            }

            ResetRotation();
        }

        public void ShowWeaponPreview(int gunId)
        {
            SetCameraActive(true);
            _spinTween?.Kill();

            if (_previewCamera != null)
            {
                // Zoom slightly closer and angle towards right hand weapon
                _previewCamera.transform.DOLocalMove(new Vector3(-0.15f, 1.1f, 2.2f), 0.3f).SetEase(Ease.OutCubic);
                _previewCamera.transform.DOLocalRotate(new Vector3(4f, 175f, 0f), 0.3f);
            }

            if (_previewApplier != null)
            {
                _previewApplier.ApplyWeapon(gunId);
            }

            ResetRotation();
        }

        public void RotateCharacter(float deltaX)
        {
            if (_previewCharacter == null) return;
            _spinTween?.Kill();
            _currentRotationY -= deltaX * 0.45f;
            _previewCharacter.transform.localRotation = Quaternion.Euler(0f, _currentRotationY, 0f);
        }

        public void ResetRotation()
        {
            if (_previewCharacter == null) return;
            _spinTween?.Kill();
            _currentRotationY = 180f;
            _spinTween = _previewCharacter.transform.DOLocalRotate(new Vector3(0f, 180f, 0f), 0.4f).SetEase(Ease.OutCubic);
        }

        public void ClosePreview()
        {
            _spinTween?.Kill();
            SetCameraActive(false);

            // Restore preview character to player's actual saved loadout
            if (_previewApplier != null)
            {
                _previewApplier.ApplyCurrentLoadout();
            }
        }
    }
}
