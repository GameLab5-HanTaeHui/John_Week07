using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 캐릭터 뷰에 보드 게임 말 느낌의 집기/드래그/착지 애니메이션을 부여합니다.
/// CharacterView와 같은 GameObject에 추가하세요.
///
/// 외부 호출 순서:
///   1. PickUp(zoneRotation)     — 드래그 시작 시
///   2. SetDragVelocity(vel)     — 드래그 중 매 프레임
///   3. LandAt(pos, rot)         — 목표 슬롯 착지
///   3. (또는) LandAt(origPos, origRot) — 취소 시 원위치 복귀
///
/// 다른 캐릭터 슬롯 재배치용:
///   ReplaceTo(pos, rot)
/// </summary>
[RequireComponent(typeof(CharacterView))]
public class CharacterPickupAnimator : MonoBehaviour
{
    [Header("Pick Up")]
    [SerializeField] private float   _liftHeight     = 1.5f;
    [SerializeField] private float   _liftDuration   = 0.18f;
    [SerializeField] private float   _liftScaleMulti = 1.2f;
    [Tooltip("잡을 때 커서 기준으로 말이 위치할 오프셋 (로컬 좌표).\nX/Z: 커서 좌우·앞뒤 어느 지점을 잡는지\nY: 커서 기준 높이 (양수 = 커서 아래에 말이 위치)")]
    [SerializeField] private Vector3 _pickupOffset  = Vector3.zero;

    [Header("Drag Tilt")]
    [SerializeField] private float _maxTiltAngle      = 22f;
    [SerializeField] private float _tiltSmoothing     = 10f;
    [SerializeField] [Range(0.01f, 2f)] private float _velocityTiltScale = 0.35f;

    [Header("Landing")]
    [SerializeField] private float _preAlignDuration = 0.12f;
    [SerializeField] private float _dropDuration     = 0.22f;
    [SerializeField] private float _impactTiltAngle  = 18f;
    [SerializeField] private float _impactDuration   = 0.09f;
    [SerializeField] private float _settleUpHeight   = 0.28f;
    [SerializeField] private float _settleDuration   = 0.14f;

    [Header("착지 이펙트")]
    [SerializeField] private GameObject _landEffect;
    [SerializeField] private float      _landEffectDuration = 0.5f;

    public float   LiftHeight    => _liftHeight;
    public Vector3 PickupOffset  => _pickupOffset;
    public bool    IsHeld        => _isHeld;

    private Vector3    _baseScale;
    private bool       _isHeld;
    private Quaternion _zoneRotation;
    private Vector3    _tiltTarget;
    private Vector3    _tiltCurrent;

    private void Awake()
    {
        _baseScale = transform.localScale;
    }

    private void Update()
    {
        if (!_isHeld) return;

        _tiltCurrent = Vector3.Lerp(_tiltCurrent, _tiltTarget, Time.deltaTime * _tiltSmoothing);
        transform.rotation = _zoneRotation * Quaternion.Euler(_tiltCurrent);
    }

    // ── 공개 API ──────────────────────────────────────────────────────────────

    /// <summary>드래그 시작 시 호출. 들어올리기 + 스케일 업.</summary>
    public void PickUp(Quaternion zoneRotation)
    {
        DOTween.Kill(transform);
        _zoneRotation = zoneRotation;
        _isHeld       = true;
        _tiltCurrent  = Vector3.zero;
        _tiltTarget   = Vector3.zero;

        DOTween.Sequence()
            .Join(transform.DOMoveY(transform.position.y + _liftHeight + _pickupOffset.y, _liftDuration).SetEase(Ease.OutBack))
            .Join(transform.DOScale(_baseScale * _liftScaleMulti, _liftDuration).SetEase(Ease.OutBack))
            .SetId(transform);
    }

    /// <summary>드래그 중 매 프레임 호출. 이동 속도 기반으로 기울기를 업데이트합니다.</summary>
    public void SetDragVelocity(Vector3 worldVelocity)
    {
        if (!_isHeld) return;

        // 이동 방향 반대로 기울어져 드래그 저항감 표현
        var clamped = Vector3.ClampMagnitude(worldVelocity, 30f);
        float tiltX = Mathf.Clamp(-clamped.z * _velocityTiltScale, -_maxTiltAngle, _maxTiltAngle);
        float tiltZ = Mathf.Clamp(-clamped.x * _velocityTiltScale, -_maxTiltAngle, _maxTiltAngle);
        _tiltTarget = new Vector3(tiltX, 0f, tiltZ);
    }

    /// <summary>
    /// 목표 슬롯으로 착지 애니메이션을 재생합니다.
    /// 순서: XZ 정렬(자리 찾기) → 수직 낙하 → 충격 기울기+살짝 튀어오름 → 안착
    /// </summary>
    public void LandAt(Vector3 targetPos, Quaternion targetRot, Action onComplete = null)
    {
        _isHeld     = false;
        _tiltTarget = Vector3.zero;
        DOTween.Kill(transform);

        var aboveTarget = new Vector3(targetPos.x, transform.position.y, targetPos.z);
        var impactRot   = targetRot * Quaternion.Euler(_impactTiltAngle, 0f, 0f);

        DOTween.Sequence()
            // 1. 수직 낙하 전 XZ 정렬 — "자리 찾기"
            .Append(transform.DOMove(aboveTarget, _preAlignDuration).SetEase(Ease.OutQuad))
            // 2. 수직 낙하 + 스케일 복원
            .Append(transform.DOMove(targetPos, _dropDuration).SetEase(Ease.InQuad))
            .Join(transform.DOScale(_baseScale, _dropDuration).SetEase(Ease.OutQuad))
            // 3. 착지 충격 — 뒤로 기울기 + 살짝 튀어오름
            .Append(transform.DORotateQuaternion(impactRot, _impactDuration).SetEase(Ease.OutQuad))
            .Join(transform.DOMoveY(targetPos.y + _settleUpHeight, _impactDuration).SetEase(Ease.OutQuad))
            // 4. 제자리 안착 — 기울기 복원 + 최종 위치
            .Append(transform.DOMoveY(targetPos.y, _settleDuration).SetEase(Ease.InQuad))
            .Join(transform.DORotateQuaternion(targetRot, _settleDuration).SetEase(Ease.OutQuad))
            .OnComplete(() =>
            {
                _tiltCurrent = Vector3.zero;
                onComplete?.Invoke();
                PlayLandEffect();
            })
            .SetId(transform);
    }

    private void PlayLandEffect()
    {
        if (_landEffect == null) return;
        DOTween.Kill(_landEffect);
        _landEffect.SetActive(true);
        DOVirtual.DelayedCall(_landEffectDuration, () =>
        {
            if (_landEffect != null)
                _landEffect.SetActive(false);
        }).SetId(_landEffect);
    }

    /// <summary>
    /// 드래그하지 않은 캐릭터의 슬롯 재배치용 간단 이동 애니메이션.
    /// </summary>
    public void ReplaceTo(Vector3 targetPos, Quaternion targetRot)
    {
        DOTween.Kill(transform);
        DOTween.Sequence()
            .Append(transform.DOMove(targetPos, 0.25f).SetEase(Ease.OutQuad))
            .Join(transform.DORotateQuaternion(targetRot, 0.25f).SetEase(Ease.OutQuad))
            .Join(transform.DOScale(_baseScale, 0.12f).SetEase(Ease.OutQuad))
            .SetId(transform);
    }
}
