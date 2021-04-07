using System;
using System.Linq;
using NeroWeNeed.ActionGraph.Editor.Schema;
using NeroWeNeed.Commons.Editor;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace NeroWeNeed.ActionGraph.Editor.Systems {

/*     [WorldSystemFilter(WorldSystemFilterFlags.GameObjectConversion)]
    [UpdateInGroup(typeof(GameObjectConversionGroup))] */
/*     public class ActionIndexConversionSystem : GameObjectConversionSystem {
        protected override void OnCreate() {
            base.OnCreate();
        }
        protected override void OnUpdate() {
            var schema = ProjectUtility.GetOrCreateProjectAsset<ActionSchema>();
            Entities.ForEach((ActionIndex indexObj) =>
            {
                var useFieldOperations = false;
                if (indexObj.definitionAssets != null) {
                    foreach (var definition in indexObj.definitionAssets.Distinct().Where(d => d != null && d.delegateType.IsCreated)) {
                        useFieldOperations = definition.useFieldOperations || useFieldOperations;

                        typeof(ActionIndexConversionSystem).GetMethod(nameof(CreateBlob), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).MakeGenericMethod(definition.delegateType).Invoke(this, new object[] { schema,  GetPrimaryEntity(indexObj) });
                    }
                }
                if (useFieldOperations) {
                    var operations = ProjectUtility.GetOrCreateProjectAsset<FieldOperationSchema>().CreateFunctionList();
                    BlobAssetStore.AddUniqueBlobAsset(ref operations);
                    DstEntityManager.AddComponentData(GetPrimaryEntity(indexObj), new FieldOperationList { value = operations });
                }

            });
        }

        private void CreateBlob<TAction>(ActionSchema schema,  Entity target) where TAction : Delegate {
            var blobAsset = schema.CreateFunctionList<TAction>();
            BlobAssetStore.AddUniqueBlobAsset(ref blobAsset);
            DstEntityManager.AddComponentData(target, new ActionList<TAction> { value = blobAsset });
        }
    } */
}