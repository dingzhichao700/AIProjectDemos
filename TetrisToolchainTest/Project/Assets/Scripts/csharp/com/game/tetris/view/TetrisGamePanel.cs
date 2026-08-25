using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Falling-block gameplay panel.
/// </summary>
public class TetrisGamePanel : BasePanel {
    /******************* UIComponent Define begin ************************/
    public GameButton btnPause;
    public RectTransform gridBoard;
    public RectTransform gridNext;
    public TextMeshProUGUI txtScore;
    public TextMeshProUGUI txtHighScore;
    public TextMeshProUGUI txtLevel;
    public TextMeshProUGUI txtLines;
    public TextMeshProUGUI txtCountdown;
    /******************* UIComponent Define finish ************************/

    private const int BoardWidth = 10;
    private const int BoardHeight = 20;
    private const float FallInterval = 0.65f;
    private const float MinimumFallInterval = 0.1f;
    private const int NextSize = 4;

    private static readonly Vector2Int[] SpawnOffset = {
        new Vector2Int(3, 0),
        new Vector2Int(3, 0),
        new Vector2Int(3, 0),
        new Vector2Int(3, 0),
        new Vector2Int(3, 0),
        new Vector2Int(3, 0),
        new Vector2Int(3, 0),
    };

    private static readonly Vector2Int[][] PieceBaseShapes = {
        new [] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(3, 1) }, // I
        new [] { new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(1, 2), new Vector2Int(2, 2) }, // O
        new [] { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1) }, // T
        new [] { new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(0, 2), new Vector2Int(1, 2) }, // S
        new [] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(2, 2) }, // Z
        new [] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1) }, // J
        new [] { new Vector2Int(2, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1) }, // L
    };

    private static readonly Vector2Int[][][] PieceShapes = BuildPieceShapes();

    private readonly Color[] pieceColors = {
        new Color(0.3f, 0.8f, 1f),
        new Color(1f, 0.85f, 0.2f),
        new Color(0.8f, 0.4f, 1f),
        new Color(0.2f, 1f, 0.6f),
        new Color(1f, 0.35f, 0.35f),
        new Color(1f, 0.6f, 0.2f),
        new Color(0.35f, 0.55f, 1f)
    };

    private Image[,] boardCells;
    private Image[] nextCells;
    private Image[,] cellPool;
    private Image[] nextPool;
    private readonly int[,] board = new int[BoardWidth, BoardHeight];

    private int currentPiece;
    private int currentRotation;
    private Vector2Int currentPos;
    private int nextPiece;
    private bool gameOver;
    private float fallTimer;
    private int score;
    private int highScore;
    private int level = 1;
    private int clearedLines;

    private bool pauseRequested;

    public TetrisGamePanel() {
        layer = PanelLayer.SCALE_PANEL_FIRST;
    }

    public override void OnOpen() {
        ResolveButtonBindings();
        AddLis();
        ResetGame();
    }

    /// <summary>恢复预制体未写入时的暂停按钮引用。</summary>
    private void ResolveButtonBindings() {
        if (btnPause != null) return;
        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++) {
            if (all[i].name == "btnPause") {
                btnPause = all[i].GetComponent<GameButton>();
                return;
            }
        }
    }

    public override void OnClose() {
        RemoveLis();
    }

    private void AddLis() {
        if (btnPause != null) {
            OnClick(btnPause.gameObject, OnPauseClicked);
            btnPause.Interactable = true;
            Image hitImage = btnPause.GetComponent<Image>();
            if (hitImage != null) hitImage.raycastTarget = true;
            btnPause.Label = "PAUSE";
            btnPause.Trans.pivot = new Vector2(0.5f, 0.5f);
        }
    }

    private void RemoveLis() {
        if (btnPause != null) {
            OffClick(btnPause.gameObject, OnPauseClicked);
        }
    }

    private void OnPauseClicked() {
        pauseRequested = true;
        PanelMgr.ins.OpenPanel(UIEnum.PAUSE_PANEL);
    }

    public void ResumeFromPause() {
        pauseRequested = false;
        fallTimer = 0f;
    }

    public void RestartFromPause() {
        pauseRequested = false;
        ResetGame();
    }

    private void ResetGame() {
        Array.Clear(board, 0, board.Length);
        score = 0;
        level = 1;
        clearedLines = 0;
        gameOver = false;
        pauseRequested = false;
        fallTimer = 0;
        highScore = PersistentDataControl.ins.saveModel.GetTetrisHighScore();

        EnsureBoardCells();
        EnsureNextCells();
        nextPiece = UnityEngine.Random.Range(0, PieceShapes.Length);
        SpawnPiece();
        RefreshAll();
    }

    private void Update() {
        if (!isOpened || gameOver || pauseRequested) {
            return;
        }

        float interval = Mathf.Max(MinimumFallInterval, FallInterval - (level - 1) * 0.05f);
        fallTimer += Time.deltaTime;
        if (fallTimer >= interval) {
            fallTimer = 0;
            StepDown();
        }
    }

    public override void OnPanelOperate(PanelOperateEnum operateCode) {
        if (gameOver) {
            return;
        }

        switch (operateCode) {
            case PanelOperateEnum.Left:
                TryMove(new Vector2Int(-1, 0));
                break;
            case PanelOperateEnum.Right:
                TryMove(new Vector2Int(1, 0));
                break;
            case PanelOperateEnum.Down:
                HardDrop();
                break;
            case PanelOperateEnum.Up:
                TryRotate();
                break;
            case PanelOperateEnum.SURE:
                HardDrop();
                break;
            case PanelOperateEnum.ESC:
                if (!pauseRequested) {
                    pauseRequested = true;
                    PanelMgr.ins.OpenPanel(UIEnum.PAUSE_PANEL);
                }
                break;
        }
    }

    private void StepDown() {
        if (!TryMove(new Vector2Int(0, 1))) {
            LockPiece();
        }
    }

    private void HardDrop() {
        while (TryMove(new Vector2Int(0, 1))) {
        }
        LockPiece();
    }

    private void TryRotate() {
        // 上方向键按逆时针方向旋转，旋转索引向前回退一格。
        int nextRotation = (currentRotation + 3) % 4;
        if (CanPlace(currentPiece, nextRotation, currentPos)) {
            currentRotation = nextRotation;
            RefreshAll();
            return;
        }

        Vector2Int[] kicks = {
            new Vector2Int(-1, 0),
            new Vector2Int(1, 0),
            new Vector2Int(-2, 0),
            new Vector2Int(2, 0),
            new Vector2Int(0, 1)
        };
        for (int i = 0; i < kicks.Length; i++) {
            Vector2Int pos = currentPos + kicks[i];
            if (CanPlace(currentPiece, nextRotation, pos)) {
                currentRotation = nextRotation;
                currentPos = pos;
                RefreshAll();
                return;
            }
        }
    }

    private bool TryMove(Vector2Int delta) {
        Vector2Int nextPos = currentPos + delta;
        if (!CanPlace(currentPiece, currentRotation, nextPos)) {
            return false;
        }
        currentPos = nextPos;
        RefreshAll();
        return true;
    }

    private void LockPiece() {
        Vector2Int[] cells = GetCells(currentPiece, currentRotation);
        for (int i = 0; i < cells.Length; i++) {
            Vector2Int p = currentPos + cells[i];
            if (p.y >= 0 && p.y < BoardHeight && p.x >= 0 && p.x < BoardWidth) {
                board[p.x, p.y] = currentPiece + 1;
            }
        }

        int cleared = ClearLines();
        if (cleared > 0) {
            clearedLines += cleared;
            score += GetLineClearScore(cleared);
            level = 1 + clearedLines / 10;
        }

        SpawnPiece();
    }

    private void SpawnPiece() {
        currentPiece = nextPiece;
        nextPiece = UnityEngine.Random.Range(0, PieceShapes.Length);
        currentRotation = 0;
        currentPos = SpawnOffset[currentPiece];
        fallTimer = 0;

        if (!CanPlace(currentPiece, currentRotation, currentPos)) {
            gameOver = true;
            Debug.Log("[Tetris] Game Over: spawn blocked.");
            RefreshAll();
            PanelMgr.ins.OpenPanel(UIEnum.GAME_RESULT_PANEL, new dynamic[] { score, clearedLines, level });
            return;
        }

        RefreshAll();
    }

    private bool CanPlace(int pieceIndex, int rotationIndex, Vector2Int pos) {
        Vector2Int[] cells = GetCells(pieceIndex, rotationIndex);
        for (int i = 0; i < cells.Length; i++) {
            Vector2Int p = pos + cells[i];
            if (p.x < 0 || p.x >= BoardWidth || p.y < 0 || p.y >= BoardHeight) {
                return false;
            }
            if (board[p.x, p.y] != 0) {
                return false;
            }
        }
        return true;
    }

    private Vector2Int[] GetCells(int pieceIndex, int rotationIndex) {
        return PieceShapes[pieceIndex][rotationIndex];
    }

    private static Vector2Int[][][] BuildPieceShapes() {
        Vector2Int[][][] result = new Vector2Int[PieceBaseShapes.Length][][];
        for (int i = 0; i < PieceBaseShapes.Length; i++) {
            result[i] = new Vector2Int[4][];
            result[i][0] = CloneCells(PieceBaseShapes[i]);
            for (int rotation = 1; rotation < 4; rotation++) {
                result[i][rotation] = RotateClockwise(result[i][rotation - 1]);
            }
        }
        return result;
    }

    private static Vector2Int[] CloneCells(Vector2Int[] source) {
        Vector2Int[] result = new Vector2Int[source.Length];
        Array.Copy(source, result, source.Length);
        return result;
    }

    private static Vector2Int[] RotateClockwise(Vector2Int[] source) {
        Vector2Int[] result = new Vector2Int[source.Length];
        for (int i = 0; i < source.Length; i++) {
            Vector2Int p = source[i];
            result[i] = new Vector2Int(3 - p.y, p.x);
        }
        NormalizeCells(result);
        return result;
    }

    private static void NormalizeCells(Vector2Int[] cells) {
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        for (int i = 0; i < cells.Length; i++) {
            minX = Mathf.Min(minX, cells[i].x);
            minY = Mathf.Min(minY, cells[i].y);
        }
        for (int i = 0; i < cells.Length; i++) {
            cells[i] = new Vector2Int(cells[i].x - minX, cells[i].y - minY);
        }
    }

    private int ClearLines() {
        int cleared = 0;
        for (int y = 0; y < BoardHeight; y++) {
            bool full = true;
            for (int x = 0; x < BoardWidth; x++) {
                if (board[x, y] == 0) {
                    full = false;
                    break;
                }
            }
            if (!full) {
                continue;
            }

            cleared++;
            for (int moveY = y; moveY > 0; moveY--) {
                for (int x = 0; x < BoardWidth; x++) {
                    board[x, moveY] = board[x, moveY - 1];
                }
            }
            for (int x = 0; x < BoardWidth; x++) {
                board[x, 0] = 0;
            }
            y--;
        }
        return cleared;
    }

    private int GetLineClearScore(int cleared) {
        int baseScore = cleared switch {
            1 => 100,
            2 => 300,
            3 => 500,
            4 => 800,
            _ => cleared * 800
        };
        return baseScore * level;
    }

    private void EnsureBoardCells() {
        if (boardCells != null) {
            return;
        }

        boardCells = new Image[BoardWidth, BoardHeight];
        cellPool = new Image[BoardWidth, BoardHeight];
        for (int x = 0; x < BoardWidth; x++) {
            for (int y = 0; y < BoardHeight; y++) {
                GameObject go = new GameObject($"cell_{x}_{y}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(gridBoard, false);
                RectTransform rt = go.transform as RectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = Vector2.one * 36f;
                rt.anchoredPosition = new Vector2(x * 40f + 2f, -(y * 40f + 2f));
                Image img = go.GetComponent<Image>();
                img.raycastTarget = false;
                img.color = Color.clear;
                boardCells[x, y] = img;
                cellPool[x, y] = img;
            }
        }
    }

    private void EnsureNextCells() {
        if (nextCells != null) {
            return;
        }

        nextCells = new Image[NextSize * NextSize];
        nextPool = new Image[NextSize * NextSize];
        for (int i = 0; i < nextCells.Length; i++) {
            int x = i % NextSize;
            int y = i / NextSize;
            GameObject go = new GameObject($"next_{x}_{y}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(gridNext, false);
            RectTransform rt = go.transform as RectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = Vector2.one * 54f;
            rt.anchoredPosition = new Vector2(x * 60f + 3f, -(y * 60f + 3f));
            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.color = Color.clear;
            nextCells[i] = img;
            nextPool[i] = img;
        }
    }

    private void RefreshAll() {
        RefreshBoard();
        RefreshNext();
        RefreshLabels();
    }

    private void RefreshBoard() {
        for (int x = 0; x < BoardWidth; x++) {
            for (int y = 0; y < BoardHeight; y++) {
                boardCells[x, y].color = board[x, y] == 0 ? Color.clear : pieceColors[board[x, y] - 1];
            }
        }

        if (gameOver) {
            return;
        }

        Vector2Int[] cells = GetCells(currentPiece, currentRotation);
        for (int i = 0; i < cells.Length; i++) {
            Vector2Int p = currentPos + cells[i];
            if (p.x >= 0 && p.x < BoardWidth && p.y >= 0 && p.y < BoardHeight) {
                boardCells[p.x, p.y].color = pieceColors[currentPiece];
            }
        }
    }

    private void RefreshNext() {
        for (int i = 0; i < nextCells.Length; i++) {
            nextCells[i].color = Color.clear;
        }

        Vector2Int[] cells = GetCells(nextPiece, 0);
        for (int i = 0; i < cells.Length; i++) {
            Vector2Int p = cells[i];
            int x = p.x;
            int y = p.y;
            if (x >= 0 && x < NextSize && y >= 0 && y < NextSize) {
                nextCells[y * NextSize + x].color = pieceColors[nextPiece];
            }
        }
    }

    private void RefreshLabels() {
        if (txtScore != null) {
            txtScore.text = score.ToString();
        }
        if (txtHighScore != null) {
            highScore = Mathf.Max(highScore, score);
            PersistentDataControl.ins.saveModel.SetTetrisHighScore(highScore);
            txtHighScore.text = highScore.ToString();
        }
        if (txtLevel != null) {
            txtLevel.text = level.ToString();
        }
        if (txtLines != null) {
            txtLines.text = clearedLines.ToString();
        }
        if (txtCountdown != null) {
            txtCountdown.text = gameOver ? "GAME OVER" : string.Empty;
        }
    }
}
