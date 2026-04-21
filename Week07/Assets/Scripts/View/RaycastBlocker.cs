using UnityEngine;

/// <summary>
/// MapObjectInputHandler의 레이캐스트를 차단하는 마커 컴포넌트입니다.
/// Collider가 있는 GameObject에 부착하고, 해당 오브젝트의 레이어를
/// MapObjectInputHandler의 MapObjectMask에 포함시키세요.
/// 레이캐스트에 이 컴포넌트가 감지되면 뒤에 있는 ClickScaleBounce는 무시됩니다.
/// </summary>
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class RaycastBlocker : MonoBehaviour { }
