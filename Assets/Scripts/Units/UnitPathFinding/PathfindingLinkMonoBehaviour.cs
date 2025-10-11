using UnityEngine;

// Linkit asetetaan tyhjään linkkejä sisältävään game objektiin joka annetaan PathFindig.cs
// Pathfinding etsii yhteydet Editorissa ennakkoon annetusta linkki conteinerista.
public class PathfindingLinkMonoBehaviour : MonoBehaviour
{
    public Vector3 linkPositionA;
    public Vector3 linkPositionB;

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Vector3 aW = transform.TransformPoint(linkPositionA);
        Vector3 bW = transform.TransformPoint(linkPositionB);
        Gizmos.DrawSphere(aW, 0.15f);
        Gizmos.DrawSphere(bW, 0.15f);
        Gizmos.DrawLine(aW, bW);
    }

    public PathfindingLink GetPathfindingLink()
    {
        var aW = transform.TransformPoint(linkPositionA);
        var bW = transform.TransformPoint(linkPositionB);
        return new PathfindingLink
        {
            gridPositionA = LevelGrid.Instance.GetGridPosition(aW),
            gridPositionB = LevelGrid.Instance.GetGridPosition(bW),
        };
    }
}
