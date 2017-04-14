using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace onboxRoofGenerator.Utils
{
    class Utils
    {
        static public Tuple<int, double> EstabilishIterations(double length, double maxPointDistInFeet)
        {
            int numberOfInteractions = 0;
            double increaseAmount = 0;

            if (maxPointDistInFeet > length)
                numberOfInteractions = 1;
            else
                numberOfInteractions = (int)(Math.Ceiling(length / maxPointDistInFeet));

            increaseAmount = length / numberOfInteractions;

            return new Tuple<int, double>(numberOfInteractions, increaseAmount);
        }
    }
}
