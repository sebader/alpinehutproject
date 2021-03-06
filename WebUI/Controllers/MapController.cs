﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
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
        private readonly IStringLocalizer _localizer;

        public MapController(AlpinehutsDbContext context, ILogger<MapController> logger, IStringLocalizer<WebUI.SharedResources> localizer)
        {
            _context = context;
            _logger = logger;
            _localizer = localizer;
        }

        // GET: api/Map?llLat=1.2&llLon=89.23&urLat=2.3&urLon=78.3[&dateFilter=2019-11-16]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MapPlotHut>>> GetHuts([FromQuery] double? llLat, [FromQuery] double? llLon, [FromQuery] double? urLat, [FromQuery] double? urLon, [FromQuery] DateTime? dateFilter)
        {
            IQueryable<Hut> huts;
            if (llLat != null && llLon != null && urLat != null && urLon != null)
            {
                huts = _context.Huts.Where(h => h.Latitude > llLat && h.Latitude < urLat && h.Longitude > llLon && h.Longitude < urLon);
            }
            else
            {
                huts = _context.Huts.Where(h => h.Latitude != null && h.Longitude != null);
            }
            /*
            if (dateFilter != null)
            {
                huts = huts.Where(h => h.Enabled == true && h.Availability.Any(a => a.Date == dateFilter && a.FreeRoom > 0));
            }
            */

            _logger.LogInformation("Received map request for coordinates llLat={llLat}, llLon={llLon}, urLat={urLat}, urLon={urLon}", llLat, llLon, urLat, urLon);

            var result = huts.Select(hut => new MapPlotHut
            {
                Id = hut.Id,
                Name = hut.Name,
                Enabled = (bool)hut.Enabled,
                OnlineBookingLink = hut.Link,
                HutWebsiteLink = hut.HutWebsite,
                Latitude = (double)hut.Latitude,
                Longitude = (double)hut.Longitude,
                FreeBeds = dateFilter != null && hut.Enabled == true ? hut.Availability.Where(a => a.Date == dateFilter).Sum(a => (int)a.FreeRoom) : (int?)null,
                LastUpdated = hut.Availability.FirstOrDefault(a => a.Date >= DateTime.Today).LastUpdated ?? (DateTime)hut.LastUpdated,
                Rooms = dateFilter != null && hut.Enabled == true ? hut.Availability.Where(a => a.Date == dateFilter).Select(a => new RoomAvailability { BedCategoryId = a.BedCategoryId, FreeBeds = (int)a.FreeRoom, TotalBeds = (int)a.TotalRoom }).ToList() : null,
            });

            var finalList = await result.AsNoTracking().ToListAsync();
            _logger.LogInformation($"GetHuts for map view returned {finalList.Count()} huts");
            if (dateFilter != null)
            {
                var bedCategories = await _context.BedCategories.Include(b => b.SharesNameWith).AsNoTracking().ToListAsync();
                foreach (var h in finalList)
                {
                    if (h.Rooms?.Count > 0)
                    {
                        foreach(var room in h.Rooms)
                        {
                            // Look up the CommonName (display name) for each room's bedCategory
                            room.BedCategory = bedCategories.SingleOrDefault(bc => room.BedCategoryId == bc.Id)?.CommonName;
                        }
                    }
                }
            }
            return finalList;
        }

        // GET: api/Map?hutid=123
        [HttpGet("{hutid}")]
        public async Task<ActionResult<IEnumerable<MapPlotHut>>> GetHut([FromRoute] int hutid)
        {
            _logger.LogInformation("Received map request hutid={hutId}", hutid);

            var hut = await _context.Huts.FirstOrDefaultAsync(h => h.Id == hutid);
            if (hut == null)
                return NotFound();

            var result = new List<MapPlotHut>
            {
                new MapPlotHut
                {
                    Id = hut.Id,
                    Name = hut.Name,
                    Enabled = (bool)hut.Enabled,
                    OnlineBookingLink = hut.Link,
                    HutWebsiteLink = hut.HutWebsite,
                    Latitude = (double)hut.Latitude,
                    Longitude = (double)hut.Longitude
                }
            };

            return result;
        }
    }
}
