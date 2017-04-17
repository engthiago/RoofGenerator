using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace onboxRoofGenerator.RoofClasses
{
    class TrussInfo
    {
        public XYZ FirstPoint { get; set; }
        public XYZ SecondPoint { get; set; }
        public double Height { get; set; }
        public CurveArray TopChords { get; set; }
        public CurveArray BottomChords { get; set; }


        internal Line FootPrintLine { get { return GetFootPrinLine(); } }

        public TrussInfo(XYZ firstPoint, XYZ secondPoint, double height)
        {
            FirstPoint = firstPoint;
            SecondPoint = secondPoint;
            Height = height;
        }

        private Line GetFootPrinLine()
        {
            if (FirstPoint != null && SecondPoint != null)
                return Line.CreateBound(FirstPoint, SecondPoint);

            return null;
        }

        static public TrussInfo BuildTrussAtRidge(XYZ currentPointOnRidge, EdgeInfo currentEdgeInfo)
        {
            TrussInfo trussInfo = null;
        
            XYZ currentTopPoint = currentEdgeInfo.GetTrussTopPoint(currentPointOnRidge);
            //If we cant get the point that means that the projection failed
            if (currentTopPoint == null)
                return trussInfo;

            IList<XYZ> currentSupportPoints = currentEdgeInfo.ProjectSupportPointsOnRoof(currentPointOnRidge);
            if (currentSupportPoints.Count == 0)
            {
                if (currentEdgeInfo.RoofLineType != RoofLineType.Ridge)
                    return trussInfo;

                IList<EdgeInfo> endConditionList = currentEdgeInfo.GetEndConditions(1);

                if (endConditionList.Count != 2)
                    return trussInfo;

                EdgeInfo edge0 = endConditionList[0];
                EdgeInfo edge1 = endConditionList[1];

                if ((edge0.RoofLineType != RoofLineType.Valley && edge0.RoofLineType != RoofLineType.Gable) || 
                    edge1.RoofLineType != RoofLineType.Valley && edge1.RoofLineType != RoofLineType.Gable)
                    return trussInfo;

                Line line0 = edge0.Curve as Line;
                Line line1 = edge1.Curve as Line;

                if (line0 == null || line1 == null)
                    return trussInfo;

                double height = currentEdgeInfo.GetCurrentRoofHeight();

                Line currentRigeLine = currentEdgeInfo.Curve as Line;
                XYZ rigePointFlatten = new XYZ(currentTopPoint.X, currentTopPoint.Y, height);
                XYZ intersectingPointValleyOrGable0 = GeometrySupport.GetRoofIntersectionFlattenLines(currentRigeLine, currentTopPoint, line0, height);
                XYZ intersectingPointValleyOrGable1 = GeometrySupport.GetRoofIntersectionFlattenLines(currentRigeLine, currentTopPoint, line1, height);

                if (intersectingPointValleyOrGable0 == null || intersectingPointValleyOrGable1 == null)
                    return trussInfo;

                XYZ supportPoint0 = currentEdgeInfo.GetSupportPoint(intersectingPointValleyOrGable0, currentRigeLine.Direction, 20);
                XYZ supportPoint1 = currentEdgeInfo.GetSupportPoint(intersectingPointValleyOrGable1, currentRigeLine.Direction, 20);

                if (supportPoint0 == null || supportPoint1 == null)
                    return trussInfo;

                trussInfo = GeometrySupport.GetTrussInfo(currentTopPoint, supportPoint0, supportPoint1);

                if (trussInfo == null)
                    return trussInfo;

                #region When we get the points from a valley we need to adjust the height of the truss

                ReferenceIntersector refIntersect = new ReferenceIntersector(currentEdgeInfo.CurrentRoof.Id, FindReferenceTarget.Element, currentEdgeInfo.CurrentRoof.Document.ActiveView as View3D);
                ReferenceWithContext refContext = refIntersect.FindNearest(supportPoint0, XYZ.BasisZ);

                if (refContext != null)
                {
                    double dist = supportPoint0.DistanceTo(new XYZ(supportPoint0.X, supportPoint0.Y, refContext.GetReference().GlobalPoint.Z));
                    trussInfo.Height = trussInfo.Height - dist;
                }

                #endregion

                return trussInfo;

            }
            else if (currentSupportPoints.Count == 1)
            {

                if (currentEdgeInfo.RoofLineType != RoofLineType.RidgeSinglePanel)
                    return trussInfo;

                XYZ projectedPointOnEave = currentSupportPoints[0];
                XYZ projectedPointOnRidge = new XYZ(currentTopPoint.X, currentTopPoint.Y, projectedPointOnEave.Z);

                double height = currentTopPoint.DistanceTo(projectedPointOnRidge);

                trussInfo = new TrussInfo(projectedPointOnEave, projectedPointOnRidge, height);
                trussInfo.TopChords.Append(Line.CreateBound(currentTopPoint, projectedPointOnEave));
                trussInfo.BottomChords.Append(Line.CreateBound(projectedPointOnRidge, projectedPointOnEave));
                //Document doc = CurrentRoof.Document;
                //FamilySymbol fs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(type => type.Name.Contains("DebugPoint2")).FirstOrDefault() as FamilySymbol;
                //doc.Create.NewFamilyInstance(projectedPointOnEave, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                //doc.Create.NewFamilyInstance(projectedPointOnRidge, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                //doc.Create.NewFamilyInstance(currentTopPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                return trussInfo;

            }
            else if (currentSupportPoints.Count == 2)
            {
                if (currentEdgeInfo.RoofLineType != RoofLineType.Ridge)
                    return trussInfo;

                XYZ firstPointOnEave = currentSupportPoints[0];
                XYZ secondPointOnEave = currentSupportPoints[1];

                if (firstPointOnEave == null || secondPointOnEave == null)
                    return trussInfo;

                trussInfo = GeometrySupport.GetTrussInfo(currentTopPoint, firstPointOnEave, secondPointOnEave);

                if (trussInfo == null)
                    return trussInfo;
                //Document doc = CurrentRoof.Document;
                //FamilySymbol fs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(type => type.Name.Contains("DebugPoint2")).FirstOrDefault() as FamilySymbol;
                //doc.Create.NewFamilyInstance(firstPointOnEave, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                //doc.Create.NewFamilyInstance(secondPointOnEave, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                //doc.Create.NewFamilyInstance(currentTopPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                return trussInfo;

            }

            return trussInfo;
        }

    }
}
