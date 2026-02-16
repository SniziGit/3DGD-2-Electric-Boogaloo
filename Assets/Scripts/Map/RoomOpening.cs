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

    [SerializeField] private Direction direction;

    public bool IsConnected { get; private set; }
    public RoomOpening ConnectedTo { get; private set; }

    public Direction FacingDirection => direction;

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
