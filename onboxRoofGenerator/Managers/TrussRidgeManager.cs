﻿using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using onboxRoofGenerator.RoofClasses;

namespace onboxRoofGenerator.Managers
{
    /// <summary>
    /// Class responsable for dealing with creation and dristribution of trusses on Ridges
    /// </summary>
    class TrussRidgeManager
    {
        //Distance from truss to truss, it will be initialized to ~8.2" or 2.5m
        double trussDistance;

        public TrussRidgeManager(double targetTrussDistance = 8.2020997375)
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

            Document doc = currentRoof.Document;
            if (doc == null)
                return currentRoofTrussList;

            if (currentRoofEdgeInfoList.Count == 0)
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

        /// <summary>
        /// Creates trusses along a specific ridge edge and stores them on a list of truss info
        /// </summary>
        /// <param name="currentRidgeEdgeInfo">The specific edge ridge to create trusses</param>
        /// <param name="doc">The target document to create the trusses</param>
        /// <param name="tType">The target truss type to be used on creation</param>
        /// <returns>The list of info of the created trusses</returns>
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
                        fs.Activate();
                        doc.Create.NewFamilyInstance(currentPointOnRidge, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
#endif
                    }
                    #endregion
                }
            }

            return trussInfoList;
        }




    }
}
