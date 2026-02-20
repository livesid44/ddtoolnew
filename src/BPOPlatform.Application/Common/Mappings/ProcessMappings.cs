using BPOPlatform.Application.Processes.DTOs;
using BPOPlatform.Domain.Entities;

namespace BPOPlatform.Application.Processes.DTOs;

internal static class ProcessMappingExtensions
{
    internal static ProcessDto ToDto(this Process p) => new(
        p.Id,
        p.Name,
        p.Description,
        p.Department,
        p.Status,
        p.AutomationScore,
        p.ComplianceScore,
        p.OwnerId,
        p.CreatedAt,
        p.Artifacts.Count
    );
}
