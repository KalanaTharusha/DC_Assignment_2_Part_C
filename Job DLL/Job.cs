using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Job_DLL
{
    public class Job
    {
        public int JobId { get; set; }
        public string PythonScript { get; set; }
        public string Result { get; set; }
        public string Status { get; set; }

    }
}
