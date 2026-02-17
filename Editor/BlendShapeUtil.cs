using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public class BlendShapeUtil
    {
        /// <summary>
        /// SkinnedMeshRendererからブレンドシェイプ情報を取得する
        /// </summary>
        /// <param name="smr">取得対象のSkinnedMeshRenderer</param>
        /// <param name="excludeBlendShapes">除外対象とするブレンドシェイプ名</param>
        /// <returns>ブレンドシェイプの名称と値のペア</returns>
        public static Dictionary<string, float> GetBlendShapes(SkinnedMeshRenderer smr, List<string> excludeBlendShapes)
        {
            var blendShapes = new Dictionary<string, float>();

            var mesh = smr.sharedMesh;
            if (mesh == null)
            {
                return blendShapes;
            }

            if (excludeBlendShapes == null)
            {
                excludeBlendShapes = new List<string>();
            }

            for (var i = 0; i < mesh.blendShapeCount; i++)
            {
                var blendShapeName = mesh.GetBlendShapeName(i);

                if (excludeBlendShapes.Exists(name => name == blendShapeName))
                {
                    continue;
                }

                var blendShapeWeight = smr.GetBlendShapeWeight(i);

                blendShapes.Add(blendShapeName, blendShapeWeight);
            }

            return blendShapes;
        }

        /// <summary>
        /// メッシュに影響を及ぼしていないブレンドシェイプ名のリストを取得する
        /// </summary>
        /// <param name="smr">取得対象のSkinnedMeshRenderer</param>
        /// <returns>ブレンドシェイプ名のリスト</returns>
        public static List<string> FindEmptyBlendShapes(SkinnedMeshRenderer smr)
        {
            var blendShapes = new List<string>();

            var mesh = smr.sharedMesh;
            if (mesh == null)
            {
                return blendShapes;
            }

            var blendShapeCount = mesh.blendShapeCount;
            var vertexCount = mesh.vertexCount;

            var deltaVertices = new Vector3[vertexCount];
            var deltaNormals = new Vector3[vertexCount];
            var deltaTangents = new Vector3[vertexCount];

            for (int i = 0; i < blendShapeCount; i++)
            {
                var blendShapeName = mesh.GetBlendShapeName(i);
                var isShapeEmpty = true;

                var frameCount = mesh.GetBlendShapeFrameCount(i);
                for (int frame = 0; frame < frameCount; frame++)
                {
                    mesh.GetBlendShapeFrameVertices(i, frame, deltaVertices, deltaNormals, deltaTangents);

                    if (HasAnyDeltas(deltaVertices) || HasAnyDeltas(deltaNormals) || HasAnyDeltas(deltaTangents))
                    {
                        isShapeEmpty = false;
                        break;
                    }
                }

                if (isShapeEmpty)
                {
                    blendShapes.Add(blendShapeName);
                }
            }

            return blendShapes;
        }

        private static bool HasAnyDeltas(Vector3[] deltas)
        {
            const float epsilon = 1e-7f;

            for (int i = 0; i < deltas.Length; i++)
            {
                if (deltas[i].sqrMagnitude > epsilon * epsilon)
                {
                    return true;
                }
            }

            return false;
        }
    }
}