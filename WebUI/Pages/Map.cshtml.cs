using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Shared.Models;

namespace WebUI.Pages
{
    public class MapModel : PageModel
    {
        private readonly AlpinehutsDbContext _context;
        private readonly IStringLocalizer _localizer;

        public List<SelectListItem> BedCategories { get; set; } = new List<SelectListItem>();
        public string SelectedBedCategory { get; set; }

        public MapModel(AlpinehutsDbContext context, IStringLocalizer<SharedResources> localizer)
        {
            _context = context;
            _localizer = localizer;
        }

        public async Task OnGet()
        {
            BedCategories.Add(new SelectListItem("-", "", true));
            var categories = await _context.BedCategories.Include(b => b.SharesNameWith).AsNoTracking().Select(b => _localizer[b.CommonName].Value).ToListAsync();
            categories = categories.Distinct().ToList();
            BedCategories.AddRange(categories.Select(s => new SelectListItem(s, s)).ToList());
        }
    }
}
