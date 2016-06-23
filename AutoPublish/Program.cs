using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AutoPublish
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var publish = new AutoPublish();
                publish.Publish();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.ReadLine();
            }
        }
    }
}
