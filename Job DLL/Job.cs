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
        public JobStatus Status { get; set; }
        public byte[] Hash { get; set; }

        public enum JobStatus{
            ToDo,
            InProgress,
            Completed,
            Displayed
        }

    }
}
