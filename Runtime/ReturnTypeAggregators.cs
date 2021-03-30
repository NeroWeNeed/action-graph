using Unity.Burst;

namespace NeroWeNeed.ActionGraph {
    [BurstCompile]
    public static class ReturnTypeAggregators {
        [BurstCompile]
        public static bool And(bool a, bool b) => a && b;
        [BurstCompile]
        public static bool Or(bool a, bool b) => a || b;
    }
}