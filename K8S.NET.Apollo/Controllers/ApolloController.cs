using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace K8S.NET.Apollo.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class ApolloController : Controller
    {
        private readonly IConfiguration _configuration;
        public ApolloController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult GetString(string key)
        {
            var val = _configuration.GetValue<string>(key);
            return Ok($"{key}:{val}");
        }

        [HttpGet]
        public IActionResult GetSection(string key)
        {
            var loggingObj = new LoggingOptions();

            _configuration.GetSection(key).Bind(loggingObj);

            return Ok(loggingObj);
        }

        [HttpGet]
        public IActionResult GetConnectionStrings(string key)
        {

            var val = _configuration.GetConnectionString(key);
            return Ok($"{key}:{val}");
        }
    }




}
