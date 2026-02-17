using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Runtime
{
    [CreateAssetMenu(fileName = "New Facial Expression Group", menuName = "Facial Expression Controller/Facial Expression Group", order = 1)]
    public class FacialExpressionGroup : ScriptableObject
    {
        public string groupName;

        public List<FacialExpression> facialExpressions;
    }
}