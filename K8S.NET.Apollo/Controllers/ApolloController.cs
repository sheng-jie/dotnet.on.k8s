using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace K8S.NET.Apollo.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class ApolloController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult GetString(string key)
        {
            throw new NotImplementedException();
        }

        public IActionResult GetSection(string sectionKey)
        {
            throw new NotImplementedException();
        }

        public IActionResult GetJson(string jsonKey)
        {
            throw new NotImplementedException();
        }
    }
}
