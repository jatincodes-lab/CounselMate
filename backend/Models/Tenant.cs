namespace EducationCrm.Api.Models;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Branch> Branches { get; set; } = [];
    public ICollection<AppUser> Users { get; set; } = [];
    public ICollection<Course> Courses { get; set; } = [];
    public ICollection<LeadStage> LeadStages { get; set; } = [];
    public ICollection<LeadSource> LeadSources { get; set; } = [];
    public ICollection<Lead> Leads { get; set; } = [];
}
