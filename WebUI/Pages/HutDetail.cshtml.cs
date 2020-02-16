using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Models;

namespace AlpinHutsDashboard.Pages
{
    public class HutDetailModel : PageModel
    {
        private readonly AlpinehutsDbContext _context;
        private readonly ILogger<HutDetailModel> _logger;

        public HutDetailModel(AlpinehutsDbContext context, ILogger<HutDetailModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        public Hut Hut { get; set; }
        public IList<Availability> Availability { get; set; }

        public async Task<IActionResult> OnGetAsync(int? hutId)
        {
            _logger.LogInformation("Get Hut Deatils for hutid={hutId}", hutId);
            if (hutId == null || (Hut = _context.Huts.AsNoTracking().SingleOrDefault(h => h.Id == hutId)) == null)
                return NotFound();

            Availability = await _context.Availability.Where(a => a.Hutid == hutId && a.Date >= DateTime.Today)
                .Include(a => a.Hut).Include(a => a.BedCategory).OrderBy(a => a.Date).AsNoTracking().ToListAsync();

            return Page();
        } 
    }
}
