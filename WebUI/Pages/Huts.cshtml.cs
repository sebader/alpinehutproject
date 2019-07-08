using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Shared.Models;

namespace AlpinHutsDashboard.Pages
{
    public class HutsModel : PageModel
    {
        private readonly AlpinehutsDbContext _context;

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:dd.MM.yyyy}", ApplyFormatInEditMode = true)]
        public DateTime? DateFilter { get; set; }

        public HutsModel(AlpinehutsDbContext context)
        {
            _context = context;
        }

        public IList<Hut> Huts { get;set; }

        public async Task OnGetAsync(bool onlyEnabled = true, DateTime? DateFilter = null)
        {
            this.DateFilter = DateFilter?.Date;
            IQueryable<Hut> huts; 
            if(this.DateFilter != null)
            {
                huts = _context.Huts.Where(h => h.Enabled == true && h.Availability.Any(a => a.Date == this.DateFilter && a.FreeRoom > 0));
            }
            else
            {
                huts = _context.Huts.Where(h => h.Enabled == true || h.Enabled == onlyEnabled);
            }
            Huts = await huts.Include(h => h.Availability).ThenInclude(a => a.BedCategory).ToListAsync();
        }
    }
}
