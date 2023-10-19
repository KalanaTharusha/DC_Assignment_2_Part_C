using Client_DLL;
using Microsoft.AspNetCore.Mvc;
using Web_Server.Models;

namespace Web_Server.Controllers
{
    public class ClientController : Controller
    {
        [Route("api/client/all")]
        [HttpGet]
        public List<Client> GetClients()
        {
            return ClientList.clientList;
        }

        [Route("api/client/register")]
        [HttpPost]
        public void Register([FromBody] Client client)
        {
            ClientList.clientList.Add(client);
        }

        [Route("api/client/remove/{no}")]
        [HttpDelete]
        public void Remove(int no)
        {
            ClientList.clientList.RemoveAt(no);
        }
    }
}
