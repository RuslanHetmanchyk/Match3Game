using UnityEngine;

namespace Match3.Model
{
    public enum GemType { Blue, Green, Red, Yellow, Purple, Bomb }

    public class GemModel
    {
        public GemType Type { get; set; }
        public Vector2Int Position { get; set; }
        public bool IsMoving { get; set; }
        public bool MarkedForDestroy { get; set; }

        public GemModel(GemType type, Vector2Int pos)
        {
            Type = type;
            Position = pos;
            IsMoving = false;
            MarkedForDestroy = false;
        }
    }
}