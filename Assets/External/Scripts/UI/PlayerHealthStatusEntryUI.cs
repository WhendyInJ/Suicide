using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 한 명의 이름 + 체력 상태 UI 한 줄을 담당하는 스크립트.
/// 
/// 이 스크립트는 다음 기능을 담당한다.
/// 1. 플레이어 이름 표시
/// 2. 현재 체력 숫자 표시
/// 3. 체력 증감 방향에 따라 좌측 화살표 표시
/// 4. 피격 시 붉은 글로우, 회복 시 초록 글로우 재생
/// 5. 도트딜 / 도트힐처럼 체력이 여러 번 변할 때
///    좌측 상단에 -1 / +1 같은 숫자 팝업 반복 표시
/// 
/// 중요:
/// - 외부에서 HealthChangedEventArgs의 상세 정보를 몰라도 동작하도록
///   "이전 체력"을 내부에 저장해서 변화량을 계산한다.
/// - 따라서 PlayerHealth에서 HealthChanged 이벤트만 정상적으로 발생하면
///   일반 피해 / 회복 / 도트딜 / 도트힐 모두 UI 반영이 가능하다.
/// </summary>
public class PlayerHealthStatusEntryUI : MonoBehaviour
{
    [Header("기본 텍스트 참조")]

    /// <summary>
    /// 플레이어 닉네임을 표시하는 텍스트.
    /// </summary>
    [SerializeField] private TMP_Text playerNameText;

    /// <summary>
    /// 현재 체력을 표시하는 텍스트.
    /// 예: 95
    /// </summary>
    [SerializeField] private TMP_Text healthValueText;

    /// <summary>
    /// 체력 증감 방향을 표시하는 좌측 화살표 텍스트.
    /// 예: ▲ / ▼ / 비움
    /// 
    /// 이미지 대신 텍스트 화살표를 쓰는 방식이라
    /// 별도 스프라이트 없이 바로 붙이기 쉽다.
    /// </summary>
    [SerializeField] private TMP_Text changeArrowText;

    [Header("글로우 참조")]

    /// <summary>
    /// 이름 + 체력 UI 뒤에서 색 글로우를 보여줄 이미지.
    /// 
    /// 사용 권장:
    /// - Entry Root의 가장 뒤에 배치
    /// - 살짝 크게 만든 둥근 배경 이미지
    /// - 기본 상태 alpha 0
    /// </summary>
    [SerializeField] private Image feedbackGlowImage;

    [Header("팝업 숫자 참조")]

    /// <summary>
    /// 도트딜 / 도트힐 숫자 팝업이 생성될 기준 위치.
    /// 
    /// 체력 텍스트바의 좌측 상단 앵커 오브젝트를 두고 연결하는 것을 권장한다.
    /// </summary>
    [SerializeField] private RectTransform tickPopupSpawnRoot;

    /// <summary>
    /// +1 / -1 팝업용 TMP 텍스트 프리팹.
    /// 
    /// 반드시 UI용 TextMeshProUGUI 프리팹을 연결하는 것을 권장한다.
    /// </summary>
    [SerializeField] private TMP_Text tickPopupTextPrefab;

    [Header("표시 포맷")]

    /// <summary>
    /// 플레이어 이름이 비어 있을 때 대신 표시할 문자열.
    /// </summary>
    [SerializeField] private string fallbackPlayerName = "Unknown";

    /// <summary>
    /// 체력 표시 포맷 문자열.
    /// 예: "{0}" -> 95
    /// 예: "HP {0}" -> HP 95
    /// </summary>
    [SerializeField] private string healthFormat = "{0}";

    [Header("화살표 표시 설정")]

    /// <summary>
    /// 체력이 증가했을 때 표시할 화살표 문자.
    /// </summary>
    [SerializeField] private string increaseArrowSymbol = "▲";

    /// <summary>
    /// 체력이 감소했을 때 표시할 화살표 문자.
    /// </summary>
    [SerializeField] private string decreaseArrowSymbol = "▼";

    /// <summary>
    /// 변화가 없을 때 표시할 문자열.
    /// 보통 빈 문자열로 두는 것을 권장한다.
    /// </summary>
    [SerializeField] private string neutralArrowSymbol = string.Empty;

    /// <summary>
    /// 체력 증가 시 화살표 색상.
    /// </summary>
    [SerializeField] private Color increaseArrowColor = new Color(0.35f, 1f, 0.45f, 1f);

