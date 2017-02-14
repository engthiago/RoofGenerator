using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace onboxRoofGenerator
{
    [Transaction(TransactionMode.Manual)]
    class AutoRun : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            if ((uidoc.ActiveView as View3D) == null)
            {
                message = "Por favor, rode este comando em uma vista 3d";
                return Result.Failed;
            }

            Reference currentReference = uidoc.Selection.PickObject(ObjectType.Element, new FootPrintRoofSelFilter(), "Selecione um telhado.");
            FootPrintRoof currentFootPrintRoof = doc.GetElement(currentReference) as FootPrintRoof;

            //TODO if the footprint contains something other than lines (straight lines) warn the user and exit


            FamilySymbol fs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(type => type.Name.Contains("DebugPoint2")).FirstOrDefault() as FamilySymbol;

            if (fs == null)
            {
                message = "The family type to place the points were not found, please open the sample file or copy the family to this project";
                return Result.Failed;
            }

            IList<EdgeInfo> currentRoofEdgeInfoList = new List<EdgeInfo>();

            using (Transaction t2 = new Transaction(doc, "Points"))
            {
                t2.Start();
                currentRoofEdgeInfoList = Support.GetRoofEdgeInfoList(currentFootPrintRoof, false);

                Element tTypeElement = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Where(fsy => fsy is TrussType).ToList().FirstOrDefault();
                TrussType tType = tTypeElement as TrussType;

                foreach (EdgeInfo currentEdgeInfo in currentRoofEdgeInfoList)
                {
                    if (currentEdgeInfo.RoofLineType == RoofLineType.RidgeSinglePanel || currentEdgeInfo.RoofLineType == RoofLineType.Ridge)
                    {
                        double distance = 0;
                        int numPoints = 0;
                        EstabilishIteractionPoints(currentEdgeInfo.Curve, 8.22, out numPoints, out distance);


                        for (int i = 0; i <= numPoints; i++)
                        {
                            CurveArray c1 = null;
                            CurveArray c2 = null;
                            TrussInfo currentTrussInfo = null;
                            double currentParam = i * distance;

                            if (currentEdgeInfo.CanBuildTrussAtRidge(currentParam, out currentTrussInfo, out c1, out c2))
                            {
                                SketchPlane stkP = SketchPlane.Create(doc, currentEdgeInfo.CurrentRoof.LevelId);

                                double levelHeight = currentEdgeInfo.GetCurrentRoofHeight();

                                XYZ firstPoint = new XYZ(currentTrussInfo.FirstPoint.X, currentTrussInfo.FirstPoint.Y, levelHeight);
                                XYZ secondPoint = new XYZ(currentTrussInfo.SecondPoint.X, currentTrussInfo.SecondPoint.Y, levelHeight);
                                Truss currentTruss = Truss.Create(doc, tType.Id, stkP.Id, Line.CreateBound(firstPoint, secondPoint));

                                currentTruss.get_Parameter(BuiltInParameter.TRUSS_HEIGHT).Set(currentTrussInfo.Height);

                            }
                        }
                    }
                }

                t2.Commit();
            }


            IList<EdgeInfo> Ridges = currentRoofEdgeInfoList.Where(ed => ed.RoofLineType == RoofLineType.Ridge || ed.RoofLineType == RoofLineType.RidgeSinglePanel).ToList();

            //EdgeInfo currentRidge = Ridges[0];

            //foreach (EdgeInfo currentEndCondition in currentRidge.GetEndConditions(1))
            //{
            //    using (Transaction t = new Transaction(doc, "Place bounding points"))
            //    {
            //        t.Start();
            //        TaskDialog.Show("Results", currentEndCondition.RoofLineType.ToString());

            //        XYZ currentPoint = currentRidge.Curve.GetEndPoint(1);
            //        XYZ currenta = currentEndCondition.Curve.Evaluate(0.5, true);
            //        doc.Create.NewFamilyInstance(currentPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            //        doc.Create.NewFamilyInstance(currenta, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            //        t.Commit();
            //    }
            //}


            //using (Transaction t = new Transaction(doc, "Place bounding points"))
            //{
            //    t.Start();
            //    foreach (EdgeInfo currentEdgeInfo in currentRoofEdgeInfoList)
            //    {
            //        XYZ currentPoint = currentEdgeInfo.Curve.Evaluate(0.5, true);
            //        doc.Create.NewFamilyInstance(currentPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

            //        //XYZ currentPoint = currentEdgeInfo.ProjectRidgePointOnEave(2);
            //        //doc.Create.NewFamilyInstance(currentPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

            //        //XYZ currentThirdPoint = currentEdgeInfo.ProjectRidgePointOnOverhang(2);
            //        //doc.Create.NewFamilyInstance(currentThirdPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

            //    }
            //    t.Commit();
            //}


            return Result.Succeeded;
        }

        private void EstabilishIteractionPoints(Curve currentCurve, double maxPointDistInFeet, out int numberOfInteractions, out double increaseAmount)
        {
            double currentCurveLength = currentCurve.ApproximateLength;

            if (maxPointDistInFeet > currentCurveLength)
                numberOfInteractions = 1;
            else
                numberOfInteractions = (int)(Math.Ceiling(currentCurveLength / maxPointDistInFeet));

            increaseAmount = currentCurveLength / numberOfInteractions;
        }
    }

    [Transaction(TransactionMode.Manual)]
    class TwoLine : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;

            Element el1 = doc.GetElement(sel.PickObject(ObjectType.Element));
            Element el2 = doc.GetElement(sel.PickObject(ObjectType.Element));


            Line l1 = (el1.Location as LocationCurve).Curve as Line;
            Line l2 = (el2.Location as LocationCurve).Curve as Line;

            IntersectionResultArray intArr = new IntersectionResultArray();
            SetComparisonResult compResult = l1.Intersect(l2, out intArr);


            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    class DetectEavePoints : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            if ((uidoc.ActiveView as View3D) == null)
            {
                message = "Por favor, rode este comando em uma vista 3d";
                return Result.Failed;
            }

            Reference currentReference = uidoc.Selection.PickObject(ObjectType.Element, new FootPrintRoofSelFilter(), "Selecione um telhado.");
            FootPrintRoof currentFootPrintRoof = doc.GetElement(currentReference.ElementId) as FootPrintRoof;

            IList<EdgeInfo> currentRoofEdgeInfoList = Support.GetRoofEdgeInfoList(currentFootPrintRoof, false);

            //TODO check for roofs with single panel why is the ridge returning undefined when we pick the bottom faces

            using (Transaction t2 = new Transaction(doc, "Points"))
            {
                t2.Start();
                foreach (EdgeInfo currentEdgeInfo in currentRoofEdgeInfoList)
                {
                    if (currentEdgeInfo.RoofLineType == RoofLineType.Ridge || currentEdgeInfo.RoofLineType == RoofLineType.RidgeSinglePanel)
                    {
                        IList<XYZ> currentPoints = currentEdgeInfo.ProjectSupportPointsOnRoof(3);
                        FamilySymbol fs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(type => type.Name.Contains("DebugPoint2")).FirstOrDefault() as FamilySymbol;
                        foreach (XYZ currentPoint in currentPoints)
                        {
                            doc.Create.NewFamilyInstance(currentPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        }
                    }
                }
                t2.Commit();
            }


            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    class DetectSingleEdge : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            if ((uidoc.ActiveView as View3D) == null)
            {
                message = "Por favor, rode este comando em uma vista 3d";
                return Result.Failed;
            }

            Reference currentReference = uidoc.Selection.PickObject(ObjectType.Edge);
            FootPrintRoof currentFootPrintRoof = doc.GetElement(currentReference.ElementId) as FootPrintRoof;
            Edge edge = currentFootPrintRoof.GetGeometryObjectFromReference(currentReference) as Edge;

            IList<PlanarFace> pfaces = new List<PlanarFace>();
            Support.IsListOfPlanarFaces(HostObjectUtils.GetBottomFaces(currentFootPrintRoof).Union(HostObjectUtils.GetTopFaces(currentFootPrintRoof)).ToList()
                , currentFootPrintRoof, out pfaces);

            using (Transaction t2 = new Transaction(doc, "Points"))
            {
                t2.Start();
                EdgeInfo currentInfo = Support.GetCurveInformation(currentFootPrintRoof, edge.AsCurve(), pfaces);
                TaskDialog.Show("fac", currentInfo.RoofLineType.ToString());

                XYZ topChordPoint = currentInfo.GetTrussTopPoint(3);
                FamilySymbol fs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(type => type.Name.Contains("DebugPoint2")).FirstOrDefault() as FamilySymbol;
                doc.Create.NewFamilyInstance(topChordPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                //IList<EdgeInfo> conditions = currentInfo.GetEndConditions(0);

                //foreach (EdgeInfo currentCondi in conditions)
                //{
                //    TaskDialog.Show("condition", currentCondi.RoofLineType.ToString());
                //}
                t2.Commit();
            }




            //using (Transaction t2 = new Transaction(doc, "Points"))
            //{
            //    t2.Start();
            //    FamilySymbol fs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(type => type.Name.Contains("DebugPoint2")).FirstOrDefault() as FamilySymbol;
            //    doc.Create.NewFamilyInstance(currentInfo.Curve.GetEndPoint(0), fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            //    foreach (EdgeInfo currentCondi in conditions)
            //    {
            //        TaskDialog.Show("condition", currentCondi.RoofLineType.ToString());
            //        PlanarFace pfce = currentCondi.GetRelatedPanels()[0] as PlanarFace;
            //        Plane pl = new Plane(pfce.FaceNormal, pfce.Origin);
            //        SketchPlane skp = SketchPlane.Create(doc, pl);
            //        doc.Create.NewModelCurve(currentCondi.Curve, skp);
            //    }
            //    t2.Commit();
            //}


            return Result.Succeeded;
        }
    }
}
