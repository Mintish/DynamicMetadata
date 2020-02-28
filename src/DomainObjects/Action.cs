using System;

namespace DomainObjects
{
    public class Action
    {
        public int ID { get; set; }
        public int Type { get; set; }
        public DateTime? Scheduled { get; set; }
        public DateTime? Entered { get; set; }
        public string Comment { get; set; }
        public double? HoursRemaining {
            get {
                double? hours;
                if (Scheduled.HasValue && Entered.HasValue) {
                    hours = (Scheduled.Value - Entered.Value).TotalHours;
                } else {
                    hours = null;
                }
                return hours;
            }
        }
    }
}