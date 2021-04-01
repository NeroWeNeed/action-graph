using System;
using System.Linq;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace NeroWeNeed.ActionGraph.Editor.Systems {

    [WorldSystemFilter(WorldSystemFilterFlags.GameObjectConversion)]
    [UpdateInGroup(typeof(GameObjectConversionGroup))]
    public class ActionIndexConversionSystem : GameObjectConversionSystem {
        protected override void OnCreate() {
            base.OnCreate();
        }
        protected override void OnUpdate() {
            Entities.ForEach((ActionIndex indexObj) =>
            {
                if (indexObj.definitionAssets != null) {
                    foreach (var definition in indexObj.definitionAssets.Distinct().Where(d => d != null && d.delegateType.IsCreated)) {
                        typeof(ActionIndexConversionSystem).GetMethod(nameof(CreateBlob), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).MakeGenericMethod(definition.delegateType).Invoke(this, new object[] { definition, GetPrimaryEntity(indexObj) });
                    }
                }

            });
        }

        private void CreateBlob<TAction>(ActionDefinitionAsset definition, Entity target) where TAction : Delegate {
            var actions = definition.GetActions();
            BlobAssetReference<ActionIndexData<TAction>> blobAsset;
            using (var builder = new BlobBuilder(Unity.Collections.Allocator.Temp)) {
                ref ActionIndexData<TAction> root = ref builder.ConstructRoot<ActionIndexData<TAction>>();
                var actionArray = builder.Allocate(ref root.value, actions.Count);
                for (int i = 0; i < actions.Count; i++) {
                    actionArray[i] = BurstCompiler.CompileFunctionPointer<TAction>((TAction)actions[i].method.Value.CreateDelegate(typeof(TAction)));
                }
                blobAsset = builder.CreateBlobAssetReference<ActionIndexData<TAction>>(Unity.Collections.Allocator.Persistent);
                BlobAssetStore.AddUniqueBlobAsset(ref blobAsset);
            }
            DstEntityManager.AddComponentData(target, new ActionIndex<TAction> { value = blobAsset });
        }
    }
}