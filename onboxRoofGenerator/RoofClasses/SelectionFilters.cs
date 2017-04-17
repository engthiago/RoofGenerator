using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace onboxRoofGenerator.RoofClasses
{
    class SelectionFilters
    {
        internal class FootPrintRoofSelFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                if ((elem as FootPrintRoof) != null)
                    return true;
                else
                    return false;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