    /// <summary>
    /// 체력 감소 시 화살표 색상.
    /// </summary>
    [SerializeField] private Color decreaseArrowColor = new Color(1f, 0.35f, 0.35f, 1f);

    /// <summary>
    /// 기본 화살표 색상.
    /// </summary>
    [SerializeField] private Color neutralArrowColor = new Color(1f, 1f, 1f, 0.5f);

    /// <summary>
    /// 화살표가 표시된 뒤 기본 상태로 돌아가기까지의 시간.
    /// </summary>
    [SerializeField, Min(0.01f)] private float arrowResetDelay = 0.6f;

    [Header("글로우 설정")]

    /// <summary>
    /// 피격 시 재생할 글로우 색상.
    /// </summary>
    [SerializeField] private Color damageGlowColor = new Color(1f, 0.2f, 0.2f, 1f);

    /// <summary>
    /// 회복 시 재생할 글로우 색상.
    /// </summary>
    [SerializeField] private Color healGlowColor = new Color(0.2f, 1f, 0.35f, 1f);

    /// <summary>
    /// 글로우가 시작될 때의 최대 알파값.
    /// </summary>
    [SerializeField, Range(0f, 1f)] private float glowMaxAlpha = 0.85f;

    /// <summary>
    /// 글로우가 사라지는 시간.
    /// </summary>
    [SerializeField, Min(0.01f)] private float glowFadeDuration = 0.4f;

    [Header("도트 숫자 팝업 설정")]

    /// <summary>
    /// 피해 숫자 색상.
    /// </summary>
    [SerializeField] private Color damageTickColor = new Color(1f, 0.25f, 0.25f, 1f);

    /// <summary>
    /// 회복 숫자 색상.
    /// </summary>
    [SerializeField] private Color healTickColor = new Color(0.25f, 1f, 0.35f, 1f);

    /// <summary>
    /// 팝업 텍스트가 살아있는 시간.
    /// </summary>
    [SerializeField, Min(0.05f)] private float tickPopupLifetime = 0.75f;

    /// <summary>
    /// 팝업 텍스트가 위로 이동하는 거리.
    /// </summary>
    [SerializeField, Min(0f)] private float tickPopupMoveY = 22f;

    /// <summary>
    /// 팝업이 생성될 때 시작 위치에 약간 랜덤 오프셋을 줄 범위.
    /// x, y 각각 ±범위로 사용한다.
    /// </summary>
    [SerializeField] private Vector2 tickPopupRandomOffset = new Vector2(8f, 4f);

    [Header("런타임 상태")]

    /// <summary>
    /// 처음 체력값을 이미 한 번 받은 적이 있는지 여부.
    /// 
    /// 첫 초기화 때는 "0 -> 현재체력"으로 잘못 판단해서
    /// 회복 이펙트가 뜨지 않도록 막는 용도다.
    /// </summary>
    [SerializeField] private bool hasInitializedHealth = false;

    /// <summary>
    /// 이전에 표시했던 체력값.
    /// 다음 SetData 시 현재값과 비교해서 증감량을 계산한다.
    /// </summary>
    [SerializeField] private float lastKnownHealth = 0f;

    /// <summary>
    /// 현재 재생 중인 글로우 코루틴 참조.
    /// 중복 재생 시 이전 코루틴을 끊기 위해 사용한다.
    /// </summary>
    private Coroutine glowCoroutine = null;

    /// <summary>
    /// 현재 재생 중인 화살표 초기화 코루틴 참조.
    /// 새 변화가 들어오면 다시 시작한다.
    /// </summary>
    private Coroutine arrowResetCoroutine = null;

    /// <summary>
    /// 시작 시 UI의 기본 상태를 정리한다.
    /// </summary>
    private void Awake()
    {
        ApplyNeutralArrow();
        HideGlowImmediate();
    }

    /// <summary>
    /// 비활성화 시 진행 중 코루틴을 정리하고
    /// 잔상처럼 남는 UI 효과를 제거한다.
    /// </summary>
    private void OnDisable()
    {
        if (glowCoroutine != null)
        {
            StopCoroutine(glowCoroutine);
            glowCoroutine = null;
        }

        if (arrowResetCoroutine != null)
        {
            StopCoroutine(arrowResetCoroutine);
            arrowResetCoroutine = null;
        }

        ApplyNeutralArrow();
        HideGlowImmediate();
    }

