using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class UDPNetworkManager : MonoBehaviour
{
    public enum NetworkMode
    {
        None,
        Server,
        Client
    }

    [Header("Configuração")]
    public NetworkMode mode = NetworkMode.None;

    public int port = 7777;

    public string serverIP = "127.0.0.1";

    [Header("Estado")]
    public bool connected = false;

    public int playerID = -1;

    public int connectedPlayers = 0;

    // =====================================================
    // UDP
    // =====================================================

    private UdpClient udp;

    private Thread receiveThread;

    private bool running = false;

    // =====================================================
    // SERVIDOR
    // =====================================================

    private Dictionary<int, IPEndPoint> players =
        new Dictionary<int, IPEndPoint>();

    // =====================================================
    // MENSAGENS
    // =====================================================

    private Queue<UDPMessage> receivedMessages =
        new Queue<UDPMessage>();

    private object messageLock =
        new object();

    // =====================================================
    // SINGLETON
    // =====================================================

    public static UDPNetworkManager Instance;

    void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        ProcessMessages();
    }

    // =====================================================
    // SERVIDOR
    // =====================================================

    public void StartServer()
    {
        if (running)
            return;

        mode =
            NetworkMode.Server;

        try
        {
            udp =
                new UdpClient(port);

            running = true;

            connected = true;

            receiveThread =
                new Thread(ReceiveLoop);

            receiveThread.IsBackground = true;

            receiveThread.Start();

            Debug.Log(
                "Servidor UDP iniciado na porta " +
                port);
        }
        catch (Exception e)
        {
            Debug.LogError(
                "Erro ao iniciar servidor: " +
                e.Message);
        }
    }

    // =====================================================
    // CLIENTE
    // =====================================================

    public void StartClient()
    {
        if (running)
            return;

        mode =
            NetworkMode.Client;

        try
        {
            udp =
                new UdpClient();

            running = true;

            connected = true;

            receiveThread =
                new Thread(ReceiveLoop);

            receiveThread.IsBackground = true;

            receiveThread.Start();

            SendMessageToServer(
                "CONNECT");

            Debug.Log(
                "Cliente UDP iniciado.");
        }
        catch (Exception e)
        {
            Debug.LogError(
                "Erro ao iniciar cliente: " +
                e.Message);
        }
    }

    // =====================================================
    // RECEBER
    // =====================================================

    private void ReceiveLoop()
    {
        IPEndPoint remote =
            new IPEndPoint(
                IPAddress.Any,
                0);

        while (running)
        {
            try
            {
                byte[] data =
                    udp.Receive(
                        ref remote);

                string message =
                    Encoding.UTF8.GetString(data);

                lock (messageLock)
                {
                    receivedMessages.Enqueue(
                        new UDPMessage
                        {
                            message = message,
                            sender = remote
                        });
                }
            }
            catch
            {
                if (!running)
                    break;
            }
        }
    }

    // =====================================================
    // PROCESSAR
    // =====================================================

    private void ProcessMessages()
    {
        lock (messageLock)
        {
            while (
                receivedMessages.Count > 0)
            {
                UDPMessage data =
                    receivedMessages.Dequeue();

                if (mode ==
                    NetworkMode.Server)
                {
                    ProcessServerMessage(
                        data.message,
                        data.sender);
                }
                else if (
                    mode ==
                    NetworkMode.Client)
                {
                    ProcessClientMessage(
                        data.message);
                }
            }
        }
    }

    // =====================================================
    // MENSAGENS DO SERVIDOR
    // =====================================================

    private void ProcessServerMessage(
        string message,
        IPEndPoint sender)
    {
        if (message == "CONNECT")
        {
            RegisterPlayer(sender);
            return;
        }

        if (message.StartsWith("INPUT|"))
        {
            if (Pong4GameManager.Instance != null)
            {
                Pong4GameManager.Instance
                    .ReceivePlayerInput(
                        GetPlayerID(sender),
                        message);
            }
        }
    }

    // =====================================================
    // REGISTRAR JOGADOR
    // =====================================================

    private void RegisterPlayer(
        IPEndPoint endpoint)
    {
        if (players.Count >= 4)
        {
            SendTo(
                endpoint,
                "FULL");

            return;
        }

        int id = -1;

        for (int i = 0; i < 4; i++)
        {
            if (!players.ContainsKey(i))
            {
                id = i;
                break;
            }
        }

        if (id == -1)
            return;

        players.Add(
            id,
            endpoint);

        connectedPlayers =
            players.Count;

        string message =
            "ID|" + id;

        SendTo(
            endpoint,
            message);

        Debug.Log(
            "Jogador " +
            id +
            " conectado.");
    }

    // =====================================================
    // IDENTIFICAR JOGADOR
    // =====================================================

    private int GetPlayerID(
        IPEndPoint endpoint)
    {
        foreach (
            var pair in players)
        {
            if (pair.Value.Address.Equals(
                    endpoint.Address) &&
                pair.Value.Port ==
                endpoint.Port)
            {
                return pair.Key;
            }
        }

        return -1;
    }

    // =====================================================
    // CLIENTE RECEBE
    // =====================================================

    private void ProcessClientMessage(
        string message)
    {
        if (message.StartsWith("ID|"))
        {
            string[] parts =
                message.Split('|');

            playerID =
                int.Parse(parts[1]);

            Debug.Log(
                "Sou o jogador " +
                playerID);

            return;
        }

        if (message == "FULL")
        {
            Debug.Log(
                "Servidor cheio.");

            return;
        }

        if (message.StartsWith("STATE|"))
        {
            if (Pong4GameManager.Instance != null)
            {
                Pong4GameManager.Instance
                    .ReceiveGameState(
                        message);
            }
        }
    }

    // =====================================================
    // ENVIAR PARA SERVIDOR
    // =====================================================

    public void SendMessageToServer(
        string message)
    {
        if (mode != NetworkMode.Client)
            return;

        try
        {
            IPEndPoint endpoint =
                new IPEndPoint(
                    IPAddress.Parse(serverIP),
                    port);

            SendTo(
                endpoint,
                message);
        }
        catch (Exception e)
        {
            Debug.LogError(
                e.Message);
        }
    }

    // =====================================================
    // ENVIAR PARA UM CLIENTE
    // =====================================================

    public void SendTo(
        IPEndPoint endpoint,
        string message)
    {
        byte[] data =
            Encoding.UTF8.GetBytes(
                message);

        udp.Send(
            data,
            data.Length,
            endpoint);
    }

    // =====================================================
    // ENVIAR PARA TODOS
    // =====================================================

    public void SendToAll(
        string message)
    {
        foreach (
            var player in players)
        {
            SendTo(
                player.Value,
                message);
        }
    }

    // =====================================================
    // PARAR
    // =====================================================

    public void StopNetwork()
    {
        running = false;

        try
        {
            udp?.Close();
        }
        catch
        {
        }
    }

    void OnApplicationQuit()
    {
        StopNetwork();
    }

    // =====================================================
    // ESTRUTURA
    // =====================================================

    private class UDPMessage
    {
        public string message;

        public IPEndPoint sender;
    }
}