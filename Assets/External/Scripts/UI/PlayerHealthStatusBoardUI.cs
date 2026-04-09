using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

/// <summary>
/// 여러 플레이어의 체력 상태 엔트리 UI를 관리하는 보드 스크립트.
/// 
/// 이 스크립트의 역할:
/// 1. 씬에 존재하는 PlayerHealth를 찾아 엔트리 UI 생성
/// 2. PlayerHealth 등록/해제 이벤트에 따라 엔트리 추가/삭제
/// 3. PlayerHealth.HealthChanged 이벤트에 따라 각 엔트리 갱신
/// 4. Photon 닉네임 변경 시 텍스트 재갱신
/// 5. ActorNumber 기준으로 표시 순서 정렬
/// 6. 표시 순서에 따라 Player1~Player4 테마 index를 각 엔트리에 분배
/// </summary>
public class PlayerHealthStatusBoardUI : MonoBehaviourPunCallbacks
{
    /// <summary>
    /// PlayerHealth와 생성된 EntryUI를 1:1로 묶어서 관리하는 내부 클래스.
    /// </summary>
    private sealed class EntryBinding
    {
        /// <summary>
        /// 실제 체력 데이터를 들고 있는 PlayerHealth 컴포넌트.
        /// </summary>
        public PlayerHealth Health;

        /// <summary>
        /// 화면에 표시되는 엔트리 UI 인스턴스.
        /// </summary>
        public PlayerHealthStatusEntryUI EntryUI;
    }

    [Header("References")]

    /// <summary>
    /// 엔트리 프리팹들이 생성될 부모 RectTransform.
    /// Grid Layout Group 또는 Horizontal/Vertical Layout Group 연결 권장.
    /// </summary>
    [SerializeField] private RectTransform contentRoot;

    /// <summary>
    /// 한 줄짜리 플레이어 체력 엔트리 UI 프리팹.
    /// </summary>
    [SerializeField] private PlayerHealthStatusEntryUI entryPrefab;

    /// <summary>
    /// 비활성화된 PlayerHealth 오브젝트도 검색에 포함할지 여부.
    /// </summary>
    [SerializeField] private bool includeInactiveHealthObjects = false;

    [Header("Fallback UI")]

    /// <summary>
    /// 플레이어가 하나도 없을 때 보여줄 텍스트.
    /// </summary>
    [SerializeField] private TMP_Text emptyStateText;

    /// <summary>
    /// 플레이어가 하나도 없을 때 표시할 메시지.
    /// </summary>
    [SerializeField] private string emptyStateMessage = "No Players";

    /// <summary>
    /// PlayerHealth를 키로 하여 바인딩 정보를 저장하는 딕셔너리.
    /// </summary>
    private readonly Dictionary<PlayerHealth, EntryBinding> bindingsByHealth = new();

    /// <summary>
    /// 현재 표시 순서를 계산하기 위한 정렬 버퍼.
    /// </summary>
    private readonly List<PlayerHealth> sortedHealthBuffer = new();

    /// <summary>
    /// 컴포넌트가 에디터에서 추가되었을 때 기본 contentRoot를 자동 연결한다.
    /// </summary>
    private void Reset()
    {
        if (contentRoot == null)
            contentRoot = transform as RectTransform;
    }

    /// <summary>
    /// 런타임 시작 시 contentRoot가 비어 있으면 자기 자신의 RectTransform을 사용한다.
    /// </summary>
    private void Awake()
    {
        if (contentRoot == null)
            contentRoot = transform as RectTransform;
    }

    /// <summary>
    /// 활성화 시 PlayerHealth 등록/해제 이벤트를 구독하고
    /// 현재 씬 상태 기준으로 전체 엔트리를 다시 생성한다.
    /// </summary>
    public override void OnEnable()
    {
        base.OnEnable();

        PlayerHealth.Registered += HandlePlayerHealthRegistered;
        PlayerHealth.Unregistered += HandlePlayerHealthUnregistered;

        RebuildAllEntries();
    }

    /// <summary>
    /// 비활성화 시 이벤트 구독을 해제하고 생성된 엔트리를 정리한다.
    /// </summary>
    public override void OnDisable()
    {
        PlayerHealth.Registered -= HandlePlayerHealthRegistered;
        PlayerHealth.Unregistered -= HandlePlayerHealthUnregistered;

        UnbindAllEntries();
        base.OnDisable();
    }

