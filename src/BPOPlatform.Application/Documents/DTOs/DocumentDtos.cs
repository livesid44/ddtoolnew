namespace BPOPlatform.Application.Documents.DTOs;

/// <summary>Returned from the document generation endpoint: file bytes ready to stream.</summary>
public record DocumentOutputDto(string FileName, string ContentType, byte[] Content);

/// <summary>Returned from the ticket creation endpoint.</summary>
public record TicketDto(string TicketId, string Url, string Status, string ProcessId);
