using UnityEngine;

/// <summary>
/// MapObjectInputHandler가 이 오브젝트 위에 마우스를 올리면
/// _hoverDelay초 후 _target을 활성화하고, 벗어나면 즉시 비활성화합니다.
/// FinalDecision 진입 시에도 강제로 비활성화합니다.
/// </summary>
[DisallowMultipleComponent]
public class MapObjectHoverActivator : MonoBehaviour
{
    [SerializeField] private GameObject _target;
    [SerializeField] private float      _hoverDelay = 1f;

    private float _hoverTimer;
    private bool  _isHovering;

    private void Start()
    {
        var gfc = GameFlowController.Instance;
        if (gfc != null)
            gfc.OnFinalDecisionEntered += OnHoverExit;
    }

    private void OnDestroy()
    {
        var gfc = GameFlowController.Instance;
        if (gfc != null)
            gfc.OnFinalDecisionEntered -= OnHoverExit;
    }

    private void Update()
    {
        if (!_isHovering) return;

        _hoverTimer += Time.deltaTime;
        if (_hoverTimer >= _hoverDelay && _target != null)
            _target.SetActive(true);
    }

    public void OnHoverEnter()
    {
        _isHovering = true;
        _hoverTimer = 0f;
    }

    public void OnHoverExit()
    {
        _isHovering = false;
        _hoverTimer = 0f;
        if (_target != null) _target.SetActive(false);
    }
}
