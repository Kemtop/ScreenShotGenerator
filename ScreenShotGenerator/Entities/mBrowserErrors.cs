using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Entities
{
    public class mBrowserErrors
    {
        public int Id { get; set; }
        public int level { get; set; }
        public string message { get; set; }
        public string url { get; set; }
        public string filename { get; set; }
        public DateTime created { get; set; }
    }
}
