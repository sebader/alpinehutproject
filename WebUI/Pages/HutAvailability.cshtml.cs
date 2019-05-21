using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace AlpinHutsDashboard.Pages
{
    public class HutAvailabilityModel : PageModel
    {
        private readonly AlpinehutsDbContext _context;

        public HutAvailabilityModel(AlpinehutsDbContext context)
        {
            _context = context;
        }

        public IList<Availability> Availability { get; set; }

        public async Task<IActionResult> OnGetAsync(int? hutId)
        {
            if (hutId == null)
                return NotFound();

            Availability = await _context.Availability.Where(a => a.Hutid == hutId && a.Date >= DateTime.Today)
                .Include(a => a.Hut).Include(a => a.BedCategory).OrderBy(a => a.Date).ToListAsync();

            return Page();
        } 
    }
}
