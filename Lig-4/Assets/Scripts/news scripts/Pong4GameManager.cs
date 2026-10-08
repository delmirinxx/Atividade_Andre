using UnityEngine;
using UnityEngine.InputSystem;

public class Pong4GameManager : MonoBehaviour
{
    [Header("Jogadores")]
    [SerializeField] private Transform player1;
    [SerializeField] private Transform player2;
    [SerializeField] private Transform player3;
    [SerializeField] private Transform player4;

    [Header("Bola")]
    [SerializeField] private Transform ball;

    [Header("Configuração")]
    [SerializeField] private float paddleSpeed = 5f;
    [SerializeField] private float ballSpeed = 6f;
    [SerializeField] private float topLimit = 3.2f;
    [SerializeField] private float bottomLimit = -3.2f;

    [Header("Limites de pontuação")]
    [SerializeField] private float leftGoalX = -8.5f;
    [SerializeField] private float rightGoalX = 8.5f;

    [Header("Rede")]
    [SerializeField] private float networkSendRate = 30f;

    private UDPNetworkManager network;
    private Score4UI scoreUI;

    // =========================================================
    // POSIÇÕES DOS JOGADORES
    // =========================================================

    private float p1Y;
    private float p2Y;
    private float p3Y;
    private float p4Y;

    // =========================================================
    // INPUTS RECEBIDOS DO OUTRO PC
    // PC 2 controla P3 e P4
    // =========================================================

    private float p3NetworkInput;
    private float p4NetworkInput;

    // =========================================================
    // BOLA
    // =========================================================

    private Vector2 ballVelocity;

    // =========================================================
    // PLACAR
    // =========================================================

    private int scoreTeamA;
    private int scoreTeamB;

    // =========================================================
    // REDE
    // =========================================================

    private float networkTimer;
    private float inputSendTimer;

    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        network = UDPNetworkManager.Instance;

        scoreUI = FindFirstObjectByType<Score4UI>();

        if (network == null)
        {
            Debug.LogError(
                "UDPNetworkManager não encontrado."
            );

            return;
        }

        // Cliente recebe o estado do servidor
        if (network.IsClient)
        {
            network.OnGameStateReceived += ReceiveGameState;
        }

