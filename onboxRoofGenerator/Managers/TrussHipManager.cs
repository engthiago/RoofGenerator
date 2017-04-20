using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using onboxRoofGenerator.RoofClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace onboxRoofGenerator.Managers
{
    class TrussHipManager
    {
        double trussDistance;

        public TrussHipManager(double targetTrussDistance = 8.2020997375)
        {
            trussDistance = targetTrussDistance;
        }

        public TrussInfo CreateTrussInfo(Document doc, XYZ currentPointOnHip, EdgeInfo currentRidgeEdgeInfo, Element firstSupport, Element secondSupport, TrussType tType)
        {
            TrussInfo currentTrussInfo = TrussInfo.BuildTrussAtHip(currentPointOnHip, currentRidgeEdgeInfo, firstSupport, secondSupport);

            using (Transaction t = new Transaction(doc, "Criar treliça"))
            {
                t.Start();

                if (currentTrussInfo != null)
                {
                    SketchPlane stkP = SketchPlane.Create(doc, currentRidgeEdgeInfo.CurrentRoof.LevelId);

                    double levelHeight = currentRidgeEdgeInfo.GetCurrentRoofHeight();

                    XYZ firstPoint = new XYZ(currentTrussInfo.FirstPoint.X, currentTrussInfo.FirstPoint.Y, levelHeight);
                    XYZ secondPoint = new XYZ(currentTrussInfo.SecondPoint.X, currentTrussInfo.SecondPoint.Y, levelHeight);
                    Truss currentTruss = Truss.Create(doc, tType.Id, stkP.Id, Line.CreateBound(firstPoint, secondPoint));

                    currentTruss.get_Parameter(BuiltInParameter.TRUSS_HEIGHT).Set(currentTrussInfo.Height);
                }

                t.Commit();

            }
            //DEBUG.CreateDebugPoint(doc, currentPointOnHip);

            return currentTrussInfo;
        }

    }
}
