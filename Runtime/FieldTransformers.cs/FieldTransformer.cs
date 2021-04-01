using NeroWeNeed.Commons;
using UnityEngine;


namespace NeroWeNeed.ActionGraph {
    [HideInInspector]
    public unsafe delegate void FieldTransformer(void* input, long inputLength, byte inputType, void* output, long expectedOutputLength);
    public struct BinaryOperationData<TData> where TData : unmanaged {
        public TData value;
    }


/*     public static class BinaryOperationTransformers {


    }
 */

}