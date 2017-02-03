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

            IList<EdgeInfo> Ridges = currentRoofEdgeInfoList.Where(ed => ed.roofLineType == RoofLineType.Ridge || ed.roofLineType == RoofLineType.RidgeSinglePanel).ToList();

            //Element w = doc.GetElement(uidoc.Selection.PickObject(ObjectType.Element));

            //ICollection<ElementId> col = w.GetGeneratingElementIds(w.get_Geometry(new Options()));



            //Element w = doc.GetElement(uidoc.Selection.PickObject(ObjectType.Element));

            //ICollection<ElementId> joinedElements = new System.Collections.ObjectModel.Collection<ElementId>();
            //GeometryElement geometryElement = w.get_Geometry(new Options());
            //foreach (GeometryObject geometryObject in geometryElement)
            //{
            //    if (geometryObject is Solid)
            //    {
            //        Solid solid = geometryObject as Solid;
            //        foreach (Face face in solid.Faces)
            //        {
            //            // for each face, find the other elements that generated the geometry of that face
            //            ICollection<ElementId> generatingElementIds = w.GetGeneratingElementIds(face);

            //            generatingElementIds.Remove(w.Id); // remove the originally selected wall, leaving only other elements joined to it
            //            foreach (ElementId id in generatingElementIds)
            //            {
            //                if (!(joinedElements.Contains(id)))
            //                    joinedElements.Add(id); // add each wall joined to this face to the overall collection 
            //            }
            //        }
            //    }
            //}

            //uidoc.Selection.SetElementIds(joinedElements); // select all of the joined elements
            //uidoc.RefreshActiveView();

            using (Transaction t = new Transaction(doc, "Place bounding points"))
            {
                t.Start();
                foreach (EdgeInfo currentEdgeInfo in Ridges)
                {

                    XYZ currentPoint = currentEdgeInfo.edge.Evaluate(0.5);
                    //foreach (Edge currentInternalEdge in currentEdgeInfo.relatedRidgeEaves)
                    //{
                    //    XYZ currentMidEave = currentInternalEdge.Evaluate(0.5);
                    //    doc.Create.NewFamilyInstance(currentMidEave, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    //}

                    doc.Create.NewFamilyInstance(currentPoint, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                }
                t.Commit();
            }


            return Result.Succeeded;
        }
    }
}
