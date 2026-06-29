using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Unity.Multiplayer.Center.NetcodeForGameObjectsExample
{
    public class AsymmetricLobbyUI : NetworkBehaviour
    {
        [Header("Stage 1: Connection")]
        [SerializeField] private GameObject m_ConnectionPanel;
        [SerializeField] private Button m_StartHostButton;
        [SerializeField] private Button m_StartClientButton;

        [Header("Stage 2: Role Pick")]
        [SerializeField] private GameObject m_RolePanel;
        [SerializeField] private Button m_ChiefButton;
        [SerializeField] private Button m_CatButton;

        [Header("Character Prefabs")]
        [SerializeField] private GameObject m_ChiefPrefab;
        [SerializeField] private GameObject m_CatPrefab;

        void Awake()
        {
            if (!FindAnyObjectByType<EventSystem>())
            {
                System.Type inputType = typeof(StandaloneInputModule);
#if ENABLE_INPUT_SYSTEM
                inputType = typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule);                
#endif
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), inputType);
                eventSystem.transform.SetParent(transform);
            }
        }
        
        void Start()
        {
            // Set default view
            m_ConnectionPanel.SetActive(true);
            m_RolePanel.SetActive(false);

            m_StartHostButton.onClick.AddListener(StartHost);
            m_StartClientButton.onClick.AddListener(StartClient);

            m_ChiefButton.onClick.AddListener(() => SelectRole(isChief: true));
            m_CatButton.onClick.AddListener(() => SelectRole(isChief: false));

            // Listen for network connection events
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            }
        }

        public override void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            }
            base.OnDestroy();
        }

        private void StartHost()
        {
            NetworkManager.Singleton.StartHost();
            Debug.unityLogger.Log("Started host");
            // Hosts connect instantly to themselves, so trigger Stage 2 immediately
            SwitchToRoleSelect();
        }

        private void StartClient()
        {
            m_StartHostButton.interactable = false;
            m_StartClientButton.interactable = false;
            Debug.unityLogger.Log("Started client");
            NetworkManager.Singleton.StartClient();
            // DO NOT switch panels here! We have to wait for the network handshake.
        }

        private void OnClientConnected(ulong clientId)
        {
            // When ANY client connects, check: "Is the person who just connected ME?"
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                SwitchToRoleSelect();
            }
        }

        private void SwitchToRoleSelect()
        {
            m_ConnectionPanel.SetActive(false);
            m_RolePanel.SetActive(true);
        }

        private void SelectRole(bool isChief)
        {
            m_RolePanel.SetActive(false); // Hide UI totally

            // Tell the server what we picked
            SpawnPlayerServerRpc(NetworkManager.Singleton.LocalClientId, isChief);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SpawnPlayerServerRpc(ulong clientId, bool isChief)
        {
            GameObject prefabToSpawn = isChief ? m_ChiefPrefab : m_CatPrefab;

            GameObject instance = Instantiate(prefabToSpawn);
            NetworkObject netObj = instance.GetComponent<NetworkObject>();

            // This officially binds the spawned Chief/Cat to that player's network connection
            netObj.SpawnAsPlayerObject(clientId, true);
        }
    }
}