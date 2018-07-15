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
                var publish = new AutoPublish(args[0], args[1], args[2], args[3]);
                publish.Publish();
                //var publish = new PsPublish();
                //publish.Publish(args[0], args[1]);
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
