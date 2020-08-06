using System;

namespace ConsoleApp1
{
    class Test
    {
        //private static void Test1()
        //{
        //    Random r = new Random();
        //    for (int i = 0; i < 10000; i++)
        //    {
        //        var tri = new BothTri(
        //            RandomVec3(r),
        //            RandomVec3(r),
        //            RandomVec3(r));
        //    }
        //}

        private static Vec3 RandomVec3(Random r)
        {
            var theta = Math.PI * 2 * r.NextDouble();
            var phi = Math.PI * r.NextDouble();
            return new Vec3(
                Math.Cos(theta) * Math.Sin(phi),
                Math.Sin(theta) * Math.Sin(phi),
                Math.Cos(phi));
        }
    }
}