        // Servidor inicia a partida
        if (network.IsServer)
        {
            InitializeServerGame();
        }
    }

    // =========================================================
    // INICIALIZAÇÃO DO SERVIDOR
    // =========================================================

    private void InitializeServerGame()
    {
        scoreTeamA = 0;
        scoreTeamB = 0;

        p1Y = player1.position.y;
        p2Y = player2.position.y;
        p3Y = player3.position.y;
        p4Y = player4.position.y;

        ResetBall(1);

        UpdateScoreUI();
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (network == null)
            return;

        if (network.IsServer)
        {
            ServerUpdate();
        }
        else if (network.IsClient)
        {
            ClientUpdate();
        }
    }

    // =========================================================
    // SERVIDOR
    // PC 1 CONTROLA P1 + P2
    // =========================================================

    private void ServerUpdate()
    {
        ReadServerInputs();

        MoveServerPlayers();

        MoveBall();

        networkTimer += Time.deltaTime;

        if (networkTimer >= 1f / networkSendRate)
        {
            networkTimer = 0f;

            SendGameState();
        }
    }

    // =========================================================
    // INPUT DO PC 1
    //
    // P1 = W / S
    // P2 = SETA CIMA / BAIXO
    // =========================================================

    private void ReadServerInputs()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        // -----------------------------------------------------
        // PLAYER 1
        // W = sobe
        // S = desce
        // -----------------------------------------------------

        float p1Input = 0f;

        if (keyboard.wKey.isPressed)
            p1Input = 1f;

        if (keyboard.sKey.isPressed)
            p1Input = -1f;

        // -----------------------------------------------------
        // PLAYER 2
        // SETA CIMA = sobe
        // SETA BAIXO = desce
        // -----------------------------------------------------

        float p2Input = 0f;

        if (keyboard.upArrowKey.isPressed)
            p2Input = 1f;

        if (keyboard.downArrowKey.isPressed)
            p2Input = -1f;

        // -----------------------------------------------------
        // MOVIMENTA P1 E P2
        // -----------------------------------------------------

        p1Y +=
            p1Input *
            paddleSpeed *
            Time.deltaTime;

        p2Y +=
            p2Input *
            paddleSpeed *
            Time.deltaTime;
    }

    // =========================================================
    // MOVIMENTAÇÃO DOS 4 JOGADORES
    // =========================================================

    private void MoveServerPlayers()
    {
        // -----------------------------------------------------
        // P3 - controlado pelo PC 2
        // -----------------------------------------------------

        p3Y +=
            p3NetworkInput *
            paddleSpeed *
            Time.deltaTime;

        // -----------------------------------------------------
        // P4 - controlado pelo PC 2
        // -----------------------------------------------------

        p4Y +=
            p4NetworkInput *
            paddleSpeed *
            Time.deltaTime;

        // -----------------------------------------------------
        // LIMITES
        // -----------------------------------------------------

        p1Y = Mathf.Clamp(
            p1Y,
            bottomLimit,
            topLimit
        );

        p2Y = Mathf.Clamp(
            p2Y,
            bottomLimit,
            topLimit
        );

        p3Y = Mathf.Clamp(
            p3Y,
            bottomLimit,
            topLimit
        );

        p4Y = Mathf.Clamp(
            p4Y,
            bottomLimit,
            topLimit
        );

        // -----------------------------------------------------
        // APLICA POSIÇÕES
        // -----------------------------------------------------

        SetPlayerY(player1, p1Y);
        SetPlayerY(player2, p2Y);
        SetPlayerY(player3, p3Y);
        SetPlayerY(player4, p4Y);
    }

    // =========================================================
    // ALTERA POSIÇÃO Y
    // =========================================================

    private void SetPlayerY(
        Transform player,
        float y)
    {
        if (player == null)
            return;

        Vector3 position = player.position;

        position.y = y;

        player.position = position;
    }

    // =========================================================
    // MOVIMENTAÇÃO DA BOLA
    // =========================================================

    private void MoveBall()
    {
        Vector3 ballPosition = ball.position;

        ballPosition +=
            (Vector3)(
                ballVelocity *
                Time.deltaTime
            );

        // -----------------------------------------------------
        // PAREDE SUPERIOR
        // -----------------------------------------------------

        if (ballPosition.y >= topLimit)
        {
            ballPosition.y = topLimit;

            ballVelocity.y =
                -Mathf.Abs(ballVelocity.y);
        }

        // -----------------------------------------------------
        // PAREDE INFERIOR
        // -----------------------------------------------------

        if (ballPosition.y <= bottomLimit)
        {
            ballPosition.y = bottomLimit;

            ballVelocity.y =
                Mathf.Abs(ballVelocity.y);
        }

        ball.position = ballPosition;

        // -----------------------------------------------------
        // COLISÕES COM OS JOGADORES
        // -----------------------------------------------------

        CheckPaddleCollision(
            player1,
            true
        );

        CheckPaddleCollision(
            player2,
            true
        );

        CheckPaddleCollision(
            player3,
            false
        );

        CheckPaddleCollision(
            player4,
            false
        );

        // -----------------------------------------------------
        // GOL DO LADO ESQUERDO
        // -----------------------------------------------------

        if (ball.position.x <= leftGoalX)
        {
            scoreTeamB++;

            UpdateScoreUI();

            ResetBall(1);
        }

        // -----------------------------------------------------
        // GOL DO LADO DIREITO
        // -----------------------------------------------------

        if (ball.position.x >= rightGoalX)
        {
            scoreTeamA++;

            UpdateScoreUI();

            ResetBall(-1);
        }
    }

    // =========================================================
    // COLISÃO DA BOLA COM O JOGADOR
    // =========================================================

    private void CheckPaddleCollision(
        Transform paddle,
        bool isLeft)
    {
        if (paddle == null || ball == null)
            return;

        Vector2 ballPosition = ball.position;
        Vector2 paddlePosition = paddle.position;

        float halfWidth = 0.2f;
        float halfHeight = 0.8f;
        float ballRadius = 0.15f;

        Renderer paddleRenderer =
            paddle.GetComponent<Renderer>();

        if (paddleRenderer != null)
        {
            halfWidth =
                paddleRenderer.bounds.extents.x;

            halfHeight =
                paddleRenderer.bounds.extents.y;
        }

        Renderer ballRenderer =
            ball.GetComponent<Renderer>();

        if (ballRenderer != null)
        {
            ballRadius =
                Mathf.Max(
                    ballRenderer.bounds.extents.x,
                    ballRenderer.bounds.extents.y
                );
        }

        bool insideX =
            Mathf.Abs(
                ballPosition.x -
                paddlePosition.x
            ) <=
            halfWidth +
            ballRadius;

        bool insideY =
            Mathf.Abs(
                ballPosition.y -
                paddlePosition.y
            ) <=
            halfHeight +
            ballRadius;

        if (!insideX || !insideY)
            return;

        // -----------------------------------------------------
        // JOGADORES DA ESQUERDA
        // P1 E P2
        // -----------------------------------------------------

        if (isLeft &&
            ballVelocity.x < 0)
        {
            ballVelocity.x =
                Mathf.Abs(ballVelocity.x);

            ball.position =
                new Vector3(
                    paddlePosition.x +
                    halfWidth +
                    ballRadius +
                    0.02f,

                    ball.position.y,

                    ball.position.z
                );

            AddVerticalInfluence(paddle);
        }

        // -----------------------------------------------------
        // JOGADORES DA DIREITA
        // P3 E P4
        // -----------------------------------------------------

        if (!isLeft &&
            ballVelocity.x > 0)
        {
            ballVelocity.x =
                -Mathf.Abs(ballVelocity.x);

            ball.position =
                new Vector3(
                    paddlePosition.x -
                    halfWidth -
                    ballRadius -
                    0.02f,

                    ball.position.y,

                    ball.position.z
                );

            AddVerticalInfluence(paddle);
        }
    }

    // =========================================================
    // INFLUÊNCIA DO MOVIMENTO DO JOGADOR
    // =========================================================

    private void AddVerticalInfluence(
        Transform paddle)
    {
        float difference =
            ball.position.y -
            paddle.position.y;

        ballVelocity.y +=
            difference * 1.5f;

        ballVelocity =
            ballVelocity.normalized *
            ballSpeed;
    }

    // =========================================================
    // REINICIA A BOLA
    // =========================================================

    private void ResetBall(float direction)
    {
        ball.position = Vector3.zero;

        float randomY =
            Random.Range(
                -0.7f,
                0.7f
            );

        ballVelocity =
            new Vector2(
                direction,
                randomY
            ).normalized *
            ballSpeed;
    }

    // =========================================================
    // ENVIA ESTADO PARA O PC 2
    // =========================================================

    private void SendGameState()
    {
        if (!network.IsConnected)
            return;

        string message =
            "STATE|" +
            p1Y.ToString("F3") + "|" +
            p2Y.ToString("F3") + "|" +
            p3Y.ToString("F3") + "|" +
            p4Y.ToString("F3") + "|" +
            ball.position.x.ToString("F3") + "|" +
            ball.position.y.ToString("F3") + "|" +
            scoreTeamA + "|" +
            scoreTeamB;

        network.SendToClient(message);
    }

    // =========================================================
    // CLIENTE
    // PC 2 CONTROLA P3 + P4
    // =========================================================

    private void ClientUpdate()
    {
        ReadClientInputs();
    }

    // =========================================================
    // INPUT DO PC 2
    //
    // P3 = W / S
    // P4 = SETA CIMA / BAIXO
    // =========================================================

    private void ReadClientInputs()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        // -----------------------------------------------------
        // PLAYER 3
        // W = sobe
        // S = desce
        // -----------------------------------------------------

        float p3Input = 0f;

        if (keyboard.wKey.isPressed)
            p3Input = 1f;

        if (keyboard.sKey.isPressed)
            p3Input = -1f;

        // -----------------------------------------------------
        // PLAYER 4
        // SETA CIMA = sobe
        // SETA BAIXO = desce
        // -----------------------------------------------------

        float p4Input = 0f;

        if (keyboard.upArrowKey.isPressed)
            p4Input = 1f;

        if (keyboard.downArrowKey.isPressed)
            p4Input = -1f;

        // -----------------------------------------------------
        // ENVIA OS INPUTS
        // -----------------------------------------------------

        inputSendTimer += Time.deltaTime;

        if (inputSendTimer >= 1f / 30f)
        {
            inputSendTimer = 0f;

            network.SendToServer(
                "INPUT|3|" +
                p3Input.ToString("F0")
            );

            network.SendToServer(
                "INPUT|4|" +
                p4Input.ToString("F0")
            );
        }
    }

    // =========================================================
    // RECEBE INPUT DO PC 2
    // =========================================================

    public void ReceiveNetworkInput(
        string message)
    {
        string[] parts =
            message.Split('|');

        if (parts.Length < 3)
            return;

        if (parts[0] != "INPUT")
            return;

        if (!int.TryParse(
            parts[1],
            out int playerID))
        {
            return;
        }

        if (!float.TryParse(
            parts[2],
            out float input))
        {
            return;
        }

        input =
            Mathf.Clamp(
                input,
                -1f,
                1f
            );

        // P3
        if (playerID == 3)
        {
            p3NetworkInput = input;
        }

        // P4
        if (playerID == 4)
        {
            p4NetworkInput = input;
        }
    }

    // =========================================================
    // RECEBE ESTADO DO SERVIDOR
    // =========================================================

    private void ReceiveGameState(
        string message)
    {
        string[] parts =
            message.Split('|');

        // STATE + 8 valores = 9 partes
        if (parts.Length < 9)
            return;

        if (parts[0] != "STATE")
            return;

        float.TryParse(
            parts[1],
            out p1Y
        );

        float.TryParse(
            parts[2],
            out p2Y
        );

        float.TryParse(
            parts[3],
            out p3Y
        );

        float.TryParse(
            parts[4],
            out p4Y
        );

        float ballX;
        float ballY;

        float.TryParse(
            parts[5],
            out ballX
        );

        float.TryParse(
            parts[6],
            out ballY
        );

        int.TryParse(
            parts[7],
            out scoreTeamA
        );

        int.TryParse(
            parts[8],
            out scoreTeamB
        );

        // -----------------------------------------------------
        // ATUALIZA JOGADORES
        // -----------------------------------------------------

        SetPlayerY(
            player1,
            p1Y
        );

        SetPlayerY(
            player2,
            p2Y
        );

        SetPlayerY(
            player3,
            p3Y
        );

        SetPlayerY(
            player4,
            p4Y
        );

        // -----------------------------------------------------
        // ATUALIZA BOLA
        // -----------------------------------------------------

        ball.position =
            new Vector3(
                ballX,
                ballY,
                ball.position.z
            );

        UpdateScoreUI();
    }

    // =========================================================
    // ATUALIZA PLACAR
    // =========================================================

    private void UpdateScoreUI()
    {
        if (scoreUI != null)
        {
            scoreUI.UpdateScore(
                scoreTeamA,
                scoreTeamB
            );
        }
    }

    // =========================================================
    // LIMPEZA
    // =========================================================

    private void OnDestroy()
    {
        if (network != null &&
            network.IsClient)
        {
            network.OnGameStateReceived -=
                ReceiveGameState;
        }
    }
}