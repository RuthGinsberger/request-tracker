using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace RequestTracker.Infrastructure.Persistence;

/// <summary>SQL Server returns datetime2 with Kind=Unspecified; mark values as UTC on read.</summary>
public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v,
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
