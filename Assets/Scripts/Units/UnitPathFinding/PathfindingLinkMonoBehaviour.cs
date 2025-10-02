using UnityEngine;

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
        return new PathfindingLink
        {
            gridPositionA = LevelGrid.Instance.GetGridPosition(linkPositionA),
            gridPositionB = LevelGrid.Instance.GetGridPosition(linkPositionB),
        };
    }
}
