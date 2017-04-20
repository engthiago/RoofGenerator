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

        public TrussInfo CreateTrussFromRidgeWithSupports(XYZ currentPointOnRidge, EdgeInfo currentRidgeEdgeInfo, TrussType tType, Line support0, Line support1)
        {
            TrussInfo currentTrussInfo = null;

            Document doc = currentRidgeEdgeInfo.CurrentRoof.Document;
            if (doc == null)
                return currentTrussInfo;

            IList<XYZ> currentSupportPoints = new List<XYZ>();
            double roofheight = currentRidgeEdgeInfo.GetCurrentRoofHeight();

            Line currentRidgeLineFlatten = (currentRidgeEdgeInfo.Curve as Line).Flatten(roofheight);

            if (currentRidgeLineFlatten == null)
                return currentTrussInfo;

            XYZ crossDirection = currentRidgeLineFlatten.Direction.CrossProduct(XYZ.BasisZ);
            XYZ currentPointOnRidgeFlatten = new XYZ(currentPointOnRidge.X, currentPointOnRidge.Y, roofheight);

            Line currentCrossedLineInfinite = Line.CreateBound(currentPointOnRidgeFlatten, currentPointOnRidgeFlatten.Add(crossDirection));
            currentCrossedLineInfinite.MakeUnbound();

            if (support0 != null)
            {
                IntersectionResultArray iResultArr = new IntersectionResultArray();
                SetComparisonResult iResulComp = currentCrossedLineInfinite.Intersect(support0.Flatten(roofheight), out iResultArr);
                if (iResultArr != null && iResultArr.Size == 1)
                {
                    currentSupportPoints.Add(iResultArr.get_Item(0).XYZPoint);
                }
            }
            if (support1 != null)
            {
                IntersectionResultArray iResultArr = new IntersectionResultArray();
                SetComparisonResult iResulComp = currentCrossedLineInfinite.Intersect(support1.Flatten(roofheight), out iResultArr);
                if (iResultArr != null && iResultArr.Size == 1)
                {
                    currentSupportPoints.Add(iResultArr.get_Item(0).XYZPoint);
                }
            }

            using (Transaction t = new Transaction(doc, "Criar treliça"))
            {
                t.Start();
                currentTrussInfo = CreateTrussInfo(doc, currentPointOnRidge, currentRidgeEdgeInfo, currentSupportPoints, tType);
                t.Commit();
            }

            return currentTrussInfo;
        }

        private TrussInfo CreateTrussInfo(Document doc, XYZ currentPointOnRidge, EdgeInfo currentRidgeEdgeInfo, IList<XYZ> currentSupportPoints, TrussType tType)
        {

            TrussInfo currentTrussInfo = TrussInfo.BuildTrussAtRidge(currentPointOnRidge, currentRidgeEdgeInfo, currentSupportPoints);

            if (currentTrussInfo != null)
            {
                SketchPlane stkP = SketchPlane.Create(doc, currentRidgeEdgeInfo.CurrentRoof.LevelId);

                double levelHeight = currentRidgeEdgeInfo.GetCurrentRoofHeight();

                XYZ firstPoint = new XYZ(currentTrussInfo.FirstPoint.X, currentTrussInfo.FirstPoint.Y, levelHeight);
                XYZ secondPoint = new XYZ(currentTrussInfo.SecondPoint.X, currentTrussInfo.SecondPoint.Y, levelHeight);
                Truss currentTruss = Truss.Create(doc, tType.Id, stkP.Id, Line.CreateBound(firstPoint, secondPoint));

                currentTruss.get_Parameter(BuiltInParameter.TRUSS_HEIGHT).Set(currentTrussInfo.Height);
            }


            return currentTrussInfo;
        }

        private IList<TrussInfo> CreateTrussInfoList(EdgeInfo currentRidgeEdgeInfo, Document doc, TrussType tType)
        {
            IList<TrussInfo> trussInfoList = new List<TrussInfo>();

            if (currentRidgeEdgeInfo.RoofLineType == RoofLineType.RidgeSinglePanel || currentRidgeEdgeInfo.RoofLineType == RoofLineType.Ridge)
            {
                Line currentRidgeLineShortenedBySupports = currentRidgeEdgeInfo.Curve as Line;

                if (currentRidgeLineShortenedBySupports == null)
                    return trussInfoList;

                IList<EdgeInfo> startConditions = currentRidgeEdgeInfo.GetEndConditions(0);
                IList<EdgeInfo> endConditions = currentRidgeEdgeInfo.GetEndConditions(1);

                currentRidgeLineShortenedBySupports = ShortenRidgeIfNecessary(currentRidgeLineShortenedBySupports, startConditions, endConditions);

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
                        doc.Create.NewFamilyInstance(currentPointOnRidge, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
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

            //TODO Change this to the max distance between Trusses
            double maxDistance = 9;

            Line newRidgeLine = currentRidgeWithSupports.Clone() as Line;

            if (startConditions != null)
            {
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
                        XYZ supportPoint = currentGableInfo.GetSupportPoint(edgePointOnRoofBaseHeight, currentRidgeWithSupports.Direction.Rotate(90), maxDistance);
                        XYZ newRidgeStartPoint = new XYZ(supportPoint.X, supportPoint.Y, startPoint.Z);

                        newRidgeLine = Line.CreateBound(newRidgeStartPoint, newRidgeLine.GetEndPoint(1));
                    }
                }
            }

            if (endConditions != null)
            {
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
                        XYZ supportPoint = currentGableInfo.GetSupportPoint(edgePointOnRoofBaseHeight, currentRidgeWithSupports.Direction.Rotate(90), maxDistance);
                        XYZ newRidgeEndPoint = new XYZ(supportPoint.X, supportPoint.Y, endPoint.Z);

                        newRidgeLine = Line.CreateBound(newRidgeLine.GetEndPoint(0), newRidgeEndPoint);
                    }
                }
            }


            if (startConditions != null && endConditions != null)
            {
                bool canShortenBothEnds = true;
                bool canShortenStart = true;
                bool canShortenEnd = true;

                foreach (EdgeInfo currentStartCondition in startConditions)
                {
                    if (currentStartCondition.RoofLineType == RoofLineType.Gable)
                    {
                        canShortenBothEnds = false;
                        canShortenStart = false;
                    }
                }

                foreach (EdgeInfo currentEndCondition in endConditions)
                {
                    if (currentEndCondition.RoofLineType == RoofLineType.Gable)
                    {
                        canShortenBothEnds = false;
                        canShortenEnd = false;
                    }
                }

                double SETBACKAMOUNT = Utils.Utils.ConvertM.cmToFeet(20);

                if (canShortenBothEnds)
                {
                    newRidgeLine = ShortenRidgeBySetBack(newRidgeLine, SETBACKAMOUNT);
                }
                else if (canShortenStart)
                {
                    newRidgeLine = ShortenStartRidgeBySetBack(newRidgeLine, SETBACKAMOUNT);
                }
                else if (canShortenEnd)
                {
                    newRidgeLine = ShortenEndRidgeBySetBack(newRidgeLine, SETBACKAMOUNT);
                }

            }


            return newRidgeLine;
        }

        private Line ShortenRidgeBySetBack(Line currentRidgeLine, double amount)
        {
            Line currentRidgeToReturn = currentRidgeLine;

            if (currentRidgeLine.ApproximateLength > ((amount * 2) + Utils.Utils.ConvertM.cmToFeet(3)))
            {
                XYZ firstPoint = currentRidgeLine.GetEndPoint(0).Add(currentRidgeLine.Direction.Multiply(amount));
                XYZ secondPoint = currentRidgeLine.GetEndPoint(1).Add(currentRidgeLine.Direction.Negate().Multiply(amount));

                currentRidgeToReturn = Line.CreateBound(firstPoint, secondPoint);
            }

            return currentRidgeToReturn;
        }

        private Line ShortenStartRidgeBySetBack(Line currentRidgeLine, double amount)
        {
            Line currentRidgeToReturn = currentRidgeLine;

            if (currentRidgeLine.ApproximateLength > (amount + Utils.Utils.ConvertM.cmToFeet(1.5)))
            {
                XYZ firstPoint = currentRidgeLine.GetEndPoint(0).Add(currentRidgeLine.Direction.Multiply(amount));

                currentRidgeToReturn = Line.CreateBound(firstPoint, currentRidgeToReturn.GetEndPoint(1));
            }

            return currentRidgeToReturn;
        }

        private Line ShortenEndRidgeBySetBack(Line currentRidgeLine, double amount)
        {
            Line currentRidgeToReturn = currentRidgeLine;

            if (currentRidgeLine.ApproximateLength > (amount + Utils.Utils.ConvertM.cmToFeet(1.5)))
            {
                XYZ secondPoint = currentRidgeLine.GetEndPoint(1).Add(currentRidgeLine.Direction.Negate().Multiply(amount));

                currentRidgeToReturn = Line.CreateBound(currentRidgeToReturn.GetEndPoint(0), secondPoint);
            }

            return currentRidgeToReturn;
        }

    }
}
