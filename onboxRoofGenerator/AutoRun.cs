using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

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

            IList<EdgeInfo> currentRoofEdgeInfoList = Support.GetRoofEdgeInfoList(currentFootPrintRoof);


            FamilySymbol fs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(type => type.Name.Contains("DebugPoint2")).FirstOrDefault() as FamilySymbol;

            if (fs == null)
            {
                message = "The family type to place the points were not found, please open the sample file or copy the family to this project";
                return Result.Failed;
            }

            IList<EdgeInfo> Ridges = currentRoofEdgeInfoList.Where(ed => ed.RoofLineType == RoofLineType.Ridge || ed.RoofLineType == RoofLineType.RidgeSinglePanel).ToList();

            EdgeInfo currentRidge = Ridges[0];

            foreach (EdgeInfo currentEndCondition in currentRidge.GetEndConditions(1))
            {
                using (Transaction t = new Transaction(doc, "Place bounding points"))
                {
                    t.Start();
                    TaskDialog.Show("Results", currentEndCondition.RoofLineType.ToString());

                    XYZ currentPoint = currentRidge.Curve.GetEndPoint(1);
                    XYZ currenta = currentEndCondition.Curve.Evaluate(0.5, true);
                    doc.Create.NewFamilyInstance(currentPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    doc.Create.NewFamilyInstance(currenta, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    t.Commit();
                }
            }


            //using (Transaction t = new Transaction(doc, "Place bounding points"))
            //{
            //    t.Start();
            //    foreach (EdgeInfo currentEdgeInfo in Ridges)
            //    {
            //        XYZ currentPoint = currentEdgeInfo.ProjectRidgePointOnEave(2);
            //        doc.Create.NewFamilyInstance(currentPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

            //        XYZ currentThirdPoint = currentEdgeInfo.ProjectRidgePointOnOverhang(2);
            //        doc.Create.NewFamilyInstance(currentThirdPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

            //    }
            //    t.Commit();
            //}


            return Result.Succeeded;
        }
    }
}
