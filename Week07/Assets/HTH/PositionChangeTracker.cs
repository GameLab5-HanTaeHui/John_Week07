// 임시 디버그 컴포넌트 — 버그 확인 후 제거
// CharacterView와 같은 오브젝트에 AddComponent
using UnityEngine;

public class PositionChangeTracker : MonoBehaviour
{
    private Vector3 _lastPosition;

    private void Start() => _lastPosition = transform.position;

    private void LateUpdate()
    {
        if (Vector3.Distance(transform.position, _lastPosition) > 0.01f)
        {
            Debug.LogError($"[위치변경] {name} | {_lastPosition} → {transform.position}\n"
                         + StackTraceUtility.ExtractStackTrace());
            _lastPosition = transform.position;
        }
    }
}