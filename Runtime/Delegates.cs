using System;

namespace NeroWeNeed.ActionGraph {
    public unsafe delegate void FieldTransformer(void* input, long inputLength,byte inputType, void* output, long outputLength, FieldTransformerConfiguration* configuration);

    public unsafe struct FieldTransformerConfiguration {
        void* value;
        byte type;
    }

}