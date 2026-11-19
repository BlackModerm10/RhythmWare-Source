using UnityEngine.InputSystem;

namespace RhythmWare.Core
{
    public static class LaneLayout
    {
        public const int Count=5, LeftCount=2, Center=4;
        public const string Id="branch-5-v1", LegacyId="branch-7-v1";
        public static readonly string[] Names={"D","F","J","K","SPACE"};
        public static readonly int[] VisualOrder={0,1,4,2,3};
        public static readonly Key[] DefaultKeys={Key.D,Key.F,Key.J,Key.K,Key.Space};
        public static bool Valid(int lane) => lane>=0&&lane<Count;
        public static bool IsLeft(int lane) => lane<LeftCount;
    }
}
