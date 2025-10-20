using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Entities
{
    public class ExtractedFields
    {
        public string? name { get; set; }
        public string? phone { get; set; }
        public string? email { get; set; }
        public string? property_address { get; set; }
        public string? renovation_type { get; set; }
        public string? scope_of_work { get; set; }
        public string? budget { get; set; }
        public string? timeline { get; set; }
        public string? planning_permission { get; set; }
        public string? architectural_drawings { get; set; }
        public string? ownership { get; set; }
        public string? financing { get; set; }
        public string? urgency { get; set; }
        public string? previous_experience { get; set; }
        //public string? appointment_type { get; set; }
        public string? appointment_day { get; set; }
        public string? appointment_date { get; set; }
        public string? appointment_time { get; set; }
        public string? communication_method { get; set; }
        public string? notes { get; set; }
        // public AppointmentDetails? Appointment_details { get; set; }
    }
}