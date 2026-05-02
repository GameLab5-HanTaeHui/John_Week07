using System.Collections;
using UnityEngine;
using HTH.Campaign; // CharacterRecordPanel 스크립트 참조용

namespace HTH.Tutorial
{
    /// <summary>
    /// [튜토리얼 전용] 캐릭터 개별 기록장 패널을 관리합니다.
    /// 본편과 달리 7개가 아닌 '엔비'의 패널 단 하나만 관리하며,
    /// 열고 닫을 때마다 TutorialManager에 이벤트를 보고하여 다음 대화로 넘깁니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialCharacterRecordPanelManager : MonoBehaviour
    {
        public static TutorialCharacterRecordPanelManager Instance { get; private set; }

        [Header("튜토리얼 전용 기록장 패널 (엔비 전용)")]
        [SerializeField] private CharacterRecordPanel _envyPanel;

        [Tooltip("엔비의 캐릭터 ID")]
        [SerializeField] private int _envyCharacterId = 4;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // 튜토리얼 씬 진입 시 패널 내부 슬롯 초기화
            StartCoroutine(InitializeSlotAfterActivation());
        }

        /// <summary>
        /// 토글 버튼 클릭 시 호출됩니다.
        /// 패널을 열거나 닫은 직후, 튜토리얼 매니저에게 다음 페이즈로 넘어가라고 알려줍니다!
        /// </summary>
        public void OpenPanel(int characterId)
        {
            // 튜토리얼은 지정된 캐릭터(엔비) 패널만 조작할 수 있습니다.
            if (characterId != _envyCharacterId || _envyPanel == null) return;

            if (_envyPanel.IsOpen)
            {
                // 열려있으면 닫기
                _envyPanel.Close();
                Debug.Log("[Tutorial] 다이어리 닫힘 -> 다음 페이즈 진행");
                TutorialManager.Instance?.NotifyCharacterCardClosed(); // ➔ Action_CloseEnvyDiary 완료 보고!
            }
            else
            {
                // 닫혀있으면 열기
                _envyPanel.Open(characterId);
                Debug.Log("[Tutorial] 다이어리 열림 -> 다음 페이즈 진행");
                TutorialManager.Instance?.NotifyCharacterCardOpened(); // ➔ Action_OpenEnvyDiary 완료 보고!
            }
        }

        private IEnumerator InitializeSlotAfterActivation()
        {
            yield return null; // Awake 보장

            const float timeout = 3f;
            float elapsed = 0f;

            // 패널이 활성화될 때까지 잠시 대기
            while (!_envyPanel.gameObject.activeInHierarchy && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            _envyPanel.InitializeSlots(_envyCharacterId);
        }
    }
}