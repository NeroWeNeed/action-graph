namespace NeroWeNeed.ActionGraph {
    public static class ReturnTypeAggregators {
        public static bool And(bool a, bool b) => a && b;
        public static bool Or(bool a, bool b) => a || b;
    }
}