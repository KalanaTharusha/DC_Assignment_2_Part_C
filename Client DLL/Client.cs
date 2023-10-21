using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client_DLL
{
    public class Client
    {
        public int ClientId { get; set; }
        public string IPAddress { get; set; }
        public int Port { get; set; }
        public string Status { get; set; }
        public int JobsCompleted { get; set; }
    }
}
