using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>버튼 가이드 패널과 메모리 이미지 등 연출용 UI 요소를 관리하는 싱글턴 매니저</summary>
public class GuideUIManager : MonoBehaviour
{
    public static GuideUIManager Instance { get; private set; }

    [Header("연출용 UI 관리")]
    [SerializeField] private List<GameObject> memoryImages;

    [Header("우측 하단 버튼 이미지 관리")] [SerializeField]
    private GameObject heavyAnchorImg;
    [SerializeField]
    private GameObject lightAnchorImg;
    [SerializeField] private Image guideImage;   // 교체할 UI Image
    [SerializeField] private Sprite heavyDefaultSprite;
    [SerializeField] private Sprite heavyDetectedSprite;

    [Header("인풋모드")]
    private InputModeManager inputModeManager;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 우측 하단 버튼 가이드를 보여주기
    public void OpenButtonGuidePanel(bool isHeavy)
    {
        // 이미지 설정
        //EImage.sprite = itemData.thumbnail;
        
        // UI 패널 표시
        GameUIManager.Instance.ShowPanel("ButtonGuidePanel", true);
        
        if (heavyAnchorImg) heavyAnchorImg.SetActive(isHeavy);
        if (lightAnchorImg) lightAnchorImg.SetActive(!isHeavy);
    }
    
    public void SetHeavyDetectedGuide(bool detected)
    {
        if (guideImage == null) return;
        guideImage.sprite = detected ? heavyDetectedSprite : heavyDefaultSprite;
    }
    
    public void CloseButtonGuidePanel()
    {
        GameUIManager.Instance.ShowPanel("ButtonGuidePanel", false);
    }

    public bool IsMemoryImageOpen { get; private set; }

    // 화면 전체를 채우는 이미지 보여주기
    public void OpenMemoryImage(int index)
    {
        if (index < 0 || index >= memoryImages.Count)
        {
            DebugLogger.Log($"[GuideUIManager] 유효하지 않은 메모리 인덱스: {index}");
            return;
        }
    
        foreach (var memory in memoryImages)
            memory.SetActive(false);
        memoryImages[index].SetActive(true);
        IsMemoryImageOpen = true;
        if (inputModeManager != null)
        {
            inputModeManager.SwitchInputMode(InputMode.UI);
            DebugLogger.Log("[GameUIManager] UI 모드 유지");
        }
        else
        {
            DebugLogger.Log("인풋모드 없음");
        }
    }
    
    public void CloseMemoryImage()
    {
        foreach (var memory in memoryImages)
            memory.SetActive(false);
        IsMemoryImageOpen = false;
    
        if (inputModeManager != null)
        {
            inputModeManager.SwitchInputMode(InputMode.Player);
            DebugLogger.Log("[GameUIManager] Player 모드로 전환");
        }
        else
        {
            DebugLogger.Log("인풋모드 없음");
        }
    }
}
