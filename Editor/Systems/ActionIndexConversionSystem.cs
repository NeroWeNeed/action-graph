using System;
using System.Linq;
using NeroWeNeed.ActionGraph.Editor.Schema;
using NeroWeNeed.Commons.Editor;
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
                var useFieldOperations = false;
                if (indexObj.definitionAssets != null) {
                    foreach (var definition in indexObj.definitionAssets.Distinct().Where(d => d != null && d.delegateType.IsCreated)) {
                        useFieldOperations = definition.useFieldOperations || useFieldOperations;
                        
                        typeof(ActionIndexConversionSystem).GetMethod(nameof(CreateBlob), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).MakeGenericMethod(definition.delegateType).Invoke(this, new object[] { definition, GetPrimaryEntity(indexObj) });
                    }
                }
                if (useFieldOperations) {
                    var operations = ProjectUtility.GetOrCreateProjectAsset<FieldOperationSchema>().CreateOperationsList();
                    BlobAssetStore.AddUniqueBlobAsset(ref operations);
                    DstEntityManager.AddComponentData(GetPrimaryEntity(indexObj), new FieldOperationList { value = operations });
                }

            });
        }

        private void CreateBlob<TAction>(ActionDefinitionAsset definition, Entity target) where TAction : Delegate {
            var actions = definition.GetActions();
            BlobAssetReference<FunctionList<TAction>> blobAsset;
            using (var builder = new BlobBuilder(Unity.Collections.Allocator.Temp)) {
                ref FunctionList<TAction> root = ref builder.ConstructRoot<FunctionList<TAction>>();
                var actionArray = builder.Allocate(ref root.value, actions.Count);
                for (int i = 0; i < actions.Count; i++) {
                    actionArray[i] = BurstCompiler.CompileFunctionPointer<TAction>((TAction)actions[i].method.Value.CreateDelegate(typeof(TAction)));
                }
                blobAsset = builder.CreateBlobAssetReference<FunctionList<TAction>>(Unity.Collections.Allocator.Persistent);
                BlobAssetStore.AddUniqueBlobAsset(ref blobAsset);
            }
            DstEntityManager.AddComponentData(target, new ActionList<TAction> { value = blobAsset });
        }
    }
}