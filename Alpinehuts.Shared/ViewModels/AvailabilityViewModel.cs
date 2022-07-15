﻿using System;
using System.Collections.Generic;

namespace Alpinehuts.Shared.ViewModels
{
    public class AvailabilityViewModel
    {
        public int? HutId { get; set; }
        public DateTime Date { get; set; }
        public List<RoomAvailabilityViewModel> RoomAvailabilities { get; set; }
    }

    public class RoomAvailabilityViewModel
    {
        public string BedCategory { get; set; }
        public int FreeBeds { get; set; }
        public int TotalBeds { get; set; }
    }
}
