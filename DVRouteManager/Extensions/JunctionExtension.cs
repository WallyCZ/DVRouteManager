using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DVRouteManager
{
    public static class JunctionExtension
    {
        public static bool IsFree(this Junction junction)
        {
            return junction.inBranch.track.IsSectorFreeFromJunction(8.0, junction)
                && junction.outBranches.All(b => b.track.IsSectorFreeFromJunction(8.0, junction));
        }
    }
}
