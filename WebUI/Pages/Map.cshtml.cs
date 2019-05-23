﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace WebUI.Pages
{
    public class MapModel : PageModel
    {
        private readonly AlpinehutsDbContext _context;

        public MapModel(AlpinehutsDbContext context)
        {
            _context = context;
        }

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:dd.MM.yyyy}", ApplyFormatInEditMode = true)]
        public DateTime? DateFilter { get; set; }


        public IList<Hut> Huts { get; set; }

        public async Task OnGetAsync(bool onlyEnabled = true, DateTime? DateFilter = null)
        {
            /*
            this.DateFilter = DateFilter?.Date;
            var huts = _context.Huts.Where(h => h.Enabled == true || h.Enabled == onlyEnabled);
            if (this.DateFilter != null)
            {
                huts = huts.Where(h => h.Availability.Any(a => a.Date == this.DateFilter && a.FreeRoom > 0));
            }
            Huts = await huts.Include(h => h.Availability).ThenInclude(a => a.BedCategory).ToListAsync();
            */
        }
    }
}
