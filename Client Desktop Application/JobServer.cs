using Job_DLL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client_Desktop_Application
{
    public class JobServer : IJobServer
    {
        public string Ping()
        {
            return "Message from server";
        }
        public Job RequestJob()
        {
            foreach (var job in JobList.Jobs)
            {
                if (job.Status.Equals(Job.JobStatus.ToDo))
                {
                    job.Status = Job.JobStatus.InProgress;
                    return job;
                }
            }
            return null;
        }

        public void SubmitResult(int jobId, string result)
        {
            JobList.Jobs.FirstOrDefault(j => j.JobId == jobId).Result = result;
            JobList.Jobs.FirstOrDefault(j => j.JobId == jobId).Status = Job.JobStatus.Completed;
        }
    }
}
