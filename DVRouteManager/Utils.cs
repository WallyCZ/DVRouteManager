using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DVRouteManager
{
    static class Utils
    {
        public static object NextEnumItem(object currentEnumItem)
        {
            if (!currentEnumItem.GetType().IsEnum)
                throw new ArgumentException(String.Format("Argument is not an Enum"));
            Array Arr = Enum.GetValues(currentEnumItem.GetType());
            int j = Array.IndexOf(Arr, currentEnumItem) + 1;
            return (Arr.Length == j) ? Arr.GetValue(0) : Arr.GetValue(j);
        }

        public static object PreviousEnumItem(object currentEnumItem)
        {
            if (!currentEnumItem.GetType().IsEnum)
                throw new ArgumentException(String.Format("Argument is not an Enum"));
            Array Arr = Enum.GetValues(currentEnumItem.GetType());
            int j = Array.IndexOf(Arr, currentEnumItem) - 1;
            return (j == -1) ? Arr.GetValue(Arr.Length - 1) : Arr.GetValue(j);
        }

        public static (Bogie aBoogie, Bogie bBogie) GetBogiesWithMaxDistance(TrainCar aCar, TrainCar bCar)
        {
            float maxDistanceSquare = 0.0f;

            Bogie aBoogie = null;
            Bogie bBoogie = null;

            foreach (var a in aCar.Bogies)
            {
                foreach (var b in bCar.Bogies)
                {
                    float lengthSquare = (a.transform.position - b.transform.position).sqrMagnitude;
                    if(lengthSquare > maxDistanceSquare)
                    {
                        maxDistanceSquare = lengthSquare;
                        aBoogie = a;
                        bBoogie = b;
                    }
                }
            }

            return (aBoogie, bBoogie);
        }



    }
}