    /// <summary>
    /// 플레이어 이름 텍스트를 갱신한다.
    /// </summary>
    /// <param name="playerName">표시할 플레이어 이름.</param>
    public void SetPlayerName(string playerName)
    {
        if (playerNameText == null)
            return;

        playerNameText.text = string.IsNullOrWhiteSpace(playerName)
            ? fallbackPlayerName
            : playerName;
    }

    /// <summary>
    /// 체력 숫자 텍스트만 갱신한다.
    /// 
    /// 주의:
    /// 이 함수는 "숫자 표시"만 담당한다.
    /// 화살표 / 글로우 / 팝업 숫자 같은 변화 연출은 SetData에서 처리한다.
    /// </summary>
    /// <param name="currentHealth">현재 체력.</param>
    public void SetHealth(float currentHealth)
    {
        if (healthValueText == null)
            return;

        int displayedHealth = Mathf.CeilToInt(Mathf.Max(0f, currentHealth));
        healthValueText.text = string.Format(healthFormat, displayedHealth);
    }

    /// <summary>
    /// 이름과 체력을 함께 갱신한다.
    /// 
    /// 이 함수가 Entry UI의 핵심 갱신 함수이며,
    /// 체력 변화량을 계산해 방향 화살표, 글로우, 팝업 숫자를 처리한다.
    /// </summary>
    /// <param name="playerName">플레이어 이름.</param>
    /// <param name="currentHealth">현재 체력.</param>
    public void SetData(string playerName, float currentHealth)
    {
        SetPlayerName(playerName);
        SetHealth(currentHealth);
        ProcessHealthChangeFeedback(currentHealth);
    }

    /// <summary>
    /// 현재 체력과 이전 체력을 비교해서
    /// UI 피드백(화살표, 글로우, 도트 숫자 팝업)을 재생한다.
    /// </summary>
    /// <param name="currentHealth">방금 전달받은 최신 체력값.</param>
    private void ProcessHealthChangeFeedback(float currentHealth)
    {
        // 첫 초기화 시에는 변화량 연출 없이 값만 저장한다.
        if (hasInitializedHealth == false)
        {
            hasInitializedHealth = true;
            lastKnownHealth = currentHealth;
            ApplyNeutralArrow();
            HideGlowImmediate();
            return;
        }

        float delta = currentHealth - lastKnownHealth;

        // 변화가 없으면 숫자만 유지하고 종료한다.
        if (Mathf.Approximately(delta, 0f))
        {
            lastKnownHealth = currentHealth;
            return;
        }

        bool isHeal = delta > 0f;

        // 0.2 같은 소수 변화도 시각적으로는 1로 보이게 하려면 올림 처리하는 편이 안전하다.
        int popupAmount = Mathf.CeilToInt(Mathf.Abs(delta));
        if (popupAmount <= 0)
        {
            popupAmount = 1;
        }

        ApplyDirectionArrow(isHeal);
        PlayGlow(isHeal ? healGlowColor : damageGlowColor);
        SpawnTickPopup(
            isHeal ? $"+{popupAmount}" : $"-{popupAmount}",
            isHeal ? healTickColor : damageTickColor);

        lastKnownHealth = currentHealth;
    }

    /// <summary>
    /// 체력 변화 방향에 맞는 화살표를 표시하고,
    /// 잠시 후 기본 상태로 돌리는 코루틴을 시작한다.
    /// </summary>
    /// <param name="isIncrease">true면 증가, false면 감소.</param>
    private void ApplyDirectionArrow(bool isIncrease)
    {
        if (changeArrowText == null)
            return;

        changeArrowText.text = isIncrease ? increaseArrowSymbol : decreaseArrowSymbol;
        changeArrowText.color = isIncrease ? increaseArrowColor : decreaseArrowColor;

        if (arrowResetCoroutine != null)
        {
            StopCoroutine(arrowResetCoroutine);
        }

        arrowResetCoroutine = StartCoroutine(CoResetArrowAfterDelay());
    }

    /// <summary>
    /// 화살표를 기본 상태로 되돌린다.
    /// </summary>
    private void ApplyNeutralArrow()
    {
        if (changeArrowText == null)
            return;

        changeArrowText.text = neutralArrowSymbol;
        changeArrowText.color = neutralArrowColor;
    }

    /// <summary>
    /// 일정 시간 뒤 화살표를 기본 상태로 되돌리는 코루틴.
    /// </summary>
    private IEnumerator CoResetArrowAfterDelay()
    {
        yield return new WaitForSecondsRealtime(arrowResetDelay);
        ApplyNeutralArrow();
        arrowResetCoroutine = null;
    }

