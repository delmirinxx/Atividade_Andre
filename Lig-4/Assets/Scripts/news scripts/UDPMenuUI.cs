using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class UDPMenuUI : MonoBehaviour
{
    public UDPNetworkManager network;

    public TMP_InputField ipInput;

    public TMP_Text statusText;

    public Button serverButton;

    public Button clientButton;

    void Start()
    {
        statusText.text =
            "Escolha como deseja jogar.";

        serverButton.interactable =
            true;

        clientButton.interactable =
            true;
    }

    public void CreateServer()
    {
        network.StartServer();

        serverButton.interactable =
            false;

        clientButton.interactable =
            false;

        statusText.text =
            "SERVIDOR CRIADO!\n" +
            "Aguardando jogadores...";
    }

    public void Connect()
    {
        string ip =
            ipInput.text.Trim();

        if (string.IsNullOrEmpty(ip))
        {
            statusText.text =
                "Digite o IP do servidor.";

            return;
        }

        network.serverIP =
            ip;

        network.StartClient();

        serverButton.interactable =
            false;

        clientButton.interactable =
            false;

        statusText.text =
            "Conectando ao servidor...";
    }

    void Update()
    {
        if (network == null)
            return;

        if (network.mode ==
            UDPNetworkManager.NetworkMode.Server)
        {
            statusText.text =
                "SERVIDOR\n" +
                "Jogadores: " +
                network.connectedPlayers +
                "/4";
        }

        if (network.mode ==
            UDPNetworkManager.NetworkMode.Client)
        {
            if (network.playerID >= 0)
            {
                statusText.text =
                    "Conectado!\n" +
                    "Você é o Player " +
                    (network.playerID + 1);
            }
        }

        if (
            network.mode ==
            UDPNetworkManager.NetworkMode.Server &&
            network.connectedPlayers >= 4)
        {
            SceneManager.LoadScene(
                "Pong");
        }

        if (
            network.mode ==
            UDPNetworkManager.NetworkMode.Client &&
            network.playerID >= 0)
        {
            SceneManager.LoadScene(
                "Pong");
        }
    }
}