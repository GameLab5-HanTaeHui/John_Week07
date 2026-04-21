using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 캐릭터 프리팹에 부착되는 뷰 컴포넌트입니다.
/// 클릭 감지, 선택 하이라이트, 구역 위치 이동을 담당합니다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CharacterView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private CharacterData _data;

    [Header("Visual")]
    [SerializeField] private GameObject _selectionIndicator;

    [Header("모델")]
    [SerializeField] private GameObject _aliveModel;
    [SerializeField] private GameObject _deadModel;

    /// <summary>캐릭터를 클릭했을 때 발생합니다.</summary>
    public event Action<CharacterView> OnClicked;

    private CharacterState _state;

    public CharacterData Data        => _data;
    public int           CharacterId => _data != null ? _data.CharacterId : -1;

    // ── 초기화 ──────────────────────────────────────────────────────────────

    /// <summary>CharacterSpawner에서 스폰 직후 호출합니다.</summary>
    public void Init(CharacterState state)
    {
        _state = state;
        _data  = state.Data;
    }

    // ── 시각 갱신 ────────────────────────────────────────────────────────────

    /// <summary>현재 CharacterState의 생사 여부에 따라 모델을 교체합니다.</summary>
    public void RefreshView()
    {
        bool alive = _state == null || _state.IsAlive;
        if (_aliveModel != null) _aliveModel.SetActive(alive);
        if (_deadModel  != null) _deadModel.SetActive(!alive);
    }

    /// <summary>선택/비선택 하이라이트를 설정합니다.</summary>
    public void SetSelected(bool selected)
    {
        if (_selectionIndicator != null)
            _selectionIndicator.SetActive(selected);
    }

    /// <summary>캐릭터를 목표 월드 좌표로 즉시 이동합니다.</summary>
    public void SnapToPosition(Vector3 worldPosition)
    {
        transform.position = worldPosition;
    }

    /// <summary>캐릭터를 목표 월드 회전으로 즉시 회전합니다.</summary>
    public void SnapToRotation(Quaternion worldRotation)
    {
        transform.rotation = worldRotation;
    }

    // ── IPointerClickHandler ─────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"[CharacterView] OnPointerClick 호출됨 — {(_data != null ? _data.CharacterName : "null")} (ID:{CharacterId})");
        OnClicked?.Invoke(this);
    }
}
