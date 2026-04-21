using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// TMP_Text 인스턴스를 재활용하는 오브젝트 풀입니다.
/// DialogueManager가 초기화 시 생성하며, Get/Return으로 인스턴스를 대여·반납합니다.
/// </summary>
public class TextPool
{
    private readonly Queue<TMP_Text> _available = new Queue<TMP_Text>();
    private readonly TMP_Text        _prefab;
    private readonly Transform       _root;

    public TextPool(TMP_Text prefab, Transform root, int initialSize = 4)
    {
        _prefab = prefab;
        _root   = root;
        for (int i = 0; i < initialSize; i++)
            _available.Enqueue(CreateInstance());
    }

    /// <summary>풀에서 TMP_Text를 꺼내 활성화합니다. 풀이 비었으면 새로 생성합니다.</summary>
    public TMP_Text Get()
    {
        var text = _available.Count > 0 ? _available.Dequeue() : CreateInstance();
        text.gameObject.SetActive(true);
        return text;
    }

    /// <summary>사용이 끝난 TMP_Text를 초기화하고 풀에 반환합니다.</summary>
    public void Return(TMP_Text text)
    {
        text.text = "";
        text.gameObject.SetActive(false);
        _available.Enqueue(text);
    }

    private TMP_Text CreateInstance()
    {
        var instance = Object.Instantiate(_prefab, _root);
        instance.gameObject.SetActive(false);
        return instance;
    }
}
