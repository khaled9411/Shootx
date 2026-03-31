using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class DirectionButtonsUI : MonoBehaviour
{
    public static DirectionButtonsUI Instance;

    [SerializeField] private Button btnUp;
    [SerializeField] private Button btnDown;
    [SerializeField] private Button btnLeft;
    [SerializeField] private Button btnRight;


    private MovementPoint currentPoint;

    void Awake()
    {
        Instance = this;

        btnUp.onClick.AddListener(() => MoveToNeighbor(currentPoint?.neighborUp));
        btnDown.onClick.AddListener(() => MoveToNeighbor(currentPoint?.neighborDown));
        btnLeft.onClick.AddListener(() => MoveToNeighbor(currentPoint?.neighborLeft));
        btnRight.onClick.AddListener(() => MoveToNeighbor(currentPoint?.neighborRight));

        HideAll();
    }

    public void ShowButtons(MovementPoint point)
    {
        currentPoint = point;

        SetButton(btnUp, point.neighborUp != null);
        SetButton(btnDown, point.neighborDown != null);
        SetButton(btnLeft, point.neighborLeft != null);
        SetButton(btnRight, point.neighborRight != null);
    }

    void SetButton(Button btn, bool show)
    {
        btn.gameObject.SetActive(show);

        if (show)
        {
            btn.transform.localScale = Vector3.zero;
            btn.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack);
        }
    }

    void MoveToNeighbor(MovementPoint neighbor)
    {
        if (neighbor == null) return;

        HideAll();

        PathMovementManager manager = FindFirstObjectByType<PathMovementManager>();
        if (manager != null)
            manager.MoveToPoint(neighbor.PointIndex);
    }

    public void HideAll()
    {
        btnUp.gameObject.SetActive(false);
        btnDown.gameObject.SetActive(false);
        btnLeft.gameObject.SetActive(false);
        btnRight.gameObject.SetActive(false);
    }
}