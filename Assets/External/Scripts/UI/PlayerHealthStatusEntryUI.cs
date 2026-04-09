using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 플레이어 한 명의 이름 + 체력 상태 UI 한 줄을 담당하는 스크립트.
///
/// 이 스크립트는 다음 기능을 담당한다.
/// 1. 플레이어 이름 표시
/// 2. 현재 체력 숫자 표시
/// 3. 체력 증가 / 감소 상태에 따라 상태 이미지 활성화 / 비활성화
/// 4. 도트딜 / 도트힐처럼 여러 번 체력 변화가 일어날 때
///    여러 개의 텍스트를 생성하여 위로 천천히 올라가며 사라지게 표시
/// 5. 플레이어 표시 순서(0,1,2,3)에 따라 Player1~Player4 테마 이미지 분배
///
/// 중요:
/// - glow 연출은 제거했다.
/// - 이전 체력값을 내부에 저장하여 체력 증가 / 감소를 판정한다.
/// - popup text prefab은 UI용 TMP_Text 프리팹을 사용한다.
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
    /// 예: 100
    /// </summary>
    [SerializeField] private TMP_Text healthValueText;

    [Header("상태 이미지 오브젝트")]

    /// <summary>
    /// 체력이 증가했을 때 잠깐 보여줄 상태 이미지 오브젝트.
    /// 예: 초록 상승 화살표
    /// </summary>
    [SerializeField] private GameObject increaseStateObject;

    /// <summary>
    /// 체력이 감소했을 때 잠깐 보여줄 상태 이미지 오브젝트.
    /// 예: 빨강 하강 화살표
    /// </summary>
    [SerializeField] private GameObject decreaseStateObject;

    /// <summary>
    /// 평상시 보여줄 기본 상태 이미지 오브젝트.
    /// 연결하지 않으면 기본 상태에서는 increase / decrease만 꺼진다.
    /// </summary>
    [SerializeField] private GameObject neutralStateObject;

    [Header("플레이어별 테마 이미지 오브젝트")]

    /// <summary>
    /// 첫 번째 플레이어 슬롯 테마 오브젝트.
    /// </summary>
    [SerializeField] private GameObject player1ThemeObject;

    /// <summary>
    /// 두 번째 플레이어 슬롯 테마 오브젝트.
    /// </summary>
    [SerializeField] private GameObject player2ThemeObject;

    /// <summary>
    /// 세 번째 플레이어 슬롯 테마 오브젝트.
    /// </summary>
    [SerializeField] private GameObject player3ThemeObject;

    /// <summary>
    /// 네 번째 플레이어 슬롯 테마 오브젝트.
    /// </summary>
    [SerializeField] private GameObject player4ThemeObject;

    [Header("도트딜 / 도트힐 플로팅 텍스트")]

    /// <summary>
    /// 플로팅 텍스트가 생성될 기준 위치.
    /// 체력 텍스트바 왼쪽 상단용 RectTransform 연결 권장.
    /// </summary>
    [SerializeField] private RectTransform tickPopupSpawnRoot;

    /// <summary>
    /// UI용 TMP_Text 플로팅 텍스트 프리팹.
    /// 이 프리팹을 여러 개 생성해서 -1 / +1 등을 띄운다.
    /// </summary>
    [SerializeField] private TMP_Text tickPopupTextPrefab;

    [Header("표시 포맷")]

    /// <summary>
    /// 플레이어 이름이 비어 있을 때 대신 표시할 문자열.
    /// </summary>
    [SerializeField] private string fallbackPlayerName = "Unknown";

    /// <summary>
    /// 체력 텍스트 표시 포맷.
    /// 예: "{0}" -> 100
    /// 예: "HP {0}" -> HP 100
    /// </summary>
    [SerializeField] private string healthFormat = "{0}";

    [Header("상태 표시 시간 설정")]

    /// <summary>
    /// 증가 / 감소 상태 이미지가 잠깐 표시된 뒤
    /// 다시 neutral 상태로 복귀하기까지의 시간.
    /// </summary>
    [SerializeField, Min(0.01f)] private float stateResetDelay = 0.6f;

    [Header("플로팅 텍스트 연출 설정")]

    /// <summary>
    /// 피해 플로팅 텍스트 색상.
    /// </summary>
    [SerializeField] private Color damageTickColor = new Color(1f, 0.35f, 0.35f, 1f);

    /// <summary>
    /// 회복 플로팅 텍스트 색상.
    /// </summary>
    [SerializeField] private Color healTickColor = new Color(0.35f, 1f, 0.45f, 1f);

    /// <summary>
    /// 플로팅 텍스트가 유지되는 시간.
    /// </summary>
    [SerializeField, Min(0.05f)] private float tickPopupLifetime = 0.85f;

    /// <summary>
    /// 플로팅 텍스트가 위로 이동할 거리.
    /// </summary>
    [SerializeField, Min(0f)] private float tickPopupMoveY = 28f;

    /// <summary>
    /// 플로팅 텍스트 시작 위치의 랜덤 오프셋 범위.
    /// x, y 각각 ± 범위로 사용한다.
    /// 여러 개가 동시에 뜰 때 완전히 겹치지 않게 하는 용도다.
    /// </summary>
    [SerializeField] private Vector2 tickPopupRandomOffset = new Vector2(10f, 6f);

    /// <summary>
    /// 플로팅 텍스트 시작 스케일.
    /// </summary>
    [SerializeField] private float tickPopupStartScale = 1f;

    /// <summary>
    /// 플로팅 텍스트 끝 스케일.
    /// 살짝 커지거나 줄어드는 느낌을 줄 수 있다.
    /// </summary>
    [SerializeField] private float tickPopupEndScale = 1.08f;

    [Header("런타임 상태")]

    /// <summary>
    /// 초기 체력값을 이미 한 번 받은 적이 있는지 여부.
    /// 첫 갱신 시 잘못된 증가 / 감소 판정을 막기 위해 사용한다.
    /// </summary>
    [SerializeField] private bool hasInitializedHealth = false;

    /// <summary>
    /// 직전에 표시했던 체력값.
    /// 현재 체력과 비교해서 변화량을 계산한다.
    /// </summary>
    [SerializeField] private float lastKnownHealth = 0f;

    /// <summary>
    /// 현재 엔트리가 어떤 플레이어 테마(0~3)를 사용하는지 기억하는 값.
    /// 0 -> Player1, 1 -> Player2, 2 -> Player3, 3 -> Player4
    /// </summary>
    [SerializeField] private int currentThemeIndex = 0;

    /// <summary>
    /// 상태 이미지 초기화 코루틴 참조.
    /// 연속 변화 시 이전 코루틴을 중단하고 새로 시작하기 위해 사용한다.
    /// </summary>
    private Coroutine stateResetCoroutine = null;

    /// <summary>
    /// 시작 시 기본 상태 이미지와 플레이어 테마를 적용한다.
    /// </summary>
    private void Awake()
    {
        ApplyNeutralStateVisual();
        ApplyThemeByIndex(currentThemeIndex);
    }

    /// <summary>
    /// 비활성화 시 진행 중 코루틴을 정리하고
    /// 상태 이미지를 기본 상태로 되돌린다.
    /// </summary>
    private void OnDisable()
    {
        if (stateResetCoroutine != null)
        {
            StopCoroutine(stateResetCoroutine);
            stateResetCoroutine = null;
        }

        ApplyNeutralStateVisual();
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
    /// 체력 숫자 텍스트를 갱신한다.
    /// 표시용으로는 소수점을 올림 처리하여 정수처럼 보이게 한다.
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
    /// 보드에서 전달한 표시 순서 index를 받아
    /// Player1~Player4 테마 이미지를 활성화 / 비활성화 한다.
    /// </summary>
    /// <param name="displayIndex">보드에서의 현재 표시 순서 index.</param>
    public void SetPlayerThemeByDisplayIndex(int displayIndex)
    {
        currentThemeIndex = NormalizeThemeIndex(displayIndex);
        ApplyThemeByIndex(currentThemeIndex);
    }

    /// <summary>
    /// 이름과 체력을 함께 갱신하는 메인 함수.
    /// 체력 변화량을 계산해 상태 이미지와 플로팅 텍스트를 처리한다.
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
    /// 이전 체력과 현재 체력을 비교해서
    /// 증가 / 감소 상태 이미지와 플로팅 텍스트를 갱신한다.
    /// </summary>
    /// <param name="currentHealth">최신 체력값.</param>
    private void ProcessHealthChangeFeedback(float currentHealth)
    {
        if (hasInitializedHealth == false)
        {
            hasInitializedHealth = true;
            lastKnownHealth = currentHealth;
            ApplyNeutralStateVisual();
            return;
        }

        float delta = currentHealth - lastKnownHealth;

        if (Mathf.Approximately(delta, 0f))
        {
            lastKnownHealth = currentHealth;
            return;
        }

        bool isHeal = delta > 0f;

        int amount = Mathf.CeilToInt(Mathf.Abs(delta));
        if (amount <= 0)
        {
            amount = 1;
        }

        ApplyTemporaryStateVisual(isHeal);

        if (isHeal)
        {
            SpawnTickPopup($"+{amount}", healTickColor);
        }
        else
        {
            SpawnTickPopup($"-{amount}", damageTickColor);
        }

        lastKnownHealth = currentHealth;
    }

    /// <summary>
    /// 현재 theme index에 맞는 플레이어 전용 테마 이미지만 활성화한다.
    /// 나머지는 모두 비활성화한다.
    /// </summary>
    /// <param name="themeIndex">0~3 테마 index.</param>
    private void ApplyThemeByIndex(int themeIndex)
    {
        SetActiveIfNotNull(player1ThemeObject, themeIndex == 0);
        SetActiveIfNotNull(player2ThemeObject, themeIndex == 1);
        SetActiveIfNotNull(player3ThemeObject, themeIndex == 2);
        SetActiveIfNotNull(player4ThemeObject, themeIndex == 3);
    }

    /// <summary>
    /// 플레이어 테마 index를 0~3 범위로 순환 보정한다.
    /// </summary>
    /// <param name="displayIndex">보드 표시 순서 index.</param>
    /// <returns>0~3으로 보정된 index.</returns>
    private int NormalizeThemeIndex(int displayIndex)
    {
        int normalizedIndex = displayIndex % 4;

        if (normalizedIndex < 0)
        {
            normalizedIndex += 4;
        }

        return normalizedIndex;
    }

    /// <summary>
    /// 증가 또는 감소 상태 이미지를 잠깐 보여주고,
    /// 일정 시간 뒤 neutral 상태로 복귀시킨다.
    /// </summary>
    /// <param name="isIncrease">true면 증가, false면 감소.</param>
    private void ApplyTemporaryStateVisual(bool isIncrease)
    {
        SetActiveIfNotNull(increaseStateObject, isIncrease);
        SetActiveIfNotNull(decreaseStateObject, isIncrease == false);
        SetActiveIfNotNull(neutralStateObject, false);

        if (stateResetCoroutine != null)
        {
            StopCoroutine(stateResetCoroutine);
        }

        stateResetCoroutine = StartCoroutine(CoResetStateVisualAfterDelay());
    }

    /// <summary>
    /// 기본 상태 이미지를 적용한다.
    /// neutralStateObject가 있으면 그것만 켜고,
    /// 없으면 increase / decrease만 끈다.
    /// </summary>
    private void ApplyNeutralStateVisual()
    {
        SetActiveIfNotNull(increaseStateObject, false);
        SetActiveIfNotNull(decreaseStateObject, false);

        if (neutralStateObject != null)
        {
            neutralStateObject.SetActive(true);
        }
    }

    /// <summary>
    /// 마지막 변화 이후 일정 시간이 지나면 상태 이미지를 기본 상태로 되돌린다.
    /// </summary>
    /// <returns>코루틴 열거자.</returns>
    private IEnumerator CoResetStateVisualAfterDelay()
    {
        yield return new WaitForSecondsRealtime(stateResetDelay);
        ApplyNeutralStateVisual();
        stateResetCoroutine = null;
    }

    /// <summary>
    /// 도트딜 / 도트힐용 플로팅 텍스트를 생성한다.
    /// 여러 개가 동시에 뜰 수 있으며,
    /// 위로 천천히 이동하면서 페이드아웃된다.
    /// </summary>
    /// <param name="message">표시할 문자열. 예: -1, +1</param>
    /// <param name="textColor">표시 색상.</param>
    private void SpawnTickPopup(string message, Color textColor)
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
        popupInstance.color = textColor;
        popupInstance.gameObject.SetActive(true);

        RectTransform popupRect = popupInstance.rectTransform;

        Vector2 startPosition = popupRect.anchoredPosition;
        startPosition += new Vector2(
            Random.Range(-tickPopupRandomOffset.x, tickPopupRandomOffset.x),
            Random.Range(-tickPopupRandomOffset.y, tickPopupRandomOffset.y));

        popupRect.anchoredPosition = startPosition;
        popupRect.localScale = Vector3.one * tickPopupStartScale;

        StartCoroutine(CoAnimateTickPopup(popupInstance, popupRect, startPosition, textColor));
    }

    /// <summary>
    /// 플로팅 텍스트를 위로 이동시키면서 서서히 사라지게 만든다.
    /// </summary>
    /// <param name="popupText">애니메이션할 TMP_Text 인스턴스.</param>
    /// <param name="popupRect">popupText의 RectTransform.</param>
    /// <param name="startPosition">시작 위치.</param>
    /// <param name="baseColor">기본 색상.</param>
    /// <returns>코루틴 열거자.</returns>
    private IEnumerator CoAnimateTickPopup(
        TMP_Text popupText,
        RectTransform popupRect,
        Vector2 startPosition,
        Color baseColor)
    {
        if (popupText == null || popupRect == null)
            yield break;

        float elapsedTime = 0f;

        while (elapsedTime < tickPopupLifetime)
        {
            if (popupText == null || popupRect == null)
                yield break;

            elapsedTime += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / tickPopupLifetime);

            popupRect.anchoredPosition = startPosition + new Vector2(0f, tickPopupMoveY * normalizedTime);
            popupRect.localScale = Vector3.one * Mathf.Lerp(tickPopupStartScale, tickPopupEndScale, normalizedTime);

            Color currentColor = baseColor;
            currentColor.a = Mathf.Lerp(1f, 0f, normalizedTime);
            popupText.color = currentColor;

            yield return null;
        }

        if (popupText != null)
        {
            Destroy(popupText.gameObject);
        }
    }

    /// <summary>
    /// GameObject가 null이 아닐 때만 SetActive를 적용한다.
    /// 인스펙터에서 일부 오브젝트를 비워둬도 안전하게 동작시키기 위한 함수다.
    /// </summary>
    /// <param name="targetObject">활성화 / 비활성화할 대상 오브젝트.</param>
    /// <param name="isActive">활성화 여부.</param>
    private void SetActiveIfNotNull(GameObject targetObject, bool isActive)
    {
        if (targetObject == null)
            return;

        targetObject.SetActive(isActive);
    }
}