using System.Collections.Generic;
using System.Linq;
using MitarashiDango.FacialExpressionController;

namespace MitarashiDango.FacialExpressionController.Editor
{
    internal static class FacialExpressionBuildUtil
    {
        public static IEnumerable<FacialExpressionGroup> GetValidGroups(FacialExpressionController fec)
        {
            return fec.facialExpressionGroups?
                .Where(g => g != null && g.facialExpressions != null)
                ?? Enumerable.Empty<FacialExpressionGroup>();
        }
    }
}