    /// <summary>
    /// Photon 룸에 새 플레이어가 들어오면 정렬 순서를 다시 반영한다.
    /// </summary>
    /// <param name="newPlayer">새로 들어온 플레이어.</param>
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshDisplayOrder();
    }

    /// <summary>
    /// Photon 룸에서 플레이어가 나가면 정렬 순서를 다시 반영한다.
    /// </summary>
    /// <param name="otherPlayer">나간 플레이어.</param>
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshDisplayOrder();
    }

    /// <summary>
    /// Photon 플레이어 프로퍼티가 바뀌었을 때 닉네임 같은 표시 텍스트를 다시 갱신한다.
    /// </summary>
    /// <param name="targetPlayer">프로퍼티가 변경된 플레이어.</param>
    /// <param name="changedProps">변경된 프로퍼티 집합.</param>
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        RefreshAllEntryTexts();
    }

    /// <summary>
    /// PlayerHealth가 새로 등록되었을 때 엔트리를 만들고 순서를 갱신한다.
    /// </summary>
    /// <param name="playerHealth">등록된 PlayerHealth.</param>
    private void HandlePlayerHealthRegistered(PlayerHealth playerHealth)
    {
        if (playerHealth == null)
            return;

        EnsureEntryBound(playerHealth);
        RefreshDisplayOrder();
    }

    /// <summary>
    /// PlayerHealth가 해제되었을 때 엔트리를 제거하고 순서를 갱신한다.
    /// </summary>
    /// <param name="playerHealth">해제된 PlayerHealth.</param>
    private void HandlePlayerHealthUnregistered(PlayerHealth playerHealth)
    {
        if (playerHealth == null)
            return;

        UnbindEntry(playerHealth);
        RefreshDisplayOrder();
    }

    /// <summary>
    /// 현재 씬의 PlayerHealth들을 다시 찾아 전체 엔트리를 재구성한다.
    /// </summary>
    private void RebuildAllEntries()
    {
        UnbindAllEntries();

        PlayerHealth[] healthObjects = FindObjectsByType<PlayerHealth>(
            includeInactiveHealthObjects ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < healthObjects.Length; i++)
        {
            EnsureEntryBound(healthObjects[i]);
        }

        RefreshDisplayOrder();
    }

    /// <summary>
    /// 지정한 PlayerHealth에 대응하는 엔트리가 없으면 새로 생성하고 이벤트를 연결한다.
    /// </summary>
    /// <param name="playerHealth">바인딩할 PlayerHealth.</param>
    private void EnsureEntryBound(PlayerHealth playerHealth)
    {
        if (playerHealth == null || bindingsByHealth.ContainsKey(playerHealth))
            return;

        if (contentRoot == null || entryPrefab == null)
            return;

        PlayerHealthStatusEntryUI entryUI = Instantiate(entryPrefab, contentRoot);
        entryUI.name = $"{entryPrefab.name}_{ResolvePlayerName(playerHealth)}";

        EntryBinding binding = new EntryBinding
        {
            Health = playerHealth,
            EntryUI = entryUI
        };

        bindingsByHealth.Add(playerHealth, binding);
        playerHealth.HealthChanged += HandleHealthChanged;

        RefreshEntry(binding);
    }

    /// <summary>
    /// 특정 PlayerHealth와 연결된 엔트리를 해제하고 삭제한다.
    /// </summary>
    /// <param name="playerHealth">해제할 PlayerHealth.</param>
    private void UnbindEntry(PlayerHealth playerHealth)
    {
        if (playerHealth == null)
            return;

        if (!bindingsByHealth.TryGetValue(playerHealth, out EntryBinding binding))
            return;

        playerHealth.HealthChanged -= HandleHealthChanged;
        bindingsByHealth.Remove(playerHealth);

        if (binding.EntryUI != null)
            Destroy(binding.EntryUI.gameObject);
    }

    /// <summary>
    /// 모든 엔트리와 이벤트 바인딩을 정리한다.
    /// </summary>
    private void UnbindAllEntries()
    {
        foreach (KeyValuePair<PlayerHealth, EntryBinding> pair in bindingsByHealth)
        {
            if (pair.Key != null)
            {
                pair.Key.HealthChanged -= HandleHealthChanged;
            }

            if (pair.Value != null && pair.Value.EntryUI != null)
            {
                Destroy(pair.Value.EntryUI.gameObject);
            }
        }

        bindingsByHealth.Clear();
    }

    /// <summary>
    /// 어떤 플레이어의 체력이 바뀌었을 때 해당 엔트리 UI를 다시 갱신한다.
    /// </summary>
    /// <param name="playerHealth">체력이 바뀐 PlayerHealth.</param>
    /// <param name="args">체력 변경 이벤트 인자.</param>
    private void HandleHealthChanged(PlayerHealth playerHealth, HealthChangedEventArgs args)
    {
        if (playerHealth == null)
            return;

        if (!bindingsByHealth.TryGetValue(playerHealth, out EntryBinding binding))
            return;

        RefreshEntry(binding);
    }

    /// <summary>
    /// 바인딩된 엔트리 하나의 이름/체력을 최신 값으로 갱신한다.
    /// </summary>
    /// <param name="binding">갱신할 바인딩 정보.</param>
    private void RefreshEntry(EntryBinding binding)
    {
        if (binding == null || binding.Health == null || binding.EntryUI == null)
            return;

        binding.EntryUI.SetData(
            ResolvePlayerName(binding.Health),
            binding.Health.CurrentHealth);
    }

    /// <summary>
    /// 모든 엔트리의 텍스트를 다시 갱신한다.
    /// 닉네임 변경 같은 경우에 사용된다.
    /// </summary>
    private void RefreshAllEntryTexts()
    {
        foreach (KeyValuePair<PlayerHealth, EntryBinding> pair in bindingsByHealth)
        {
            RefreshEntry(pair.Value);
        }
    }

    /// <summary>
    /// 현재 엔트리들을 정렬 기준에 맞게 다시 배치한다.
    /// 
    /// 그리고 여기서 각 엔트리에 display index를 넘겨
    /// Player1~Player4 테마 이미지를 분배한다.
    /// </summary>
    private void RefreshDisplayOrder()
    {
        sortedHealthBuffer.Clear();

        foreach (KeyValuePair<PlayerHealth, EntryBinding> pair in bindingsByHealth)
        {
            if (pair.Key != null)
            {
                sortedHealthBuffer.Add(pair.Key);
            }
        }

        sortedHealthBuffer.Sort(CompareHealthEntries);

        for (int i = 0; i < sortedHealthBuffer.Count; i++)
        {
            PlayerHealth playerHealth = sortedHealthBuffer[i];
            if (!bindingsByHealth.TryGetValue(playerHealth, out EntryBinding binding))
                continue;

            if (binding.EntryUI != null)
            {
                binding.EntryUI.transform.SetSiblingIndex(i);

                // 표시 순서에 맞춰 Player1~Player4 테마를 분배한다.
                binding.EntryUI.SetPlayerThemeByDisplayIndex(i);
            }

            RefreshEntry(binding);
        }

        if (emptyStateText != null)
        {
            bool hasEntries = sortedHealthBuffer.Count > 0;
            emptyStateText.gameObject.SetActive(!hasEntries);

            if (!hasEntries)
            {
                emptyStateText.text = emptyStateMessage;
            }
        }
    }

    /// <summary>
    /// 플레이어 엔트리의 정렬 기준.
    /// 1. OwnerActorNumber 오름차순
    /// 2. 닉네임 사전순
    /// </summary>
    /// <param name="left">왼쪽 비교 대상.</param>
    /// <param name="right">오른쪽 비교 대상.</param>
    /// <returns>정렬 비교 결과.</returns>
    private static int CompareHealthEntries(PlayerHealth left, PlayerHealth right)
    {
        if (left == right)
            return 0;

        if (left == null)
            return 1;

        if (right == null)
            return -1;

        int actorCompare = left.OwnerActorNumber.CompareTo(right.OwnerActorNumber);
        if (actorCompare != 0)
            return actorCompare;

        return string.CompareOrdinal(ResolvePlayerName(left), ResolvePlayerName(right));
    }

    /// <summary>
    /// PlayerHealth로부터 화면에 표시할 플레이어 이름을 구한다.
    /// 
    /// 우선순위:
    /// 1. PhotonView.Owner.NickName
    /// 2. playerHealth 오브젝트 이름
    /// </summary>
    /// <param name="playerHealth">이름을 확인할 PlayerHealth.</param>
    /// <returns>표시할 플레이어 이름.</returns>
    private static string ResolvePlayerName(PlayerHealth playerHealth)
    {
        if (playerHealth == null)
            return string.Empty;

        PhotonView photonView = playerHealth.GetComponent<PhotonView>();
        if (photonView == null)
            photonView = playerHealth.GetComponentInParent<PhotonView>();

        if (photonView != null && photonView.Owner != null)
            return photonView.Owner.NickName;

        return playerHealth.name;
    }
}