using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.response
{
    public class CallState
    {
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? PropertyAddress { get; set; }
        public string? RenovationType { get; set; }
        public string? ScopeOfWork { get; set; }
        public string? Budget { get; set; }
        public string? TimeLine { get; set; }
        public string? PlanningPermission { get; set; }
        public string? ArchitecturalDrawings { get; set; }
        public string? Ownership { get; set; }
        public string? Financing { get; set; }
        public string? Urgency { get; set; }
        public string? PreviousExperience { get; set; }
        //public string? appointment_type { get; set; }
        public string? CommunicationMethod { get; set; }
        public string? Notes { get; set; }
        public DateTime? Appointment { get; set; }
        public string? AppointmentString { get; set; }
        public string? AppointmentDay { get; set; }
        public string? AppointmentDate { get; set; }
        public string? AppointmentTime { get; set; }
        public string? AppointmentType { get; set; }
        // optional: track what the caller just said
        // NEW: hold time jab user sirf time bole (e.g., "10 a.m.")
        public int? TimeAttempts { get; set; }
        public TimeSpan? PendingTime { get; set; }
        public bool IsComplete =>
            !string.IsNullOrWhiteSpace(Name) &&
            !string.IsNullOrWhiteSpace(Phone) &&
            !string.IsNullOrWhiteSpace(CommunicationMethod) &&
            !string.IsNullOrWhiteSpace(AppointmentDate) &&
            !string.IsNullOrWhiteSpace(AppointmentTime);
    }
}