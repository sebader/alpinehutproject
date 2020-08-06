using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Shared.Models;
using Microsoft.Extensions.Logging;

namespace AlpinHutsDashboard.Pages
{
    public class HutsModel : PageModel
    {
        private readonly AlpinehutsDbContext _context;
        private readonly ILogger<HutsModel> _logger;

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:dd.MM.yyyy}", ApplyFormatInEditMode = true)]
        public DateTime? DateFilter { get; set; }
        public int NumberOfPlaces { get; set; } = 1;

        public HutsModel(AlpinehutsDbContext context, ILogger<HutsModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IList<Hut> Huts { get;set; }

        public async Task OnGetAsync(int NumberOfPlaces = 1, DateTime? DateFilter = null)
        {
            this.DateFilter = DateFilter?.Date;

            _logger.LogInformation("Getting hut list for date filter={dateFilter}", this.DateFilter);

            IQueryable<Hut> huts; 
            if(this.DateFilter != null)
            {
                huts = _context.Huts.Where(h => h.Enabled == true && h.Availability.Any(a => a.Date == this.DateFilter && a.FreeRoom >= NumberOfPlaces)).Include(h => h.Availability).ThenInclude(a => a.BedCategory);
            }
            else
            {
                huts = _context.Huts;
            }
            Huts = await huts.AsNoTracking().ToListAsync();

            _logger.LogInformation("Found {hutCount} huts with date filter={dateFilter}", Huts.Count, this.DateFilter);
        }
    }
}
