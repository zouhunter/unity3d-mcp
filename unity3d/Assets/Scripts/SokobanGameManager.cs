using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SokobanGameManager : MonoBehaviour
{
    [Header("UI References")]
    public Text titleText;
    public Text stepsText;
    public Button restartButton;
    public Transform gamePanel;

    [Header("Game Settings")]
    public int gridWidth = 10;
    public int gridHeight = 10;
    public float cellSize = 40f;

    // Game State
    private int[,] gameGrid;
    private Vector2Int playerPos;
    private List<Vector2Int> boxPositions = new List<Vector2Int>();
    private List<Vector2Int> targetPositions = new List<Vector2Int>();
    private List<GameObject> gameObjects = new List<GameObject>();
    private int steps = 0;

    // Grid Values
    private const int EMPTY = 0;
    private const int WALL = 1;
    private const int TARGET = 2;
    private const int BOX = 3;
    private const int PLAYER = 4;

    void Start()
    {
        InitializeUI();
        InitializeLevel();
    }

    void InitializeUI()
    {
        // UI references are now bound via MCP - no manual finding needed

        // Set up restart button event
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartLevel);
        }
        else
        {
            Debug.LogError("RestartButton not bound! Please use MCP to bind UI references.");
        }
    }

    void InitializeLevel()
    {
        ClearLevel();
        CreateSimpleLevel();
        RenderLevel();
        UpdateStepsText();
    }

    void ClearLevel()
    {
        foreach (GameObject obj in gameObjects)
        {
            if (obj != null) DestroyImmediate(obj);
        }
        gameObjects.Clear();
        boxPositions.Clear();
        targetPositions.Clear();
        steps = 0;
    }

    void CreateSimpleLevel()
    {
        gameGrid = new int[gridWidth, gridHeight];

        // Create a simple level - walls around the border
        for (int x = 0; x < gridWidth; x++)
        {
            gameGrid[x, 0] = WALL;
            gameGrid[x, gridHeight - 1] = WALL;
        }
        for (int y = 0; y < gridHeight; y++)
        {
            gameGrid[0, y] = WALL;
            gameGrid[gridWidth - 1, y] = WALL;
        }

        // Player position
        playerPos = new Vector2Int(2, 2);
        gameGrid[2, 2] = PLAYER;

        // Boxes and targets
        Vector2Int[] boxes = { new Vector2Int(4, 3), new Vector2Int(5, 4) };
        Vector2Int[] targets = { new Vector2Int(7, 3), new Vector2Int(7, 4) };

        foreach (Vector2Int box in boxes)
        {
            gameGrid[box.x, box.y] = BOX;
            boxPositions.Add(box);
        }

        foreach (Vector2Int target in targets)
        {
            gameGrid[target.x, target.y] = TARGET;
            targetPositions.Add(target);
        }

        // Some walls for obstacles
        gameGrid[3, 5] = WALL;
        gameGrid[4, 5] = WALL;
        gameGrid[6, 2] = WALL;
    }

    void RenderLevel()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector2 worldPos = GridToWorldPos(x, y);

                // Always create ground first
                CreateGameObject(Color.white, worldPos, 0, "Ground");

                // Then create the specific cell content
                switch (gameGrid[x, y])
                {
                    case WALL:
                        CreateGameObject(Color.gray, worldPos, 1, "Wall");
                        break;
                    case TARGET:
                        CreateGameObject(Color.green, worldPos, 1, "Target");
                        break;
                    case BOX:
                        CreateGameObject(Color.yellow, worldPos, 2, "Box");
                        break;
                    case PLAYER:
                        CreateGameObject(Color.blue, worldPos, 3, "Player");
                        break;
                }
            }
        }
    }

    void CreateGameObject(Color color, Vector2 position, int sortingOrder, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(gamePanel);

        RectTransform rect = obj.AddComponent<RectTransform>();
        Image img = obj.AddComponent<Image>();

        img.color = color;
        rect.sizeDelta = new Vector2(cellSize - 2, cellSize - 2);
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;

        // Set sorting order for proper layering
        Canvas canvas = obj.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        gameObjects.Add(obj);
    }

    Vector2 GridToWorldPos(int gridX, int gridY)
    {
        float startX = -(gridWidth * cellSize) / 2f + cellSize / 2f;
        float startY = (gridHeight * cellSize) / 2f - cellSize / 2f;
        return new Vector2(startX + gridX * cellSize, startY - gridY * cellSize);
    }

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        Vector2Int direction = Vector2Int.zero;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            direction = Vector2Int.up;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            direction = Vector2Int.down;
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            direction = Vector2Int.left;
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            direction = Vector2Int.right;

        if (direction != Vector2Int.zero)
        {
            MovePlayer(direction);
        }
    }

    void MovePlayer(Vector2Int direction)
    {
        Vector2Int newPlayerPos = new Vector2Int(playerPos.x - direction.y, playerPos.y + direction.x);

        // Check boundaries
        if (newPlayerPos.x < 0 || newPlayerPos.x >= gridWidth ||
            newPlayerPos.y < 0 || newPlayerPos.y >= gridHeight)
            return;

        // Check wall collision
        if (gameGrid[newPlayerPos.x, newPlayerPos.y] == WALL)
            return;

        // Check box collision and push logic
        if (gameGrid[newPlayerPos.x, newPlayerPos.y] == BOX)
        {
            Vector2Int newBoxPos = new Vector2Int(newPlayerPos.x - direction.y, newPlayerPos.y + direction.x);

            // Check if box can be pushed
            if (newBoxPos.x < 0 || newBoxPos.x >= gridWidth ||
                newBoxPos.y < 0 || newBoxPos.y >= gridHeight ||
                gameGrid[newBoxPos.x, newBoxPos.y] == WALL ||
                gameGrid[newBoxPos.x, newBoxPos.y] == BOX)
                return;

            // Move box
            gameGrid[newPlayerPos.x, newPlayerPos.y] = EMPTY;
            gameGrid[newBoxPos.x, newBoxPos.y] = BOX;

            // Update box positions list
            int boxIndex = boxPositions.FindIndex(pos => pos == newPlayerPos);
            if (boxIndex >= 0)
            {
                boxPositions[boxIndex] = newBoxPos;
            }
        }

        // Move player
        gameGrid[playerPos.x, playerPos.y] = EMPTY;
        gameGrid[newPlayerPos.x, newPlayerPos.y] = PLAYER;
        playerPos = newPlayerPos;

        // Restore targets that might have been overwritten
        foreach (Vector2Int target in targetPositions)
        {
            if (gameGrid[target.x, target.y] == EMPTY)
            {
                gameGrid[target.x, target.y] = TARGET;
            }
        }

        steps++;
        UpdateStepsText();
        RenderLevel();
        CheckWinCondition();
    }

    void UpdateStepsText()
    {
        if (stepsText != null)
            stepsText.text = "步数: " + steps;
    }

    void CheckWinCondition()
    {
        foreach (Vector2Int target in targetPositions)
        {
            bool hasBox = boxPositions.Contains(target);
            if (!hasBox) return;
        }

        // Player won!
        if (titleText != null)
            titleText.text = "恭喜通关！步数: " + steps;
    }

    public void RestartLevel()
    {
        InitializeLevel();
        if (titleText != null)
            titleText.text = "推箱子游戏";
    }
}
