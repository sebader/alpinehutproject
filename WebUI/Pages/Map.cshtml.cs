using System;
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
    }
}
