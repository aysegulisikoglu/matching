using UnityEngine;

public enum SousChefPassive { None, ExtraMoves2, StartRandomBomb }

[CreateAssetMenu(menuName = "Match3/SousChef")]
public class SousChef : ScriptableObject
{
    public string DisplayName;
    public SousChefPassive Passive;
}
