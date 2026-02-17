using UnityEngine;

public class RoomOpening : MonoBehaviour
{
    public enum Direction
    {
        North,
        East,
        South,
        West
    }

    [SerializeField] private Direction baseDirection;

    public bool IsConnected { get; private set; }
    public RoomOpening ConnectedTo { get; private set; }

    public Direction FacingDirection => GetRotatedDirection();

    private Direction GetRotatedDirection()
    {
        // Get the world-space forward vector of the opening
        Vector3 worldForward = transform.forward;
        worldForward.y = 0; // Ignore Y component
        worldForward = worldForward.normalized;

        // Determine direction based on world-space forward vector
        float dotNorth = Vector3.Dot(worldForward, Vector3.forward);
        float dotEast = Vector3.Dot(worldForward, Vector3.right);
        float dotSouth = Vector3.Dot(worldForward, Vector3.back);
        float dotWest = Vector3.Dot(worldForward, Vector3.left);

        // Find the direction with the highest dot product
        float maxDot = Mathf.Max(dotNorth, dotEast, dotSouth, dotWest);

        if (maxDot == dotNorth) return Direction.North;
        if (maxDot == dotEast) return Direction.East;
        if (maxDot == dotSouth) return Direction.South;
        return Direction.West;
    }

    public void Initialize(Direction dir)
    {
        baseDirection = dir;
    }

    public void MarkConnected(RoomOpening other)
    {
        IsConnected = true;
        ConnectedTo = other;
    }

    public void Seal(GameObject wallPrefab, Transform parent)
    {
        IsConnected = true;
        ConnectedTo = null;

        if (wallPrefab == null)
        {
            return;
        }

        Instantiate(wallPrefab, transform.position, transform.rotation, parent);
    }
}
