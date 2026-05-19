using UnityEngine;
using System.Collections.Generic;

public static class PanelStateFactory
{
    private static readonly Dictionary<string, IPanelState> _states = new Dictionary<string, IPanelState>
    {
        // HUD 패널
        { "QuestGuidePanel", new HUDPanelState() },
        { "ButtonGuidePanel", new HUDPanelState() },
        { "SaveDataPanel", new HUDPanelState() },
        
        // 플레이어 모드 패널
        { "CollisionPanel", new PlayerModePanelState() },
        
        // UI 모드 패널
        { "GuidePanel", new UIModePanelState() },
        { "SettingPanel", new UIModePanelState() },
        { "InventoryPanel", new UIModePanelState() },
        { "MenuPanel", new UIModePanelState() },
        { "QuestPanel", new UIModePanelState() },
        
        // 대화 (가상 패널)
        { "Dialogue", new DialoguePanelState() }
    };

    public static IPanelState GetState(string panelName)
    {
        return _states.TryGetValue(panelName, out var state) ? state : new DefaultPanelState();
    }
}