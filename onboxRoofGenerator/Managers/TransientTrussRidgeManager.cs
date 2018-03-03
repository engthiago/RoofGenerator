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
    class TransientTrussRidgeManager
    {
        internal double trussDistance;

        public TransientTrussRidgeManager(double targetTrussDistance = 8.2020997375)
        {
            trussDistance = targetTrussDistance;
        }

        public void CreateTrussesFromRoof(FootPrintRoof currentRoof)
        {
            IList<EdgeInfo> currentRoofEdgeInfoList = new List<EdgeInfo>();
            IList<TrussInfo> currentRoofTrussList = new List<TrussInfo>();

            if (currentRoof == null)
                return;

            Document doc = currentRoof.Document;
            if (doc == null)
                return;

            DirectShapeLibrary dl = DirectShapeLibrary.GetDirectShapeLibrary(doc);
            ElementId prevPrototypeTrussesTypeId = dl.FindDefinitionType("tTrussTypeLib");


            if (prevPrototypeTrussesTypeId != null && prevPrototypeTrussesTypeId != ElementId.InvalidElementId)
            {
                using (Transaction t1 = new Transaction(doc, "Clean"))
                {
                    t1.Start();
                    try
                {
                        doc.Delete(prevPrototypeTrussesTypeId);
                    }
                    catch (Exception)
                    {
                    }
                t1.Commit();
            }
        }

            currentRoofEdgeInfoList = Support.GetRoofEdgeInfoList(currentRoof, false).Where(e => e.RoofLineType == RoofLineType.Ridge || e.RoofLineType == RoofLineType.RidgeSinglePanel).ToList();

            if (currentRoofEdgeInfoList.Count == 0)
                return;

            doc.MakeTransientElements(new ProtoTypeTrussMaker(currentRoofEdgeInfoList, trussDistance));

            using (Transaction t2 = new Transaction(doc, "Create"))
            {
                t2.Start();
                doc.Regenerate();
            t2.Commit();
        }


            return;
        }

        private class ProtoTypeTrussMaker : ITransientElementMaker
        {
            double trussDistance;
            IList<EdgeInfo> roofEdgeInfoList = new List<EdgeInfo>();

            public ProtoTypeTrussMaker(IList<EdgeInfo> targetRoofEdgeInfoList, double targetTrussDistance)
            {
                roofEdgeInfoList = targetRoofEdgeInfoList;
                trussDistance = targetTrussDistance;
            }


            public void Execute()
            {
                Document doc = roofEdgeInfoList[0].CurrentRoof.Document;
                DirectShapeType currentShapeType = DirectShapeType.Create(doc, "tTrussType", new ElementId(BuiltInCategory.OST_StructuralTruss));
                DirectShape currentShape = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_StructuralTruss));

                foreach (EdgeInfo currentRidgeEdgeInfo in roofEdgeInfoList)
                {
                    if (currentRidgeEdgeInfo == null) continue;

                    Line currentRidgeLineShortenedBySupports = currentRidgeEdgeInfo.Curve as Line;

                    if (currentRidgeLineShortenedBySupports == null) continue;

                    IList<EdgeInfo> startConditions = currentRidgeEdgeInfo.GetEndConditions(0);
                    IList<EdgeInfo> endConditions = currentRidgeEdgeInfo.GetEndConditions(1);

                    currentRidgeLineShortenedBySupports = Support.ShortenRidge.ShortenRidgeIfNecessary(currentRidgeLineShortenedBySupports, startConditions, endConditions);

                    Tuple<int, double> iterations = Utils.Utils.EstabilishIterations(currentRidgeLineShortenedBySupports.ApproximateLength, trussDistance);
                    int numPoints = iterations.Item1;
                    double distance = iterations.Item2;

                    for (int i = 0; i <= numPoints; i++)
                    {
                        double currentParam = i * distance;
                        XYZ currentPointOnRidge = currentRidgeLineShortenedBySupports.Evaluate(currentRidgeLineShortenedBySupports.GetEndParameter(0) + currentParam, false);
                        TrussInfo currentTrussInfo = TrussInfo.BuildTrussAtRidge(currentPointOnRidge, currentRidgeEdgeInfo, null);

                        if (currentTrussInfo != null)
                        {
                            double levelHeight = currentRidgeEdgeInfo.GetCurrentRoofHeight();

                            XYZ firstPoint = new XYZ(currentTrussInfo.FirstPoint.X, currentTrussInfo.FirstPoint.Y, levelHeight);
                            XYZ secondPoint = new XYZ(currentTrussInfo.SecondPoint.X, currentTrussInfo.SecondPoint.Y, levelHeight);
                            XYZ thirdPoint = currentPointOnRidge;

                            Line firstLine = Line.CreateBound(firstPoint, secondPoint);
                            Line secondLine = Line.CreateBound(secondPoint, thirdPoint);
                            Line thirdLine = Line.CreateBound(thirdPoint, firstPoint);

                            CurveLoop curveLoop = new CurveLoop();
                            curveLoop.Append(firstLine);
                            curveLoop.Append(secondLine);
                            curveLoop.Append(thirdLine);

                            IList<CurveLoop> curveLoopList = new List<CurveLoop> { curveLoop };

                            Solid currentTransientTrussSolid = GeometryCreationUtilities.CreateExtrusionGeometry(curveLoopList, currentRidgeLineShortenedBySupports.Direction, 0.001);

                            currentShape.AppendShape(new List<GeometryObject> { currentTransientTrussSolid });
                            currentShape.SetTypeId(currentShapeType.Id);

                        }
                    }
                }

                currentShape.SetName("tTruss");
                DirectShapeLibrary.GetDirectShapeLibrary(doc).AddDefinitionType("tTrussTypeLib", currentShapeType.Id);
            }
        }
    }
}
