using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using onboxRoofGenerator.RoofClasses;

namespace onboxRoofGenerator.Managers
{
    class TrussManager
    {
        double trussDistance;

        public TrussManager(double targetTrussDistance = 8.2020997375)
        {
            trussDistance = targetTrussDistance;
        }

        public IList<TrussInfo> CreateTrussesFromRoof(FootPrintRoof currentRoof, TrussType tType)
        {
            IList<EdgeInfo> currentRoofEdgeInfoList = new List<EdgeInfo>();
            IList<TrussInfo> currentRoofTrussList = new List<TrussInfo>();

            if (currentRoof == null)
                return currentRoofTrussList;

            currentRoofEdgeInfoList = Support.GetRoofEdgeInfoList(currentRoof, false);

            if (currentRoof == null)
                return currentRoofTrussList;

            Document doc = currentRoof.Document;
            if (doc == null)
                return currentRoofTrussList;

            using (Transaction t = new Transaction(doc, "Create Roof Trusses"))
            {
                t.Start();
                foreach (EdgeInfo currentEdgeInfo in currentRoofEdgeInfoList)
                {
                    currentRoofTrussList = currentRoofTrussList.Union(CreateTrussInfoList(currentEdgeInfo, doc, tType)).ToList();
                }
                t.Commit();
            }

            return currentRoofTrussList;
        }

        public IList<TrussInfo> CreateTrussesFromRidgeInfo(EdgeInfo currentRidgeEdgeInfo, TrussType tType)
        {
            IList<TrussInfo> trussInfoList = new List<TrussInfo>();

            if (currentRidgeEdgeInfo == null)
                return trussInfoList;

            FootPrintRoof currentRoof = currentRidgeEdgeInfo.CurrentRoof;
            if (currentRoof == null)
                return trussInfoList;

            Document doc = currentRidgeEdgeInfo.CurrentRoof.Document;
            if (doc == null)
                return trussInfoList;

            IList<EdgeInfo> currentRoofEdgeInfoList = new List<EdgeInfo>();

            using (Transaction t = new Transaction(doc, "Create Ridge Trusses"))
            {
                t.Start();
                trussInfoList = CreateTrussInfoList(currentRidgeEdgeInfo, doc, tType);
                t.Commit();
            }

            return trussInfoList;
        }

        private IList<TrussInfo> CreateTrussInfoList(EdgeInfo currentRidgeEdgeInfo, Document doc, TrussType tType)
        {
            IList<TrussInfo> trussInfoList = new List<TrussInfo>();

            if (currentRidgeEdgeInfo.RoofLineType == RoofLineType.RidgeSinglePanel || currentRidgeEdgeInfo.RoofLineType == RoofLineType.Ridge)
            {
                Line currentRidgeWithSupports = currentRidgeEdgeInfo.Curve as Line;

                if (currentRidgeWithSupports == null)
                    return trussInfoList;

                IList<EdgeInfo> startConditions = currentRidgeEdgeInfo.GetEndConditions(0);
                IList<EdgeInfo> endConditions = currentRidgeEdgeInfo.GetEndConditions(1);

                currentRidgeWithSupports = ShortenRidgeIfNecessary(currentRidgeWithSupports, startConditions, endConditions);

                Tuple<int, double> iterations = Utils.Utils.EstabilishIterations(currentRidgeWithSupports.ApproximateLength, trussDistance);
                int numPoints = iterations.Item1;
                double distance = iterations.Item2;

                for (int i = 0; i <= numPoints; i++)
                {
                    double currentParam = i * distance;
                    TrussInfo currentTrussInfo = TrussInfo.BuildTrussAtRidge(currentParam, currentRidgeEdgeInfo);

                    if (currentTrussInfo != null)
                    {
                        SketchPlane stkP = SketchPlane.Create(doc, currentRidgeEdgeInfo.CurrentRoof.LevelId);

                        double levelHeight = currentRidgeEdgeInfo.GetCurrentRoofHeight();

                        XYZ firstPoint = new XYZ(currentTrussInfo.FirstPoint.X, currentTrussInfo.FirstPoint.Y, levelHeight);
                        XYZ secondPoint = new XYZ(currentTrussInfo.SecondPoint.X, currentTrussInfo.SecondPoint.Y, levelHeight);
                        Truss currentTruss = Truss.Create(doc, tType.Id, stkP.Id, Line.CreateBound(firstPoint, secondPoint));

                        currentTruss.get_Parameter(BuiltInParameter.TRUSS_HEIGHT).Set(currentTrussInfo.Height);
                        trussInfoList.Add(currentTrussInfo);
                    }
                    #region DEBUG ONLY
                    else
                    {
#if DEBUG
                        FamilySymbol fs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(type => type.Name.Contains("DebugPoint")).FirstOrDefault() as FamilySymbol;
                        doc.Create.NewFamilyInstance(currentRidgeEdgeInfo.GetTrussTopPoint(currentParam), fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
#endif
                    }
                    #endregion
                }
            }

            return trussInfoList;
        }

        private Line ShortenRidgeIfNecessary(Line currentRidgeWithSupports, IList<EdgeInfo> startConditions, IList<EdgeInfo> endConditions)
        {
            if (currentRidgeWithSupports == null)
                return null;

            Line newRidgeLine = currentRidgeWithSupports.Clone() as Line;

            if (startConditions.Count == 1 || startConditions.Count == 2)
            {
                EdgeInfo currentGableInfo = null;

                if (startConditions.Count == 1)
                {
                    currentGableInfo = startConditions[0];
                }
                else
                {
                    if (startConditions[0].RoofLineType == RoofLineType.Gable)
                        currentGableInfo = startConditions[0];
                    else if (startConditions[1].RoofLineType == RoofLineType.Gable)
                        currentGableInfo = startConditions[1];
                }

                if (currentGableInfo != null)
                {
                    XYZ startPoint = currentRidgeWithSupports.GetEndPoint(0);
                    XYZ edgePointOnRoofBaseHeight = new XYZ(startPoint.X, startPoint.Y, currentGableInfo.GetCurrentRoofHeight());
                    XYZ supportPoint = currentGableInfo.GetSupportPoint(edgePointOnRoofBaseHeight, currentRidgeWithSupports.Direction.Rotate(90));

                    newRidgeLine = Line.CreateBound(supportPoint, newRidgeLine.GetEndPoint(1));
                }
            }

            if (endConditions.Count == 1 || endConditions.Count == 2)
            {
                EdgeInfo currentGableInfo = null;

                if (endConditions.Count == 1)
                {
                    currentGableInfo = endConditions[0];
                }
                else
                {
                    if (endConditions[0].RoofLineType == RoofLineType.Gable)
                        currentGableInfo = endConditions[0];
                    else if (endConditions[1].RoofLineType == RoofLineType.Gable)
                        currentGableInfo = endConditions[1];
                }

                if (currentGableInfo != null)
                {
                    XYZ endPoint = currentRidgeWithSupports.GetEndPoint(1);
                    XYZ edgePointOnRoofBaseHeight = new XYZ(endPoint.X, endPoint.Y, currentGableInfo.GetCurrentRoofHeight());
                    XYZ supportPoint = currentGableInfo.GetSupportPoint(edgePointOnRoofBaseHeight, currentRidgeWithSupports.Direction.Rotate(90));

                    newRidgeLine = Line.CreateBound(newRidgeLine.GetEndPoint(0), supportPoint);
                }
            }

            return newRidgeLine;
        }
    }
}
