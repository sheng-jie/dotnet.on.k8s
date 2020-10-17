using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace K8S.NET.CityApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CityController : ControllerBase
    {
        private static readonly string[] Cities = new[]
        {
            "Beijing", "Shanghai", "Shenzhen", "Wuhan", "Hong Kong", "London"
        };

        [HttpGet]
        public ActionResult Get()
        {
            var rng = new Random();
            return Ok(Cities[rng.Next(Cities.Length)]);
        }
    }
}
