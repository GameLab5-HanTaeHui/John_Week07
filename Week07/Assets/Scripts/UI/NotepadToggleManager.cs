using System;
using UnityEngine;

/// <summary>
/// 메모장 토글 스위치 버튼들을 총괄 관리하는 컴포넌트입니다.
///
/// Inspector 설정:
///   _toggles 배열에 씬의 SequentialImageToggle 컴포넌트를 순서대로 연결하세요.
/// </summary>
public class NotepadToggleManager : MonoBehaviour
{
    [SerializeField] private SequentialImageToggle[] _toggles;

    /// <summary>토글 중 하나라도 변경되면 발생합니다. TutorialManager에서 구독합니다.</summary>
    public event Action OnAnyToggleChanged;

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Start()
    {
        RestoreStates();

        for (int i = 0; i < _toggles.Length; i++)
        {
            if (_toggles[i] == null) continue;
            _toggles[i].OnIndexChanged += _ => HandleToggleChanged();
        }
    }

    private void OnDestroy()
    {
        foreach (var toggle in _toggles)
        {
            if (toggle == null) continue;
            toggle.OnIndexChanged -= _ => HandleToggleChanged();
        }
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void RestoreStates()
    {
        for (int i = 0; i < _toggles.Length; i++)
        {
            if (_toggles[i] == null) continue;
            _toggles[i].SetIndex(0);
        }
    }

    private void HandleToggleChanged()
    {
        OnAnyToggleChanged?.Invoke();
    }
}
