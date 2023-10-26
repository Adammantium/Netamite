using Netamite.Converter;
using Netamite.Datatype;
using System;

namespace NetamiteTest {
    internal class ValueConverterTest {

        private enum TestEnum : byte {
            ONE = 1,
            TWO = 2,
            THREE = 3
        }

        public static void DoTest() {
            TestConvert(TestEnum.TWO);
            TestConvert(true);
            TestConvert(1f);
            TestConvert(1.1);
            TestConvert((byte)   10);
            TestConvert((short)  10);
            TestConvert((ushort) 10);
            TestConvert((int)    10);
            TestConvert((uint)   10);
            TestConvert((long)   10);
            TestConvert((ulong)  10);

            TestConvert('T');
            TestConvert("Test");

            TestConvert(new Vector3(10.5f, 13.6f, -10.1f));
            TestConvert(new Quaternion(0.002f, 0.533f, -0.178f, -0.827f));

            TestConvert(Color.yellow);
        }

        public static void TestConvert(object value) {
            Console.WriteLine("> " + value.GetType().FullName);

            Console.WriteLine("\tIN: " + value);

            try {
                byte[] compress_data = DataConverter.GetBytes(value, true);
                Console.Write($"\tCOMP ({compress_data.Length}): ");
                foreach(byte b in compress_data)
                    Console.Write(Convert.ToString(b, 2).PadLeft(8, '0') + " ");
                Console.WriteLine();
                Console.WriteLine("\tOUT: " + DataConverter.FromBytes(value.GetType(), compress_data, 0, true));


                byte[] full_data = DataConverter.GetBytes(value, false);
                Console.Write($"\tFULL ({full_data.Length}): ");
                foreach(byte b in full_data)
                    Console.Write(Convert.ToString(b, 2).PadLeft(8, '0') + " ");
                Console.WriteLine();
                Console.WriteLine("\tOUT: " + DataConverter.FromBytes(value.GetType(), full_data, 0, false));


                Console.WriteLine();
            } catch(MissingDataConverterException e) {
                Console.WriteLine("\t" + e.Message);
            }
        }

    }
}
