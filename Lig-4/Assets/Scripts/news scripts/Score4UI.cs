using TMPro;
using UnityEngine;

public class Score4UI : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;

    public void UpdateScore(
        int teamA,
        int teamB)
    {
        if (scoreText == null)
            return;

        scoreText.text =
            teamA + "  x  " + teamB;
    }
}