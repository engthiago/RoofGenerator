using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace onboxRoofGenerator
{
    static public class Extensions
    {
        static internal bool IsAlmostEqualTo(this Curve firstCurve, Curve secondCurve)
        {
            if (firstCurve != null && secondCurve != null)
            {
                XYZ firstCurveFirstPoint = firstCurve.GetEndPoint(0);
                XYZ firstCurveSecondPoint = firstCurve.GetEndPoint(1);

                XYZ secondCurveFirstPoint = secondCurve.GetEndPoint(0);
                XYZ secondCurveSecondPoint = secondCurve.GetEndPoint(1);

                if (firstCurveFirstPoint.IsAlmostEqualTo(secondCurveFirstPoint) && firstCurveSecondPoint.IsAlmostEqualTo(secondCurveSecondPoint) ||
                    firstCurveFirstPoint.IsAlmostEqualTo(secondCurveSecondPoint) && firstCurveSecondPoint.IsAlmostEqualTo(secondCurveFirstPoint))
                    return true;
            }
            return false;
        }

        static internal Line Flatten(this Line targetLine, double height = 0)
        {
            XYZ firstPoint = new XYZ(targetLine.GetEndPoint(0).X, targetLine.GetEndPoint(0).Y, height);
            XYZ secondPoint = new XYZ(targetLine.GetEndPoint(1).X, targetLine.GetEndPoint(1).Y, height);

            return Line.CreateBound(firstPoint, secondPoint);
        }
          
        static internal bool IsAlmostEqualTo(this double firstNumber, double secondNumber, double tolerance = 0.01)
        {
            double diference = Math.Abs(firstNumber - secondNumber);

            if (diference <= tolerance)
            {
                return true;
            }

            return false;
        }
               
        static internal bool ContainsSimilarCurve(this IList<Curve> currentList, Curve curve)
        {
            if (currentList != null)
            {
                foreach (Curve currentCurve in currentList)
                {
                    if (currentCurve.IsAlmostEqualTo(curve))
                        return true;
                }
            }
            return false;
        }
    }
}
