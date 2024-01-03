using System;
using System.Collections.Generic;
using System.Linq;

namespace NetamiteTest {
    internal class ConsoleMenu {
        private List<Option> options;
        private int index = 0;
        public ConsoleMenu(List<Option> options) {
            this.options = options;
        }

        public void Open() {
            // Write the menu out
            WriteMenu();

            // Store key info in here
            ConsoleKeyInfo keyinfo;
            do {
                keyinfo = Console.ReadKey();

                // Handle each key input (down arrow will write the menu again with a different selected item)
                if(keyinfo.Key == ConsoleKey.DownArrow) {
                    if(index + 1 < options.Count) {
                        index++;
                    } else {
                        index = 0;
                    }
                    WriteMenu();
                }
                if(keyinfo.Key == ConsoleKey.UpArrow) {
                    if(index - 1 >= 0) {
                        index--;
                    } else {
                        index = options.Count - 1;
                    }
                    WriteMenu();
                }
                // Handle different action for the option
                if(keyinfo.Key == ConsoleKey.Enter) {
                    options[index].Selected.Invoke();
                    index = 0;

                    Console.WriteLine("Press ENTER to return to the menu.");
                    Console.ReadKey();
                    WriteMenu();
                }
            }
            while(keyinfo.Key != ConsoleKey.X);

            Console.ReadKey();
        }

        private void WriteMenu() {
            Console.Clear();

            int length = options.Aggregate("", (max, cur) => max.Length > cur.Name.Length ? max : cur.Name).Length;

            int offset_left = (Console.BufferWidth - length - 6) / 2;

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("".PadLeft(offset_left) + "╔" + "".PadRight(length + 6, '═') + "╗");

            foreach(Option option in options) {
                Console.Write("".PadLeft(offset_left) + "║ ");

                if(option == options[index]) {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("> ");
                } else {
                    Console.Write("  ");
                }

                int spacing = length - option.Name.Length;
                string name = option.Name.PadLeft((int) Math.Floor(spacing / 2f) + option.Name.Length, ' ').PadRight(spacing + option.Name.Length, ' ');

                Console.Write(name);
                if(option == options[index]) Console.Write(" < ");
                else Console.Write("   ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("║");
            }
            Console.WriteLine("".PadLeft(offset_left) + "╚" + "".PadRight(length + 6, '═') + "╝");
        }

        public class Option {
            public string Name { get; }
            public Action Selected { get; }

            public Option(string name, Action selected) {
                Name = name;
                Selected = selected;
            }
        }
    }
}
