using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public Board Board;
    public LevelConfig Level;

    [Header("UI")]
    public Text MovesText;
    public Text GoalText;
    public Text StatusText;

    private int _movesLeft;
    private int _goalLeft;
    private int _score;

    private void Awake()
    {
        if (Board != null && Level != null)
        {
            Board.Width = Level.Width;
            Board.Height = Level.Height;
        }
        _movesLeft = Level != null ? Level.MoveLimit : 20;
        _goalLeft = Level != null ? Level.GoalCount : 30;
        UpdateUI();
    }

    private void OnEnable()
    {
        if (Board != null)
        {
            Board.OnTilesCleared += HandleTilesCleared;
            Board.OnBoardStable += HandleBoardStable;
            Board.OnChainResolved += HandleChain;
        }
    }

    private void OnDisable()
    {
        if (Board != null)
        {
            Board.OnTilesCleared -= HandleTilesCleared;
            Board.OnBoardStable -= HandleBoardStable;
            Board.OnChainResolved -= HandleChain;
        }
    }

    private void Start()
    {
        if (Board != null) Board.InputEnabled = true;
        if (StatusText != null) StatusText.text = "";
        UpdateUI();
    }

    // ----- skor & hedef -----
    void HandleTilesCleared(int count)
    {
        _score += count * 10; // baz skor
    }

    void HandleBoardStable()
    {
        if (_movesLeft > 0) _movesLeft--; // geçerli swap sonrası 1 düş
        UpdateUI();
        CheckEnd();
    }

    void HandleChain(int chain, int clearedThisChain)
    {
        int add = ComboScore(chain, clearedThisChain);
        _score += add;

        if (StatusText != null && clearedThisChain > 0)
            StatusText.text = chain == 1 ? "Nice!" : $"Combo x{chain} (+{add})";
    }

    int ComboScore(int chain, int cleared)
    {
        float mult = chain == 1 ? 1f : (chain == 2 ? 1.5f : (chain == 3 ? 2f : 3f));
        return Mathf.RoundToInt(cleared * 10 * mult);
    }

    public void CountGoalFrom(TileType type, int cleared)
    {
        if (Level == null) return;
        if (type == Level.GoalTile)
        {
            _goalLeft -= cleared;
            if (_goalLeft < 0) _goalLeft = 0;
            UpdateUI();
            CheckEnd();
        }
    }

    // ----- UI / Bitiş -----
    void UpdateUI()
    {
        if (MovesText) MovesText.text = $"Moves: {_movesLeft}";
        if (GoalText) GoalText.text = $"Goal: {Level.GoalTile}";  // isteğin üzerine sade
        if (StatusText && _goalLeft > 0 && _movesLeft > 0) StatusText.text = "";

    }

    void CheckEnd()
    {
        if (_goalLeft <= 0)
        {
            if (Board) Board.InputEnabled = false;
            if (StatusText) StatusText.text = "Level Complete!";
        }
        else if (_movesLeft <= 0)
        {
            if (Board) Board.InputEnabled = false;
            if (StatusText) StatusText.text = "Out of Moves!";
        }
    }
}
