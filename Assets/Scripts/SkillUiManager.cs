using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillUIManager : MonoBehaviour
{
    [Serializable]
    public class SkillData
    {
        public string skillName = "Skill";
        public Button skillButton;
        public Image skillIcon;
        public Sprite activeSkillIcon;
        public Sprite inactiveSkillIcon;
        public SkillPanelIcons skillPanel;
        [HideInInspector] public RectTransform skillRect;
    }

    [Header("UI References")]
    [SerializeField] private Button mainToggleButton;
    [SerializeField] private RectTransform menuRootTransform;
    [SerializeField] private CanvasGroup skillWindow;
    [SerializeField] private TextMeshProUGUI skillNameLabel;

    [Header("Skills")]
    [SerializeField] private SkillData[] skillList;

    [Header("Layout")]
    [SerializeField] private float radialMenuRadius = 150f;
    [SerializeField] private float angleOffset = 0f;
    [SerializeField] private float selectedSkillScale = 1.2f;
    [SerializeField] private float unselectedSkillScale = 1f;

    [Header("Animation Timings")]
    [SerializeField] private float fadeDuration = 0.20f;
    [SerializeField] private float scaleDuration = 0.20f;
    [SerializeField] private float expandDuration = 0.25f;
    [SerializeField] private float collapseDuration = 0.20f;
    [SerializeField] private float rotationDuration = 0.25f;

    [Header("Scales")]
    [SerializeField] private float openWindowScale = 1.2f;
    [SerializeField] private float closedWindowScale = 0.8f;

    [Header("Initial State")]
    [SerializeField] private int defaultSkillIndex = 0;
    [SerializeField] private bool startOpen = false;

    public event Action<int> OnSkillChanged;

    private int currentSkillIndex = -1;
    private bool isWindowOpen = false;
    private bool initialPanelShown = false;

    private Vector2[] radialPositions;
    private Vector2 topSkillPosition;

    private Coroutine windowAnimationRoutine;
    private Coroutine menuAnimationRoutine;
    private Coroutine rotationRoutine;
    private Coroutine panelSwitchRoutine;

    private RectTransform mainButtonRectTransform;
    private Quaternion savedMenuRotation = Quaternion.identity;

    private readonly WaitForEndOfFrame waitForFrameEnd = new WaitForEndOfFrame();

    private void Awake()
    {
        mainButtonRectTransform = mainToggleButton.transform as RectTransform;
        if (skillList != null)
        {
            for (int skillIndex = 0; skillIndex < skillList.Length; skillIndex++)
            {
                SkillData skill = skillList[skillIndex];
                if (skill.skillButton != null)
                {
                    int capturedIndex = skillIndex;
                    skill.skillButton.onClick.AddListener(() => OnSkillClick(capturedIndex));
                    skill.skillRect = skill.skillButton.transform as RectTransform;
                }
            }
        }
        currentSkillIndex = Mathf.Clamp(defaultSkillIndex, 0, skillList.Length - 1);
        topSkillPosition = new Vector2(0, radialMenuRadius);
        ComputeRadialPositions();
        SetWindowVisible(false, true);
    }

    private void Start()
    {
        foreach (SkillData skill in skillList)
        {
            if (!skill.skillPanel) continue;
            skill.skillPanel.gameObject.SetActive(true);
            skill.skillPanel.SnapClosed();
            skill.skillPanel.gameObject.SetActive(false);
        }
        SelectSkill(currentSkillIndex, true);
        SnapMenuCollapsed();
        if (startOpen)
        {
            ToggleWindow(true, true);
            ShowPanel(currentSkillIndex, false);
            initialPanelShown = true;
        }
    }

    private void OnEnable()
    {
        if (mainToggleButton != null)
            mainToggleButton.onClick.AddListener(ToggleWindow);
    }

    private void OnDisable()
    {
        if (mainToggleButton != null)
            mainToggleButton.onClick.RemoveListener(ToggleWindow);
    }

    public void ToggleWindow() => ToggleWindow(!isWindowOpen, false);
    public void OpenWindow() => ToggleWindow(true, false);
    public void CloseWindow() => ToggleWindow(false, false);

    private void ToggleWindow(bool open, bool immediate)
    {
        if (isWindowOpen == open && !immediate) return;
        isWindowOpen = open;
        StopRoutine(ref windowAnimationRoutine);
        windowAnimationRoutine = StartCoroutine(WindowAnimation(open, immediate));
    }

    private IEnumerator WindowAnimation(bool open, bool immediate)
    {
        if (open)
        {
            SetWindowVisible(true, false);
            AnimateMenu(true, immediate);
            StartRotateTo(savedMenuRotation, immediate);
            if (!initialPanelShown)
            {
                StartShowInitialPanel(immediate);
                initialPanelShown = true;
            }
        }
        else
        {
            savedMenuRotation = menuRootTransform.localRotation;
            StartRotateTo(Quaternion.identity, immediate);
            AnimateMenu(false, immediate);
        }
        float fadeTime = immediate ? 0f : fadeDuration;
        float scaleTime = immediate ? 0f : scaleDuration;
        yield return AnimateAlphaAndScale(
            skillWindow,
            mainButtonRectTransform,
            skillWindow.alpha,
            open ? 1f : 0f,
            mainButtonRectTransform.localScale,
            Vector3.one * (open ? openWindowScale : closedWindowScale),
            fadeTime,
            scaleTime
        );
        if (!open) SetWindowVisible(false, true);
    }

    private void SetWindowVisible(bool visible, bool immediate)
    {
        skillWindow.interactable = visible;
        skillWindow.blocksRaycasts = visible;
        if (immediate) skillWindow.alpha = visible ? 1f : 0f;
    }

    private IEnumerator AnimateAlphaAndScale(CanvasGroup canvasGroup,
                                             RectTransform targetTransform,
                                             float startAlpha,
                                             float targetAlpha,
                                             Vector3 startScale,
                                             Vector3 targetScale,
                                             float alphaDuration,
                                             float scaleDuration)
    {
        float time = 0f;
        while (time < 1f)
        {
            time += Time.unscaledDeltaTime / Mathf.Max(alphaDuration, 0.0001f);
            float interpolation = Mathf.Clamp01(time);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, interpolation);
            float scaleInterpolation = alphaDuration > 0f
                ? Mathf.Clamp01(time * (alphaDuration / Mathf.Max(scaleDuration, 0.0001f)))
                : 1f;
            targetTransform.localScale = Vector3.Lerp(startScale, targetScale, scaleInterpolation);
            yield return null;
        }
        canvasGroup.alpha = targetAlpha;
        targetTransform.localScale = targetScale;
    }

    private void AnimateMenu(bool expand, bool immediate)
    {
        StopRoutine(ref menuAnimationRoutine);
        menuAnimationRoutine = StartCoroutine(MenuExpandCollapse(expand, immediate));
    }

    private IEnumerator MenuExpandCollapse(bool expand, bool immediate)
    {
        float duration = expand ? expandDuration : collapseDuration;
        if (immediate) duration = 0f;
        Vector2[] startPositions = new Vector2[skillList.Length];
        Vector3[] startScales = new Vector3[skillList.Length];
        for (int skillIndex = 0; skillIndex < skillList.Length; skillIndex++)
        {
            RectTransform skillRect = skillList[skillIndex].skillRect;
            startPositions[skillIndex] = skillRect.anchoredPosition;
            startScales[skillIndex] = skillRect.localScale;
            skillRect.gameObject.SetActive(true);
        }
        float time = 0f;
        while (time < 1f)
        {
            time += Time.unscaledDeltaTime / Mathf.Max(duration, 0.0001f);
            float interpolation = Mathf.SmoothStep(0, 1, Mathf.Clamp01(time));
            for (int skillIndex = 0; skillIndex < skillList.Length; skillIndex++)
            {
                Vector2 targetPosition = expand ? radialPositions[skillIndex] : topSkillPosition;
                Vector3 targetScale = (!expand && skillIndex != currentSkillIndex)
                    ? Vector3.zero
                    : Vector3.one * (skillIndex == currentSkillIndex ? selectedSkillScale : unselectedSkillScale);
                skillList[skillIndex].skillRect.anchoredPosition = Vector2.Lerp(startPositions[skillIndex], targetPosition, interpolation);
                skillList[skillIndex].skillRect.localScale = Vector3.Lerp(startScales[skillIndex], targetScale, interpolation);
            }
            yield return null;
        }
        for (int skillIndex = 0; skillIndex < skillList.Length; skillIndex++)
        {
            skillList[skillIndex].skillRect.anchoredPosition = expand ? radialPositions[skillIndex] : topSkillPosition;
            skillList[skillIndex].skillRect.localScale = (!expand && skillIndex != currentSkillIndex)
                ? Vector3.zero
                : Vector3.one * (skillIndex == currentSkillIndex ? selectedSkillScale : unselectedSkillScale);
            skillList[skillIndex].skillRect.gameObject.SetActive(expand || skillIndex == currentSkillIndex);
        }
    }

    private void StartRotateTo(Quaternion targetRotation, bool immediate)
    {
        StopRoutine(ref rotationRoutine);
        rotationRoutine = StartCoroutine(RotateTo(targetRotation, immediate ? 0f : rotationDuration));
    }

    private void StartRotateToSkill(int skillIndex)
    {
        StopRoutine(ref rotationRoutine);
        float angle = -((360f / Mathf.Max(1, skillList.Length)) * skillIndex) + angleOffset;
        Quaternion targetRotation = Quaternion.Euler(0, 0, angle);
        rotationRoutine = StartCoroutine(RotateTo(targetRotation, rotationDuration, true));
    }

    private IEnumerator RotateTo(Quaternion targetRotation, float duration, bool saveEndRotation = false)
    {
        Quaternion startRotation = menuRootTransform.localRotation;
        float time = 0f;
        while (time < 1f)
        {
            time += Time.unscaledDeltaTime / Mathf.Max(duration, 0.0001f);
            float interpolation = Mathf.SmoothStep(0, 1, Mathf.Clamp01(time));
            menuRootTransform.localRotation = Quaternion.Lerp(startRotation, targetRotation, interpolation);
            yield return null;
        }
        menuRootTransform.localRotation = targetRotation;
        if (saveEndRotation) savedMenuRotation = targetRotation;
    }

    private void OnSkillClick(int skillIndex)
    {
        if (!isWindowOpen) ToggleWindow(true, false);
        SelectSkill(skillIndex, false);
    }

    public void SelectSkill(int skillIndex, bool silent)
    {
        if (skillIndex < 0 || skillIndex >= skillList.Length) return;
        int previousSkillIndex = currentSkillIndex;
        currentSkillIndex = skillIndex;
        if (skillNameLabel) skillNameLabel.text = skillList[skillIndex].skillName;
        UpdateSkillIcons();
        if (silent)
        {
            ActivatePanelSnap(skillIndex);
            return;
        }
        if (skillIndex == previousSkillIndex) return;
        StartRotateToSkill(skillIndex);
        StartPanelSwitch(previousSkillIndex, currentSkillIndex);
        OnSkillChanged?.Invoke(skillIndex);
    }

    private void UpdateSkillIcons()
    {
        for (int skillIndex = 0; skillIndex < skillList.Length; skillIndex++)
        {
            bool isSelected = (skillIndex == currentSkillIndex);
            if (skillList[skillIndex].skillIcon)
                skillList[skillIndex].skillIcon.sprite = isSelected ? skillList[skillIndex].activeSkillIcon : skillList[skillIndex].inactiveSkillIcon;
            skillList[skillIndex].skillRect.localScale = Vector3.one * (isSelected ? selectedSkillScale : unselectedSkillScale);
        }
    }

    private void StartPanelSwitch(int previousSkillIndex, int nextSkillIndex)
    {
        if (previousSkillIndex == nextSkillIndex)
        {
            ActivatePanelSnap(nextSkillIndex);
            return;
        }
        StopRoutine(ref panelSwitchRoutine);
        panelSwitchRoutine = StartCoroutine(PanelSwitch(previousSkillIndex, nextSkillIndex));
    }

    private IEnumerator PanelSwitch(int previousSkillIndex, int nextSkillIndex)
    {
        SkillPanelIcons previousPanel = (previousSkillIndex >= 0 && previousSkillIndex < skillList.Length) ? skillList[previousSkillIndex].skillPanel : null;
        SkillPanelIcons nextPanel = (nextSkillIndex >= 0 && nextSkillIndex < skillList.Length) ? skillList[nextSkillIndex].skillPanel : null;
        if (previousPanel)
        {
            previousPanel.AnimateIn();
            yield return new WaitForSecondsRealtime(previousPanel.TotalInTime * 0.9f);
            previousPanel.gameObject.SetActive(false);
        }
        if (nextPanel)
        {
            nextPanel.gameObject.SetActive(true);
            nextPanel.SnapClosed();
            yield return waitForFrameEnd;
            nextPanel.AnimateOut();
        }
    }

    private void ActivatePanelSnap(int skillIndex)
    {
        for (int index = 0; index < skillList.Length; index++)
        {
            SkillPanelIcons panel = skillList[index].skillPanel;
            if (!panel) continue;
            bool isActive = (index == skillIndex);
            panel.gameObject.SetActive(isActive);
            if (isActive) panel.SnapClosed();
        }
    }

    private void ShowPanel(int skillIndex, bool animate)
    {
        if (skillIndex < 0 || skillIndex >= skillList.Length) return;
        SkillPanelIcons panel = skillList[skillIndex].skillPanel;
        if (!panel) return;
        panel.gameObject.SetActive(true);
        if (animate)
        {
            panel.SnapClosed();
            panel.AnimateOut();
        }
        else
        {
            panel.SnapOpened();
        }
    }

    private void StartShowInitialPanel(bool immediate)
    {
        StopRoutine(ref panelSwitchRoutine);
        panelSwitchRoutine = StartCoroutine(ShowInitialPanelRoutine(immediate));
    }

    private IEnumerator ShowInitialPanelRoutine(bool immediate)
    {
        if (currentSkillIndex < 0 || currentSkillIndex >= skillList.Length) yield break;
        SkillPanelIcons panel = skillList[currentSkillIndex].skillPanel;
        if (!panel) yield break;
        panel.gameObject.SetActive(true);
        panel.SnapClosed();
        if (!immediate)
            yield return waitForFrameEnd;
        panel.AnimateOut();
    }

    private void ComputeRadialPositions()
    {
        int totalSkills = Mathf.Max(1, skillList.Length);
        radialPositions = new Vector2[totalSkills];
        float stepAngle = 360f / totalSkills;
        for (int skillIndex = 0; skillIndex < totalSkills; skillIndex++)
        {
            float angle = (angleOffset + stepAngle * skillIndex + 180f) * Mathf.Deg2Rad;
            radialPositions[skillIndex] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radialMenuRadius;
        }
    }

    private void SnapMenuCollapsed()
    {
        if (menuRootTransform) menuRootTransform.localRotation = Quaternion.identity;
        for (int skillIndex = 0; skillIndex < skillList.Length; skillIndex++)
        {
            RectTransform rectTransform = skillList[skillIndex].skillRect;
            rectTransform.anchoredPosition = topSkillPosition;
            rectTransform.localScale = (skillIndex == currentSkillIndex) ? Vector3.one * selectedSkillScale : Vector3.zero;
            rectTransform.gameObject.SetActive(skillIndex == currentSkillIndex);
        }
    }

    private void StopRoutine(ref Coroutine routine)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }
}
