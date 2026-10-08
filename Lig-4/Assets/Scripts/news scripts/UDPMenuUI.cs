using TMPro;
using UnityEngine;

public class UDPMenuUI : MonoBehaviour
{
    [Header("Campo de IP")]
    [SerializeField] private TMP_InputField ipInput;

    [Header("Texto de status")]
    [SerializeField] private TMP_Text statusText;

    private UDPNetworkManager network;

    private void Start()
    {
        network = UDPNetworkManager.Instance;

        if (network == null)
        {
            Debug.LogError(
                "UDPNetworkManager não encontrado!"
            );

            return;
        }

        if (statusText != null)
        {
            statusText.text =
                "Escolha como deseja jogar.";
        }
    }

    // =========================================================
    // BOTÃO CRIAR SERVIDOR
    // =========================================================

    public void CriarServidor()
    {
        if (network == null)
            return;

        network.StartServer();

        if (statusText != null)
        {
            statusText.text =
                "Servidor criado!\n" +
                "Aguardando outro computador...";
        }

        Debug.Log("Servidor criado.");
    }

    // =========================================================
    // BOTÃO ENTRAR NO JOGO
    // =========================================================

    public void EntrarNoJogo()
    {
        if (network == null)
            return;

        string ip = "";

        if (ipInput != null)
        {
            ip = ipInput.text.Trim();
        }

        if (string.IsNullOrEmpty(ip))
        {
            if (statusText != null)
                statusText.text =
                    "Digite o IP do servidor.";

            return;
        }

        if (statusText != null)
        {
            statusText.text =
                "Conectando ao servidor...";
        }

        network.StartClient(ip);
    }

    // =========================================================
    // STATUS
    // =========================================================

    private void Update()
    {
        if (network == null)
            return;

        if (network.IsServer &&
            !network.IsConnected)
        {
            if (statusText != null)
            {
                statusText.text =
                    "Servidor ativo.\n" +
                    "Aguardando o outro computador...";
            }
        }

        if (network.IsServer &&
            network.IsConnected)
        {
            if (statusText != null)
            {
                statusText.text =
                    "Cliente conectado!\n" +
                    "Iniciando partida...";
            }
        }
    }
}