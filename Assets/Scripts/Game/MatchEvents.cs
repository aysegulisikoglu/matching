using UnityEngine;

public class MatchEvents : MonoBehaviour
{
    public static MatchEvents Instance;
    public GameManager Game;

    private void Awake() { Instance = this; }

    public void ReportCleared(TileType type, int count)
    {
        if (Game != null) Game.CountGoalFrom(type, count);
    }
}
