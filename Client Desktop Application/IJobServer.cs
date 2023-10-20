using Job_DLL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Client_Desktop_Application
{
    [ServiceContract]
    public interface IJobServer
    {
        [OperationContract]
        Job RequestJob();
        [OperationContract]
        string Ping();

        [OperationContract]
        void SubmitResult(int jobId, string result);
    }
}
