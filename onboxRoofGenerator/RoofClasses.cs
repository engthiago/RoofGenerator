using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace onboxRoofGenerator
{
    enum RoofLineType { Hip, Ridge, Valley, Eave, Gable, Undefined };

    struct EdgeInfo
    {
        public Edge edge;
        public Curve curve;
        public RoofLineType roofLineType;
    }
}
