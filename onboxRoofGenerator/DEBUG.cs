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
            using (Transaction t = new Transaction(doc, "ReferencePoint"))
            {
                t.Start();
                Element e = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel).WhereElementIsElementType().Where(type => type.Name.Contains("DebugPoint")).FirstOrDefault();
                if (e != null)
                {
                    FamilySymbol fs = e as FamilySymbol;

                    if (fs != null)
                    {
                        doc.Create.NewFamilyInstance(pointLocation, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    }
                }
                t.Commit();
            }
#endif

        }
    }
}
