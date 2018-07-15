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
            var publish = new AutoPublish(args[0], args[1], args[2], args[3], args[4]);
            publish.Publish();
            //var publish = new PsPublish();
            //publish.Publish(args[0], args[1]);
        }
    }
}
