using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DVRouteManager.Extensions
{
    public static class TrainsetExtension
    {
        public static float Length(this Trainset trainset)
        {
            return trainset.cars.Sum(c => c.logicCar.length);
        }
    }
}
