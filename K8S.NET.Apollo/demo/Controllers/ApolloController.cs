using System;
using System.Collections.Generic;
using System.Text.Json;
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

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("key")]
        public IActionResult GetLogLevelSection()
        {
            var key = "Logging:LogLevel";
            var val = _configuration.GetSection(key).Get<LoggingOptions>();
            return Ok($"{key}:{JsonSerializer.Serialize(val)}");
        }

        [HttpGet("key")]
        public IActionResult GetString(string key)
        {
            var val = _configuration.GetValue<string>(key);
            return Ok($"{key}:{val}");
        }


        [HttpGet("key")]
        public IActionResult GetConnectionStrings(string key)
        {

            var val = _configuration.GetConnectionString(key);
            return Ok($"{key}:{val}");
        }
    }

    public class LoggingOptions : Dictionary<string, string>
    {

    }




}
