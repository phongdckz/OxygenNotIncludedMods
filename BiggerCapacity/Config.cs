using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BiggerCapacity
{
    public class Config
    {
        public float StorageMultiplier = 4f;
        public float BatteryMultiplier = 2f;
        public float WireMultiplier = 1f;
        public float GeneratorMultiplier = 1f;
        public float OtherMultiplier = 1f;

        public Capacities capacity = new Capacities();

        public class Capacities
        {
            public int Locker = 20000;
            public int WoodStorage = 20000;
            public int RationBox = 150;
            public int Refrigerator = 100;
        }
    }
}
