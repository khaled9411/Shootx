using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Local-only animated laser visual. Spawned ONLY on the local client that fired.
/// Never replicated or networked as a shared GameObject across other clients.
/// Animates the beam along the calculated segmented trajectory (including ricochets),
/// pauses briefly, fades out, and cleanly destroys itself with safe DOTween cleanup.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class LaserBeamVisual : MonoBehaviour
{
    [Header("Visual Configuration")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Color beamColor = new Color(0f, 0.9f, 1f, 1f);
    [SerializeField] private float beamWidth = 0.08f;
    [SerializeField] private float travelSpeed = 80f; // units per second
    [SerializeField] private float beamLifetime = 0.08f; // time to hold after full extension
    [SerializeField] private float fadeDuration = 0.15f;
    [SerializeField] private GameObject impactEffectPrefab;

    private Sequence _animSeq;
    private List<Vector3> _points = new List<Vector3>();
    private Material _lineMaterialInstance;

    private void Awake()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer != null)
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = beamWidth;
            lineRenderer.endWidth = beamWidth;
            lineRenderer.startColor = beamColor;
            lineRenderer.endColor = beamColor;
            
            // Create a local material instance so fade tween doesn't affect shared project assets
            if (lineRenderer.material != null)
            {
                _lineMaterialInstance = new Material(lineRenderer.material);
                lineRenderer.material = _lineMaterialInstance;
            }
        }
    }

    /// <summary>
    /// Starts the laser animation along the full trajectory points.
    /// </summary>
    public void AnimatePath(IReadOnlyList<Vector3> trajectoryPoints)
    {
        if (trajectoryPoints == null || trajectoryPoints.Count < 2 || lineRenderer == null)
        {
            Destroy(gameObject);
            return;
        }

        _points.Clear();
        for (int i = 0; i < trajectoryPoints.Count; i++)
        {
            _points.Add(trajectoryPoints[i]);
        }

        // Calculate total distance to configure travel timing
        float totalDist = 0f;
        for (int i = 0; i < _points.Count - 1; i++)
        {
            totalDist += Vector3.Distance(_points[i], _points[i + 1]);
        }

        float travelTime = Mathf.Max(0.04f, totalDist / Mathf.Max(1f, travelSpeed));

        // Start with line collapsed at the muzzle
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, _points[0]);
        lineRenderer.SetPosition(1, _points[0]);

        KillActiveTween();
        _animSeq = DOTween.Sequence();

        // 1. Advance the beam head along the segmented path
        _animSeq.Append(DOVirtual.Float(0f, 1f, travelTime, progress =>
        {
            if (this == null || lineRenderer == null) return;
            EvaluateBeamProgress(progress);
        }).SetEase(Ease.Linear));

        // 2. Play impact effect at the final point when head arrives
        _animSeq.AppendCallback(() =>
        {
            if (impactEffectPrefab != null && _points.Count > 0)
            {
                Vector3 endPt = _points[_points.Count - 1];
                GameObject vfx = Instantiate(impactEffectPrefab, endPt, Quaternion.identity);
                Destroy(vfx, 1.5f);
            }
        });

        // 3. Hold beam
        _animSeq.AppendInterval(beamLifetime);

        // 4. Fade out beam width & color
        _animSeq.Append(DOVirtual.Float(1f, 0f, fadeDuration, alpha =>
        {
            if (this == null || lineRenderer == null) return;
            Color c = beamColor;
            c.a *= alpha;
            lineRenderer.startColor = c;
            lineRenderer.endColor = c;
            lineRenderer.startWidth = beamWidth * alpha;
            lineRenderer.endWidth = beamWidth * alpha;
        }).SetEase(Ease.InQuad));

        // 5. Cleanup
        _animSeq.OnComplete(() =>
        {
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        });
    }

    private void EvaluateBeamProgress(float progress)
    {
        if (_points.Count < 2) return;

        float totalDist = 0f;
        float[] segLengths = new float[_points.Count - 1];
        for (int i = 0; i < _points.Count - 1; i++)
        {
            segLengths[i] = Vector3.Distance(_points[i], _points[i + 1]);
            totalDist += segLengths[i];
        }

        float targetDist = progress * totalDist;
        float accumulated = 0f;

        List<Vector3> activePts = new List<Vector3> { _points[0] };

        for (int i = 0; i < _points.Count - 1; i++)
        {
            float segLen = segLengths[i];
            if (accumulated + segLen >= targetDist || Mathf.Approximately(segLen, 0f))
            {
                float remain = targetDist - accumulated;
                float segT = segLen > 0f ? Mathf.Clamp01(remain / segLen) : 1f;
                Vector3 currentHead = Vector3.Lerp(_points[i], _points[i + 1], segT);
                activePts.Add(currentHead);
                break;
            }
            else
            {
                accumulated += segLen;
                activePts.Add(_points[i + 1]);
            }
        }

        lineRenderer.positionCount = activePts.Count;
        for (int k = 0; k < activePts.Count; k++)
        {
            lineRenderer.SetPosition(k, activePts[k]);
        }
    }

    private void KillActiveTween()
    {
        if (_animSeq != null && _animSeq.IsActive())
        {
            _animSeq.Kill();
            _animSeq = null;
        }
    }

    private void OnDisable()
    {
        KillActiveTween();
    }

    private void OnDestroy()
    {
        KillActiveTween();
        if (_lineMaterialInstance != null)
        {
            Destroy(_lineMaterialInstance);
            _lineMaterialInstance = null;
        }
    }
}
