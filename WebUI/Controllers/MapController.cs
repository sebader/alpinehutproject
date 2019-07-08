using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Models;
using WebUI.Models;

namespace WebUI.Cotrollers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MapController : ControllerBase
    {
        private readonly AlpinehutsDbContext _context;
        private readonly ILogger _logger;

        public MapController(AlpinehutsDbContext context, ILogger<MapController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Map?llLat=1.2&llLon=89.23&urLat=2.3&urLon=78.3[&dateFilter=2019-11-16]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Hut>>> GetHuts([FromQuery] double llLat, [FromQuery] double llLon, [FromQuery] double urLat, [FromQuery] double urLon, [FromQuery] DateTime? dateFilter)
        {
            var huts = _context.Huts.Where(h => h.Latitude > llLat && h.Latitude < urLat && h.Longitude > llLon && h.Longitude < urLon);
            if (dateFilter != null)
            {
                huts = huts.Where(h => h.Enabled == true && h.Availability.Any(a => a.Date == dateFilter && a.FreeRoom > 0));
            }           
            //var result = huts.Select(hut => new MapPlotHut { Id = hut.Id, Name = hut.Name, Details = hut.Region, Latitude = (double)hut.Latitude, Longitude = (double)hut.Longitude });   
            if(huts.Count() > 0)
            {
                _logger.LogInformation($"GetHuts returned {huts.Count()} huts");
            }
            return await huts.ToListAsync();
        }
    }
}
