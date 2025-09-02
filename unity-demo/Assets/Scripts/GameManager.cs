using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public TextMeshProUGUI scoreText;
    public GameObject winPanel;
    public GameObject losePanel;

    private int score = 0;
    private int totalBricks;

    void Start()
    {
        // 计算总砖块数量
        totalBricks = GameObject.FindGameObjectsWithTag("Brick").Length;
        UpdateScoreText();
    }

    void Update()
    {
        // 检查是否所有砖块被打破
        GameObject[] remainingBricks = GameObject.FindGameObjectsWithTag("Brick");
        if (remainingBricks.Length == 0)
        {
            Win();
        }
    }

    public void AddScore(int points)
    {
        score += points;
        UpdateScoreText();
    }

    void UpdateScoreText()
    {
        if (scoreText != null)
            scoreText.text = "Score: " + score;
    }

    void Win()
    {
        if (winPanel != null)
            winPanel.SetActive(true);
        Time.timeScale = 0;
    }

    void Lose()
    {
        if (losePanel != null)
            losePanel.SetActive(true);
        Time.timeScale = 0;
    }

    public void RestartGame()
    {
        Time.timeScale = 1;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}