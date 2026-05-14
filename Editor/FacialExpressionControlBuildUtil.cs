using System.Collections.Generic;
using System.Linq;
using MitarashiDango.FacialExpressionController;

namespace MitarashiDango.FacialExpressionController.Editor
{
    internal static class FacialExpressionControlBuildUtil
    {
        public static IEnumerable<FacialExpressionGroup> GetValidGroups(FacialExpressionControl fec)
        {
            return fec.facialExpressionGroups?
                .Where(g => g != null && g.facialExpressions != null)
                ?? Enumerable.Empty<FacialExpressionGroup>();
        }
    }
}
