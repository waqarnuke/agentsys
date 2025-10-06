using System;

namespace API.Entities;

public class BaseEntity
{
    public int Id { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? CreatedByDateTime { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedByDateTime { get; set; }
    public bool Status { get; set; }
}
