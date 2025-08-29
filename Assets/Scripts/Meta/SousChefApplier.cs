using UnityEngine;

public class SousChefApplier : MonoBehaviour
{
    public SousChef Equipped;
    public GameManager GM;

    private void Start()
    {
        if (Equipped == null || GM == null || GM.Level == null) return;
        if (Equipped.Passive == SousChefPassive.ExtraMoves2)
            GM.Level.MoveLimit += 2;

    }
}
