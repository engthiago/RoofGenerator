using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace onboxRoofGenerator
{
    class DEBUG
    {
        static public void CreateDebugPoint(Document doc, XYZ pointLocation)
        {

#if DEBUG
            if (doc != null && pointLocation != null)
            {
                Element e = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(type => type.Name.Contains("DebugPoint")).FirstOrDefault();
                if (e != null)
                {
                    using (Transaction t = new Transaction(doc, "ReferencePoint"))
                    {
                        t.Start();

                        FamilySymbol fs = e as FamilySymbol;

                        if (fs != null)
                        {
                            doc.Create.NewFamilyInstance(pointLocation, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        }

                        t.Commit();
                    }
                } 
            }
#endif

        }


        static public void CreateDebugFlattenLine(Document doc, Line currentLine)
        {
            #region DEBUG ONLY
#if DEBUG
            if (doc != null && currentLine != null)
            {
                using (Transaction ta = new Transaction(doc, "Line test"))
                {
                    ta.Start();

                    currentLine = currentLine.Flatten();
                    Frame fr = new Frame(currentLine.Evaluate(0.5, true), XYZ.BasisX, XYZ.BasisY, XYZ.BasisZ);
                    SketchPlane skp = SketchPlane.Create(doc, Plane.Create(fr));
                    doc.Create.NewModelCurve(currentLine, skp);


                    ta.Commit();
                } 
            }
#endif
            #endregion
        }

    }
}
