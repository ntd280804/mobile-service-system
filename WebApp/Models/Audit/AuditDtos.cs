using System;
using System.Collections.Generic;

namespace WebApp.Models.Audit
{
    public class TriggerAuditLogDto
    {
        public long LogId { get; set; }
        public DateTime? EventTs { get; set; }
        public string? DbUser { get; set; }
        public string? OsUser { get; set; }
        public string? Machine { get; set; }
        public string? Module { get; set; }
        public string? AppRole { get; set; }
        public string? EmpId { get; set; }
        public string? CustomerPhone { get; set; }
        public string? ObjectName { get; set; }
        public string? Note { get; set; }
        public string? DmlType { get; set; }
        public string? ChangedColumns { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
    }
    public class StandardAuditLogDto
    {
        public DateTime? EventTs { get; set; }
        public string? DbUser { get; set; }
        public string? ObjectSchema { get; set; }
        public string? ObjectName { get; set; }
        public string? Action { get; set; }
        public string? Note { get; set; }
    }
    public class FgaAuditLogDto
    {
        public DateTime? EventTs { get; set; }
        public string? DbUser { get; set; }
        public string? OsUser { get; set; }
        public string? UserHost { get; set; }
        public string? ObjectSchema { get; set; }
        public string? ObjectName { get; set; }
        public string? PolicyName { get; set; }
        public string? StatementType { get; set; }
        public long? Scn { get; set; }
        public string? SqlText { get; set; }
        public string? SqlBind { get; set; }
    }
    public class AuditStatusDto
    {
        public AuditTypeStatus? StandardAudit { get; set; }
        public AuditTypeStatus? TriggerAudit { get; set; }
        public AuditTypeStatus? FgaAudit { get; set; }
    }
    public class AuditTypeStatus
    {
        public bool Enabled { get; set; }
        public int Count { get; set; }
        public int ExpectedCount { get; set; }
        public string? Note { get; set; }
    }
    public class TriggerAuditViewModel
    {
        public List<TriggerAuditLogDto> Logs { get; set; } = new();
        public AuditTypeStatus? Status { get; set; }
    }
    public class StandardAuditViewModel
    {
        public List<StandardAuditLogDto> Logs { get; set; } = new();
        public AuditTypeStatus? Status { get; set; }
    }
    public class FgaAuditViewModel
    {
        public List<FgaAuditLogDto> Logs { get; set; } = new();
        public AuditTypeStatus? Status { get; set; }
    }
}
