using UnityEngine;
using System.Globalization;

public class Pong4GameManager : MonoBehaviour
{
    public static Pong4GameManager Instance;

    [Header("Rede")]
    public UDPNetworkManager network;

    [Header("Players")]
    public Transform player1;
    public Transform player2;
    public Transform player3;
    public Transform player4;

    [Header("Bola")]
    public Transform ball;

    [Header("Configuração")]
    public float playerSpeed = 6f;

    public float ballSpeed = 5f;

    public float playerLimit = 4f;

    [Header("Placar")]
    public int teamAScore = 0;

    public int teamBScore = 0;

    private float[] playerInputs =
        new float[4];

    private Vector2 ballDirection;

    private bool gameStarted = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        network =
            UDPNetworkManager.Instance;

        if (
            network.mode ==
            UDPNetworkManager.NetworkMode.Server)
        {
            ResetBall();
        }
    }

    void Update()
    {
        if (network == null)
            return;

        if (
            network.mode ==
            UDPNetworkManager.NetworkMode.Server)
        {
            ServerUpdate();
        }
        else
        {
            ClientUpdate();
        }
    }

    // =====================================================
    // SERVIDOR
    // =====================================================

    void ServerUpdate()
    {
        if (
            network.connectedPlayers <
            4)
        {
            return;
        }

        gameStarted = true;

        // Player 1
        MovePlayer(
            player1,
            playerInputs[0]);

        // Player 2
        MovePlayer(
            player2,
            playerInputs[1]);

        // Player 3
        MovePlayer(
            player3,
            playerInputs[2]);

        // Player 4
        MovePlayer(
            player4,
            playerInputs[3]);

        MoveBall();

        SendGameState();
    }

    // =====================================================
    // CLIENTE
    // =====================================================

    void ClientUpdate()
    {
        int id =
            network.playerID;

        if (id < 0 || id > 3)
            return;

        float input = 0f;

        if (Input.GetKey(KeyCode.W) ||
            Input.GetKey(KeyCode.UpArrow))
        {
            input = 1f;
        }

        if (Input.GetKey(KeyCode.S) ||
            Input.GetKey(KeyCode.DownArrow))
        {
            input = -1f;
        }

        string message =
            "INPUT|" +
            id +
            "|" +
            input.ToString(
                CultureInfo.InvariantCulture);

        network.SendMessageToServer(
            message);
    }

    // =====================================================
    // RECEBER INPUT
    // =====================================================

    public void ReceivePlayerInput(
        int playerID,
        string message)
    {
        string[] parts =
            message.Split('|');

        if (parts.Length < 3)
            return;

        int id;

        float input;

        if (!int.TryParse(
            parts[1],
            out id))
        {
            return;
        }

        if (!float.TryParse(
            parts[2],
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out input))
        {
            return;
        }

        if (id < 0 || id > 3)
            return;

        playerInputs[id] =
            Mathf.Clamp(
                input,
                -1f,
                1f);
    }

    // =====================================================
    // MOVER PLAYER
    // =====================================================

    void MovePlayer(
        Transform player,
        float input)
    {
        Vector3 position =
            player.position;

        position.y +=
            input *
            playerSpeed *
            Time.deltaTime;

        position.y =
            Mathf.Clamp(
                position.y,
                -playerLimit,
                playerLimit);

        player.position =
            position;
    }

    // =====================================================
    // BOLA
    // =====================================================

    void MoveBall()
    {
        Vector3 position =
            ball.position;

        position.x +=
            ballDirection.x *
            ballSpeed *
            Time.deltaTime;

        position.y +=
            ballDirection.y *
            ballSpeed *
            Time.deltaTime;

        ball.position =
            position;

        // Paredes
        if (
            ball.position.y >= 4.5f)
        {
            ballDirection.y =
                -Mathf.Abs(
                    ballDirection.y);
        }

        if (
            ball.position.y <= -4.5f)
        {
            ballDirection.y =
                Mathf.Abs(
                    ballDirection.y);
        }

        // Players
        CheckCollision(
            player1,
            true);

        CheckCollision(
            player2,
            true);

        CheckCollision(
            player3,
            false);

        CheckCollision(
            player4,
            false);

        // Gol Time B
        if (
            ball.position.x >
            8.5f)
        {
            teamAScore++;

            ResetBall();
        }

        // Gol Time A
        if (
            ball.position.x <
            -8.5f)
        {
            teamBScore++;

            ResetBall();
        }
    }

    // =====================================================
    // COLISÃO
    // =====================================================

    void CheckCollision(
        Transform player,
        bool leftSide)
    {
        float dx =
            Mathf.Abs(
                ball.position.x -
                player.position.x);

        float dy =
            Mathf.Abs(
                ball.position.y -
                player.position.y);

        if (
            dx < 0.5f &&
            dy < 1.2f)
        {
            if (leftSide)
            {
                ballDirection.x =
                    Mathf.Abs(
                        ballDirection.x);
            }
            else
            {
                ballDirection.x =
                    -Mathf.Abs(
                        ballDirection.x);
            }

            ballDirection.y =
                Mathf.Clamp(
                    ball.position.y -
                    player.position.y,
                    -1f,
                    1f);

            ballDirection.Normalize();
        }
    }

    // =====================================================
    // RESET
    // =====================================================

    void ResetBall()
    {
        ball.position =
            Vector3.zero;

        float y =
            Random.Range(
                -0.8f,
                0.8f);

        float x =
            Random.value > 0.5f
            ? 1f
            : -1f;

        ballDirection =
            new Vector2(
                x,
                y).normalized;
    }

    // =====================================================
    // ENVIAR ESTADO
    // =====================================================

    void SendGameState()
    {
        string message =
            "STATE|" +

            player1.position.y + "|" +
            player2.position.y + "|" +
            player3.position.y + "|" +
            player4.position.y + "|" +

            ball.position.x + "|" +
            ball.position.y + "|" +

            teamAScore + "|" +
            teamBScore;

        network.SendToAll(
            message);
    }

    // =====================================================
    // CLIENTE RECEBE ESTADO
    // =====================================================

    public void ReceiveGameState(
        string message)
    {
        string[] parts =
            message.Split('|');

        if (parts.Length < 9)
            return;

        float p1 =
            float.Parse(
                parts[1],
                CultureInfo.InvariantCulture);

        float p2 =
            float.Parse(
                parts[2],
                CultureInfo.InvariantCulture);

        float p3 =
            float.Parse(
                parts[3],
                CultureInfo.InvariantCulture);

        float p4 =
            float.Parse(
                parts[4],
                CultureInfo.InvariantCulture);

        float bx =
            float.Parse(
                parts[5],
                CultureInfo.InvariantCulture);

        float by =
            float.Parse(
                parts[6],
                CultureInfo.InvariantCulture);

        teamAScore =
            int.Parse(parts[7]);

        teamBScore =
            int.Parse(parts[8]);

        SetPosition(
            player1,
            p1);

        SetPosition(
            player2,
            p2);

        SetPosition(
            player3,
            p3);

        SetPosition(
            player4,
            p4);

        ball.position =
            new Vector3(
                bx,
                by,
                0);
    }

    void SetPosition(
        Transform player,
        float y)
    {
        Vector3 position =
            player.position;

        position.y =
            Mathf.Lerp(
                position.y,
                y,
                15f *
                Time.deltaTime);

        player.position =
            position;
    }
}