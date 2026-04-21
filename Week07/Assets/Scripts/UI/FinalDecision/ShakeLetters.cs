using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 텍스트를 한 글자씩 분리하여 각 글자에 독립적인 DOTween 흔들기를 적용합니다.
/// HorizontalLayoutGroup으로 레이아웃 확정 후 자동으로 흔들기를 시작합니다.
/// </summary>
[RequireComponent(typeof(HorizontalLayoutGroup))]
public class ShakeLetters : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset _fontAsset;
    [SerializeField] private float         _fontSize      = 36f;
    [SerializeField] private Color         _color         = Color.white;

    [Header("흔들기 설정")]
    [SerializeField] private float _strength  = 2.5f;
    [SerializeField] private float _duration  = 0.45f;
    [SerializeField] private int   _vibrato   = 25;
    [SerializeField] private float _maxDelay  = 0.5f;

    private HorizontalLayoutGroup      _layout;
    private readonly List<RectTransform> _letterRects = new();
    private readonly List<Tween>         _tweens      = new();
    private Coroutine                    _shakeRoutine;

    private void Awake()
    {
        _layout = GetComponent<HorizontalLayoutGroup>();
        _layout.childForceExpandWidth  = false;
        _layout.childForceExpandHeight = false;
        _layout.childAlignment         = TextAnchor.MiddleLeft;
        _layout.spacing                = 0f;

        var csf = GetComponent<ContentSizeFitter>() ?? gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
    }

    // ── Public ───────────────────────────────────────────────────────────────

    public void SetText(string text)
    {
        Clear();
        if (string.IsNullOrEmpty(text)) return;

        foreach (char c in text)
            SpawnLetter(c);

        if (gameObject.activeInHierarchy)
            _shakeRoutine = StartCoroutine(BeginShakeAfterLayout());
    }

    public void CopyStyleFrom(TMP_Text source)
    {
        if (source == null) return;
        _fontAsset = source.font;
        _fontSize  = source.fontSize;
        _color     = source.color;
    }

    public void Clear()
    {
        if (_shakeRoutine != null) { StopCoroutine(_shakeRoutine); _shakeRoutine = null; }
        KillTweens();
        foreach (var rt in _letterRects)
            if (rt != null) Destroy(rt.gameObject);
        _letterRects.Clear();
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void SpawnLetter(char c)
    {
        var go  = new GameObject(c.ToString(), typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = c.ToString();
        tmp.fontSize  = _fontSize;
        tmp.color     = _color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (_fontAsset != null) tmp.font = _fontAsset;

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth  = _fontSize * 0.7f;
        le.preferredHeight = _fontSize * 1.2f;

        _letterRects.Add(go.GetComponent<RectTransform>());
    }

    private IEnumerator BeginShakeAfterLayout()
    {
        // 레이아웃이 확정될 때까지 대기
        yield return null;
        yield return null;

        _layout.enabled = false; // 레이아웃 고정 후 비활성화하여 shake와 충돌 방지

        for (int i = 0; i < _letterRects.Count; i++)
        {
            var rt    = _letterRects[i];
            if (rt == null) continue;

            float delay   = Random.Range(0f, _maxDelay);
            float strVar  = _strength * Random.Range(0.7f, 1.3f);
            int   vibVar  = _vibrato + Random.Range(-5, 5);
            float durVar  = _duration * Random.Range(0.8f, 1.2f);

            var seq = DOTween.Sequence().SetLink(rt.gameObject);
            seq.AppendInterval(delay);
            seq.AppendCallback(() =>
            {
                var t = rt.DOShakeAnchorPos(durVar, strVar, vibVar, 90f, false, false)
                          .SetLoops(-1, LoopType.Restart)
                          .SetLink(rt.gameObject);
                _tweens.Add(t);
            });
            _tweens.Add(seq);
        }
    }

    private void KillTweens()
    {
        foreach (var t in _tweens) t?.Kill();
        _tweens.Clear();
    }

    private void OnDestroy() => KillTweens();
}
