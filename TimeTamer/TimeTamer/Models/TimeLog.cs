using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeTamer.Models
{
    public class TimeLog : IValidatableObject
    {
        [Key]
        public int Id { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Please choose a start time")]
        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public double DurationMinutes => EndTime.HasValue && StartTime != default
            ? (EndTime.Value - StartTime).TotalMinutes
            : 0;

        public double CompletionTimeMinutes => EndTime.HasValue
            ? (EndTime.Value - CreatedAt).TotalMinutes
            : 0;

        public string FormattedCompletionTime
        {
            get
            {
                if (!EndTime.HasValue) return "In progress...";
                var span = EndTime.Value - CreatedAt;
                if (span.TotalMinutes < 60) return $"{span.Minutes} minutes";
                return $"{(int)span.TotalHours}h {span.Minutes}m";
            }
        }

        public int TaskItemId { get; set; }

        [ForeignKey("TaskItemId")]
        public virtual TaskItem? TaskItem { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartTime < DateTime.Now.AddMinutes(-2))
            {
                yield return new ValidationResult(
                    "You cannot schedule a time in the past. Please choose a current or future time.",
                    new[] { nameof(StartTime) }
                );
            }

            if (EndTime.HasValue && EndTime.Value <= StartTime)
            {
                yield return new ValidationResult(
                    "End time must be later than start time.",
                    new[] { nameof(EndTime) }
                );
            }
        }
    }
}
