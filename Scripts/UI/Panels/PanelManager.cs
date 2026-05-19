using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 여러 UI 패널 중 하나만 활성화하도록 관리하는 클래스
/// </summary>
public class PanelManager : MonoBehaviour
{
    [Tooltip("패널들의 부모 오브젝트")]
    [SerializeField]
    private GameObject panelContainer;
    
    [Tooltip("이전 페이지 버튼")]
    [SerializeField]
    private Button previousButton;
    
    [Tooltip("다음 페이지 버튼")]
    [SerializeField]
    private Button nextButton;
    
    [Tooltip("닫기 버튼")]
    [SerializeField]
    private Button closeButton;

    
    private Image _previousButtonImage;
    private Image _nextButtonImage;
    
    [SerializeField, ReadOnly] private GameObject[] panels;
    
    // 현재 활성화된 패널의 인덱스 (0부터 시작)
    private int _currentPageIndex = 0;
    
    // OnValidate 추가 - 인스펙터에서 값 변경 시 자동 호출
    void OnValidate()
    {
        InitializePanels();
    }
    
    void Awake()
    {
        // Awake에서도 초기화 (런타임용)
        InitializePanels();
        
        if (previousButton != null)
        {
            _previousButtonImage = previousButton.GetComponent<Image>();
        }
    
        if (nextButton != null)
        {
            _nextButtonImage = nextButton.GetComponent<Image>();
        }
    }
    
    // 패널 초기화 메서드 추가
    /// <summary>
    /// 패널 컨테이너의 자식들을 패널 배열로 초기화
    /// </summary>
    private void InitializePanels()
    {
        if (panelContainer != null)
        {
            int childCount = panelContainer.transform.childCount;
            panels = new GameObject[childCount];
            
            for (int i = 0; i < childCount; i++)
            {
                panels[i] = panelContainer.transform.GetChild(i).gameObject;
            }
        }
        else
        {
            panels = new GameObject[0];
        }
    }
    
    void OnEnable()
    {
        // 활성화 시 첫 번째 패널만 활성화
        ShowPanel(0);
        if(closeButton) closeButton.gameObject.SetActive(false);
    }

    /// <summary>
    /// 지정된 인덱스의 패널을 활성화하고 나머지 패널은 모두 비활성화
    /// </summary>
    /// <param name="index">활성화할 패널의 인덱스</param>
    public void ShowPanel(int index)
    {
        if (panels == null || panels.Length == 0)
        {
            DebugLogger.LogError("PanelManager에 패널이 등록되지 않았습니다.");
            return;
        }
        if (index < 0 || index >= panels.Length)
        {
            DebugLogger.LogError($"잘못된 패널 인덱스입니다: {index}. 인덱스는 0과 {panels.Length - 1} 사이여야 합니다.");
            return;
        }
        
        // 모든 패널을 순회하며 비활성화
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null)
            {
                panels[i].SetActive(false);
            }
        }
        
        // 목표 인덱스의 패널만 활성화
        if (panels[index] != null)
        {
            panels[index].SetActive(true);
            _currentPageIndex = index;
            
            UpdateArrowButtons();
        }
    }
    
    /// <summary>
    /// 현재 페이지에 따라 화살표 버튼 활성화/비활성화
    /// </summary>
    private void UpdateArrowButtons()
    {
        // 이전 버튼: 첫 페이지가 아닐 때만 활성화
        if (previousButton != null)
        {
            previousButton.interactable = _currentPageIndex > 0;
            if (_previousButtonImage != null)
            {
                _previousButtonImage.enabled = _currentPageIndex > 0;
            }
        }
    
        // 다음 버튼: 마지막 페이지가 아닐 때만 활성화
        bool isLastPage = _currentPageIndex == panels.Length - 1;
    
        if (nextButton != null)
        {
            nextButton.interactable = !isLastPage;
            if (_nextButtonImage != null)
            {
                _nextButtonImage.enabled = !isLastPage;
            }
        }

        // 닫기 버튼: 마지막 페이지에서만 활성화
        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(isLastPage);
        }
    }
    
    /// <summary>
    /// 다음 페이지(패널)로 이동
    /// </summary>
    public void GoToNext()
    {
        if (_currentPageIndex < panels.Length - 1)
        {
            ShowPanel(_currentPageIndex + 1);
        }
    }
    
    /// <summary>
    /// 이전 페이지(패널)로 이동
    /// </summary>
    public void GoToPrevious()
    {
        if (_currentPageIndex > 0)
        {
            ShowPanel(_currentPageIndex - 1);
        }
    }
    
    /// <summary>
    /// 관리 중인 전체 패널의 개수를 반환
    /// </summary>
    /// <returns>패널 개수</returns>
    public int GetPanelCount()
    {
        return panels?.Length ?? 0;
    }
}