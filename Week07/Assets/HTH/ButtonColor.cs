using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HTH
{
    public class ButtonColor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("버튼의 자식 Text를 기입해 주세요")]
        [SerializeField] private TextMeshProUGUI _buttonText;
        [Header("기본 색상")]
        [SerializeField] private Color _originColor = Color.white;
        [Header("바꿀 색상")]
        [SerializeField] private Color _ChangeColor = Color.green;
        public Color ButtonChangeColor => _ChangeColor;
        public TextMeshProUGUI ButtonText => _buttonText;

        private Color _orignalColor;
        private void OnEnable()
        {
            _buttonText.color = _originColor;
        }
        void Awake()
        {
            _orignalColor = _originColor;
            if (_buttonText == null) _buttonText = GetComponentInChildren<TextMeshProUGUI>();
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            _buttonText.color = _ChangeColor;
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            _buttonText.color = _orignalColor;
        }
    }
}
