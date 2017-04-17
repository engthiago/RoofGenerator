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

        public class ConvertM
        {
            static public double mToFeet(double x)
            {
                return x * 3.28084;
            }
            static public double mmToFeet(double x)
            {
                return x / 304.8;
            }
            static public double feetTomm(double x)
            {
                return x / 0.00328084;
            }
            static public double cmToFeet(double x)
            {
                return x / 30.48;
            }
            static public double feetToCm(double x)
            {
                return x / 0.0328084;
            }
            static public double feetToM(double x)
            {
                return x / 3.28084;
            }
            static public double degreesToRadians(double x)
            {
                return (x * (Math.PI / 180));
            }
            static public double radiansToDegrees(double x)
            {
                return (x * (180 / Math.PI));
            }
        }

    }
}
