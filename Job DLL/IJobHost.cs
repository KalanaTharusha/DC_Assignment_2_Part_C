using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Job_DLL
{
    [ServiceContract]
    public interface IJobHost
    {
        [OperationContract]
        Job RequestJob();
        [OperationContract]
        string Ping();

        [OperationContract]
        void SubmitResult(int jobId, string result);
    }
}
