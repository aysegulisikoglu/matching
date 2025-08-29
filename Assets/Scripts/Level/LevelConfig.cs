using UnityEngine;

[CreateAssetMenu(menuName = "Match3/LevelConfig")]
public class LevelConfig : ScriptableObject
{
    [Header("Board")]
    public int Width = 8;
    public int Height = 8;

    [Header("Rules")]
    public int MoveLimit = 20;
    public TileType GoalTile = TileType.Red;
    public int GoalCount = 30;
}
