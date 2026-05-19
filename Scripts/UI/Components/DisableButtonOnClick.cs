using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class DisableButtonOnClick : MonoBehaviour
{
    private Button _button;
    
    private void Awake()
    {
        _button = GetComponent<Button>();
        
        if (_button != null)
        {
            _button.onClick.AddListener(OnButtonClicked);
        }
    }

    // GameObject 활성화 시 버튼도 다시 활성화
    private void OnEnable()
    {
        if (_button != null)
        {
            _button.interactable = true;
        }
    }

    private void OnButtonClicked()
    {
        if (_button != null)
        {
            _button.interactable = false;
        }
    }
    
    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnButtonClicked);
        }
    }
}