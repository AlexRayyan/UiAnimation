using System.Collections;
using UnityEngine;

public class SkillPanelIcons : MonoBehaviour
{
    [Header("Icon References")]
    [SerializeField] RectTransform[] topIcons;
    [SerializeField] RectTransform[] bottomIcons;

    [Header("Animation Settings")]
    [SerializeField] float moveDuration = 0.25f;
    [SerializeField] float staggerDelay = 0.04f;
    [SerializeField] float topY = 120f;
    [SerializeField] float bottomY = -120f;
    [SerializeField] AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public float TotalOutTime => (Mathf.Max(topIcons.Length, bottomIcons.Length) - 1) * staggerDelay + moveDuration;
    public float TotalInTime => (Mathf.Max(topIcons.Length, bottomIcons.Length) - 1) * staggerDelay + moveDuration;

    Coroutine animRoutine;
    readonly WaitForEndOfFrame waitFrame = new WaitForEndOfFrame();

    public void SnapClosed()
    {
        StopAnim();
        SetAllY(topIcons, 0f);
        SetAllY(bottomIcons, 0f);
    }

    public void SnapOpened()
    {
        StopAnim();
        SetAllY(topIcons, topY);
        SetAllY(bottomIcons, bottomY);
    }

    public void AnimateOut() => StartAnimation(true);
    public void AnimateIn() => StartAnimation(false);

    void StartAnimation(bool outward)
    {
        StopAnim();
        animRoutine = StartCoroutine(AnimateIcons(outward));
    }

    void StopAnim()
    {
        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = null;
    }

    IEnumerator AnimateIcons(bool outward)
    {
        yield return waitFrame;
        int count = Mathf.Max(topIcons.Length, bottomIcons.Length);
        for (int i = 0; i < count; i++)
        {
            if (i < topIcons.Length)
                StartCoroutine(MoveIcon(topIcons[i], outward ? topY : 0f));
            if (i < bottomIcons.Length)
                StartCoroutine(MoveIcon(bottomIcons[i], outward ? bottomY : 0f));

            yield return new WaitForSecondsRealtime(staggerDelay);
        }
        animRoutine = null;
    }

    IEnumerator MoveIcon(RectTransform icon, float targetY)
    {
        if (!icon) yield break;
        float startY = icon.anchoredPosition.y;
        float dur = Mathf.Max(moveDuration, 0.0001f);
        float time = 0f;

        while (time < 1f)
        {
            time += Time.unscaledDeltaTime / dur;
            float e = curve.Evaluate(Mathf.Clamp01(time));
            SetY(icon, Mathf.Lerp(startY, targetY, e));
            yield return null;
        }
        SetY(icon, targetY);
    }

    static void SetAllY(RectTransform[] icons, float y)
    {
        for (int i = 0; i < icons.Length; i++) SetY(icons[i], y);
    }

    static void SetY(RectTransform icon, float y)
    {
        if (!icon) return;
        var pos = icon.anchoredPosition;
        pos.y = y;
        icon.anchoredPosition = pos;
    }
}
