using Penguin.Analysis.Constraints;
using System;
using System.Collections.Generic;
using System.Text;

namespace Penguin.Analysis
{
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<IRouteConstraint> Violations { get; set; } = new List<IRouteConstraint>();
        public LongByte  Checked { get; set; }
        public string Message { get; set; }
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