    /// <summary>
    /// 지정한 색상으로 글로우를 재생한다.
    /// 기존 글로우가 재생 중이면 끊고 새 효과로 덮어쓴다.
    /// </summary>
    /// <param name="glowColor">재생할 글로우 색상.</param>
    private void PlayGlow(Color glowColor)
    {
        if (feedbackGlowImage == null)
            return;

        if (glowCoroutine != null)
        {
            StopCoroutine(glowCoroutine);
        }

        glowCoroutine = StartCoroutine(CoPlayGlow(glowColor));
    }

    /// <summary>
    /// 글로우를 즉시 숨긴다.
    /// </summary>
    private void HideGlowImmediate()
    {
        if (feedbackGlowImage == null)
            return;

        Color color = feedbackGlowImage.color;
        color.a = 0f;
        feedbackGlowImage.color = color;
        feedbackGlowImage.enabled = false;
    }

    /// <summary>
    /// 글로우를 진하게 켰다가 서서히 사라지게 만드는 코루틴.
    /// </summary>
    /// <param name="baseColor">글로우 기본 색상.</param>
    private IEnumerator CoPlayGlow(Color baseColor)
    {
        if (feedbackGlowImage == null)
            yield break;

        feedbackGlowImage.enabled = true;

        float elapsedTime = 0f;
        Color color = baseColor;
        color.a = glowMaxAlpha;
        feedbackGlowImage.color = color;

        while (elapsedTime < glowFadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / glowFadeDuration);

            color.a = Mathf.Lerp(glowMaxAlpha, 0f, normalizedTime);
            feedbackGlowImage.color = color;

            yield return null;
        }

        HideGlowImmediate();
        glowCoroutine = null;
    }

    /// <summary>
    /// 좌측 상단에 +1 / -1 같은 숫자 팝업을 생성한다.
    /// 도트딜 / 도트힐처럼 여러 번 이벤트가 오면 여러 번 생성된다.
    /// </summary>
    /// <param name="message">표시할 문자열.</param>
    /// <param name="popupColor">문자 색상.</param>
    private void SpawnTickPopup(string message, Color popupColor)
    {
        if (tickPopupTextPrefab == null)
            return;

        RectTransform spawnRoot = tickPopupSpawnRoot != null
            ? tickPopupSpawnRoot
            : transform as RectTransform;

        if (spawnRoot == null)
            return;

        TMP_Text popupInstance = Instantiate(tickPopupTextPrefab, spawnRoot);
        popupInstance.text = message;
        popupInstance.color = popupColor;

        RectTransform popupRect = popupInstance.rectTransform;

        Vector2 startPosition = popupRect.anchoredPosition;
        startPosition += new Vector2(
            Random.Range(-tickPopupRandomOffset.x, tickPopupRandomOffset.x),
            Random.Range(-tickPopupRandomOffset.y, tickPopupRandomOffset.y));

        popupRect.anchoredPosition = startPosition;

        StartCoroutine(CoAnimateTickPopup(popupInstance, startPosition, popupColor));
    }

    /// <summary>
    /// 팝업 텍스트를 위로 살짝 띄우면서 투명하게 사라지게 만드는 코루틴.
    /// </summary>
    /// <param name="popupText">애니메이션할 팝업 텍스트 인스턴스.</param>
    /// <param name="startPosition">시작 위치.</param>
    /// <param name="baseColor">기본 문자 색상.</param>
    private IEnumerator CoAnimateTickPopup(TMP_Text popupText, Vector2 startPosition, Color baseColor)
    {
        if (popupText == null)
            yield break;

        RectTransform popupRect = popupText.rectTransform;
        float elapsedTime = 0f;

        while (elapsedTime < tickPopupLifetime)
        {
            if (popupText == null)
                yield break;

            elapsedTime += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / tickPopupLifetime);

            if (popupRect != null)
            {
                popupRect.anchoredPosition = startPosition + new Vector2(0f, tickPopupMoveY * normalizedTime);
                popupRect.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 1.08f, normalizedTime);
            }

            Color color = baseColor;
            color.a = Mathf.Lerp(1f, 0f, normalizedTime);
            popupText.color = color;

            yield return null;
        }

        if (popupText != null)
        {
            Destroy(popupText.gameObject);
        }
    }
}