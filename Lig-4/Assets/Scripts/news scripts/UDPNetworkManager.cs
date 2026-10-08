using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UDPNetworkManager : MonoBehaviour
{
    public static UDPNetworkManager Instance { get; private set; }

    [Header("Rede")]
    [SerializeField] private int port = 7777;

    [Header("Cena do jogo")]
    [SerializeField] private string pongSceneName = "Pong";

    public bool IsServer { get; private set; }
    public bool IsClient { get; private set; }
    public bool IsConnected { get; private set; }

    public string PlayerRole { get; private set; } = "";

    public int Port => port;

    private UdpClient udp;
    private Thread receiveThread;
    private volatile bool running;

    private IPEndPoint clientEndpoint;
    private IPEndPoint serverEndpoint;

    private readonly ConcurrentQueue<string> receivedMessages =
        new ConcurrentQueue<string>();

    private readonly ConcurrentQueue<IPEndPoint> receivedEndpoints =
        new ConcurrentQueue<IPEndPoint>();

    public event Action<string> OnGameStateReceived;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        ProcessReceivedMessages();
    }

    // =========================================================
    // SERVIDOR
    // =========================================================

    public void StartServer()
    {
        StopNetwork();

        try
        {
            IsServer = true;
            IsClient = false;
            IsConnected = false;

            PlayerRole = "P1 + P3";

            udp = new UdpClient(port);

            running = true;

            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            Debug.Log("Servidor UDP iniciado na porta " + port);
        }
        catch (Exception e)
        {
            Debug.LogError("Erro ao iniciar servidor: " + e.Message);
        }
    }

    // =========================================================
    // CLIENTE
    // =========================================================

    public void StartClient(string serverIP)
    {
        StopNetwork();

        if (string.IsNullOrWhiteSpace(serverIP))
        {
            Debug.LogError("Digite o IP do servidor.");
            return;
        }

        try
        {
            IsServer = false;
            IsClient = true;
            IsConnected = false;

            PlayerRole = "P2 + P4";

            udp = new UdpClient();

            serverEndpoint = new IPEndPoint(
                IPAddress.Parse(serverIP),
                port
            );

            udp.Connect(serverEndpoint);

            running = true;

            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            SendToServer("CONNECT");

            Debug.Log("Tentando conectar ao servidor: " + serverIP);
        }
        catch (Exception e)
        {
            Debug.LogError("Erro ao conectar: " + e.Message);
        }
    }

    // =========================================================
    // RECEBIMENTO UDP
    // =========================================================

    private void ReceiveLoop()
    {
        while (running)
        {
            try
            {
                IPEndPoint endpoint = new IPEndPoint(
                    IPAddress.Any,
                    0
                );

                byte[] data = udp.Receive(ref endpoint);

                string message = Encoding.UTF8.GetString(data);

                receivedEndpoints.Enqueue(endpoint);
                receivedMessages.Enqueue(message);
            }
            catch (SocketException)
            {
                if (!running)
                    break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception e)
            {
                if (running)
                    Debug.LogError("Erro no recebimento UDP: " + e.Message);
            }
        }
    }

    // =========================================================
    // PROCESSAMENTO DAS MENSAGENS
    // =========================================================

    private void ProcessReceivedMessages()
    {
        while (receivedMessages.TryDequeue(out string message))
        {
            receivedEndpoints.TryDequeue(out IPEndPoint endpoint);

            if (string.IsNullOrEmpty(message))
                continue;

            // -------------------------------------------------
            // SERVIDOR
            // -------------------------------------------------

            if (IsServer)
            {
                if (message == "CONNECT")
                {
                    HandleClientConnection(endpoint);
                    continue;
                }

                if (message.StartsWith("INPUT|"))
                {
                    if (clientEndpoint != null &&
                        endpoint.Address.Equals(clientEndpoint.Address) &&
                        endpoint.Port == clientEndpoint.Port)
                    {
                        OnClientInputReceived(message);
                    }

                    continue;
                }
            }

            // -------------------------------------------------
            // CLIENTE
            // -------------------------------------------------

            if (IsClient)
            {
                if (message == "START")
                {
                    IsConnected = true;

                    Debug.Log("Servidor iniciou a partida.");

                    LoadPongScene();
                    continue;
                }

                if (message.StartsWith("STATE|"))
                {
                    OnGameStateReceived?.Invoke(message);
                    continue;
                }
            }
        }
    }

    // =========================================================
    // CONEXÃO DO CLIENTE
    // =========================================================

    private void HandleClientConnection(IPEndPoint endpoint)
    {
        if (clientEndpoint == null)
        {
            clientEndpoint = new IPEndPoint(
                endpoint.Address,
                endpoint.Port
            );

            IsConnected = true;

            Debug.Log(
                "Cliente conectado: " +
                clientEndpoint.Address +
                ":" +
                clientEndpoint.Port
            );

            SendToClient("ROLE|P2|P4");

            // Dá um pequeno tempo para o cliente receber a função
            // e depois inicia o jogo.
            SendToClient("START");

            LoadPongScene();
        }
        else
        {
            // Já existe um cliente conectado.
            SendToEndpoint(endpoint, "FULL");
        }
    }

    // =========================================================
    // ENVIO
    // =========================================================

    public void SendToServer(string message)
    {
        if (!IsClient || udp == null)
            return;

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);

            udp.Send(
                data,
                data.Length
            );
        }
        catch (Exception e)
        {
            Debug.LogError("Erro ao enviar para servidor: " + e.Message);
        }
    }

    public void SendToClient(string message)
    {
        if (!IsServer || udp == null || clientEndpoint == null)
            return;

        SendToEndpoint(clientEndpoint, message);
    }

    private void SendToEndpoint(
        IPEndPoint endpoint,
        string message)
    {
        if (udp == null || endpoint == null)
            return;

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);

            udp.Send(
                data,
                data.Length,
                endpoint
            );
        }
        catch (Exception e)
        {
            Debug.LogError("Erro ao enviar UDP: " + e.Message);
        }
    }

    // =========================================================
    // INPUT DO CLIENTE
    // =========================================================

    private void OnClientInputReceived(string message)
    {
        // O Pong4GameManager recebe os inputs através deste método.
        Pong4GameManager game =
            FindFirstObjectByType<Pong4GameManager>();

        if (game != null)
        {
            game.ReceiveNetworkInput(message);
        }
    }

    // =========================================================
    // CARREGAR PONG
    // =========================================================

    private void LoadPongScene()
    {
        if (SceneManager.GetActiveScene().name != pongSceneName)
        {
            SceneManager.LoadScene(pongSceneName);
        }
    }

    // =========================================================
    // PARAR REDE
    // =========================================================

    public void StopNetwork()
    {
        running = false;

        try
        {
            if (udp != null)
            {
                udp.Close();
                udp = null;
            }
        }
        catch
        {
        }

        try
        {
            if (receiveThread != null &&
                receiveThread.IsAlive)
            {
                receiveThread.Join(100);
            }
        }
        catch
        {
        }

        receiveThread = null;

        clientEndpoint = null;
        serverEndpoint = null;

        IsServer = false;
        IsClient = false;
        IsConnected = false;

        PlayerRole = "";
    }

    private void OnApplicationQuit()
    {
        StopNetwork();
    }
}