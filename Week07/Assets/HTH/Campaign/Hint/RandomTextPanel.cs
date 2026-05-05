using UnityEngine;
using TMPro; // TextMeshPro를 사용하기 위해 필수
using System.Collections.Generic; // List를 사용하기 위해 필수

public class RandomTextPanel : MonoBehaviour //[cite: 1]
{
    // 1. 싱글톤 인스턴스 선언
    public static RandomTextPanel Instance { get; private set; }

    [Header("UI Reference")]
    public TextMeshProUGUI targetTMP; // 텍스트를 띄울 TMP 컴포넌트

    [Header("Text Data")]
    public List<string> randomStrings; // 랜덤으로 뽑을 문자열들을 담을 리스트

    private void Awake()
    {
        // 2. 싱글톤 패턴 초기화
        if (Instance == null)
        {
            Instance = this;
            // 씬 전환 시에도 파괴되지 않게 하려면 아래 주석을 해제하세요.
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    //[cite: 1] 기존 Start, Update는 필요하지 않다면 삭제하셔도 무방합니다.

    // 3. 랜덤 텍스트 출력 함수
    public void DisplayRandomText()
    {
        // 예외 처리: 리스트가 비어있거나 TMP가 할당되지 않았을 때 방어
        if (randomStrings == null || randomStrings.Count == 0)
        {
            Debug.LogWarning("출력할 문자열 리스트가 비어있습니다!");
            return;
        }

        if (targetTMP == null)
        {
            Debug.LogWarning("TextMeshPro 컴포넌트가 할당되지 않았습니다!");
            return;
        }

        // 0부터 리스트의 개수-1 까지의 수 중 하나를 랜덤으로 뽑음
        int randomIndex = Random.Range(0, randomStrings.Count);

        // 뽑은 문자열을 TMP에 적용
        targetTMP.text = randomStrings[randomIndex];
    }
}