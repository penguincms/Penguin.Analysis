using Penguin.Analysis.Constraints;
using System.Collections.Generic;

namespace Penguin.Analysis
{
    public class ValidationResult
    {
        public LongByte Checked { get; set; }

        public bool IsValid { get; set; }

        public string Message { get; set; }

        public List<IRouteConstraint> Violations { get; } = new List<IRouteConstraint>();

        public ValidationResult(string message, LongByte @checked)
        {
            IsValid = false;
            Message = message;
            Checked = @checked;
        }

        public ValidationResult(IRouteConstraint violation, LongByte @checked)
        {
            IsValid = false;
            Violations.Add(violation);
            Checked = @checked;
        }

        public ValidationResult(LongByte @checked)
        {
            IsValid = true;
            Checked = @checked;
        }
    }
}