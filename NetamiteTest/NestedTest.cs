using Netamite.Logging;
using NetamiteTest.Test;
using System;

namespace NetamiteTest {
    internal class NestedTest {
        public static void DoTest() {
            NestedPacket packet = new NestedPacket() { Name = "Main" };
            packet.nestedClass = new NestedPacket.NestedClass() { Name = "Nested Class 1" };
            packet.nestedClasses = new NestedPacket.NestedClass[] {
                new NestedPacket.NestedClass() { Name = "Nested Array Class 1" },
                new NestedPacket.NestedClass() { Name = "Nested Array Class 2" }
            };

            byte[] data = packet.GetData();
            Log.Debug(data.Length);
            foreach(byte b in data)
                Console.Write(Convert.ToString(b, 16).PadLeft(2, '0') + " ");
            Console.WriteLine();

            NestedPacket result = new NestedPacket();
            result.SetData(data);

            Console.WriteLine(result.ToString());
        }
    }
}
