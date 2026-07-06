using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using EducationCrm.Api.Data;
using EducationCrm.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EducationCrm.Api.Services;

public static class LeadFileService
{
    public const long MaximumFileBytes = 10 * 1024 * 1024;
    public const int MaximumRows = 5000;
    public const int MaximumColumns = 50;
    public const int MaximumCellLength = 2000;
    public const int MaximumExportRows = 50000;

    private static readonly IReadOnlyList<ImportColumnDefinition> Columns =
    [
        new("studentName", "Student Name", true, ["studentname", "student", "name", "leadname"]),
        new("guardianName", "Guardian Name", false, ["guardianname", "guardian", "parentname", "fathername", "mothername"]),
        new("email", "Email", true, ["email", "emailaddress", "studentemail"]),
        new("phone", "Phone", true, ["phone", "phonenumber", "mobile", "mobilenumber", "contactnumber"]),
        new("city", "City", false, ["city", "location"]),
        new("course", "Course", true, ["course", "program", "programme", "interestedcourse"]),
        new("source", "Lead Source", true, ["source", "leadsource", "channel"]),
        new("stage", "Lead Stage", false, ["stage", "leadstage", "pipelinestage"]),
        new("branch", "Branch", false, ["branch", "campus", "center", "centre"]),
        new("assignedTo", "Assigned To", false, ["assignedto", "assignee", "counsellor", "counselor", "owner"]),
        new("status", "Status", false, ["status", "leadstatus"]),
        new("priority", "Priority", false, ["priority", "leadpriority"]),
        new("nextFollowUp", "Next Follow-up", false, ["nextfollowup", "followup", "followupdate", "nextfollowupdate"])
    ];

    private static readonly string[] AllowedPriorities = ["Low", "Medium", "High", "Urgent"];

    public static IReadOnlyCollection<ImportColumnResponse> GetColumns() =>
        Columns.Select(item => new ImportColumnResponse(item.Key, item.Label, item.Required)).ToArray();

    public static async Task<LeadImportSheet> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new LeadImportException("Select a non-empty CSV or XLSX file.");
        }
        if (file.Length > MaximumFileBytes)
        {
            throw new LeadImportException("The import file must be 10 MB or smaller.");
        }

        var extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
        if (extension is not ".csv" and not ".xlsx")
        {
            throw new LeadImportException("Only .csv and .xlsx files are supported.");
        }

        await using var source = file.OpenReadStream();
        using var buffer = new MemoryStream((int)Math.Min(file.Length, MaximumFileBytes));
        await source.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length > MaximumFileBytes)
        {
            throw new LeadImportException("The import file must be 10 MB or smaller.");
        }

        var bytes = buffer.ToArray();
        var fingerprint = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        try
        {
            var parsed = extension == ".csv" ? ReadCsv(bytes) : ReadWorkbook(bytes);
            ValidateHeaders(parsed.Headers);
            if (parsed.Rows.Count == 0)
            {
                throw new LeadImportException("The file contains headers but no data rows.");
            }
            return parsed with
            {
                FileName = Path.GetFileName(file.FileName),
                Fingerprint = fingerprint
            };
        }
        catch (LeadImportException)
        {
            throw;
        }
        catch (Exception exception) when (exception is CsvHelperException or DecoderFallbackException or InvalidDataException or FormatException)
        {
            throw new LeadImportException("The file could not be parsed. Check that it is a valid CSV or XLSX workbook.");
        }
    }

    public static IReadOnlyDictionary<string, string> ResolveMapping(
        IReadOnlyList<string> headers,
        IReadOnlyDictionary<string, string>? requestedMapping)
    {
        var headerLookup = headers.ToDictionary(NormalizeHeader, item => item, StringComparer.OrdinalIgnoreCase);
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in Columns)
        {
            if (requestedMapping is not null &&
                requestedMapping.TryGetValue(column.Key, out var requestedHeader) &&
                !string.IsNullOrWhiteSpace(requestedHeader))
            {
                var match = headers.FirstOrDefault(item => string.Equals(item, requestedHeader.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    mapping[column.Key] = match;
                }
                continue;
            }

            var suggestedHeader = column.Aliases
                .Select(alias => headerLookup.GetValueOrDefault(alias))
                .FirstOrDefault(item => item is not null);
            if (suggestedHeader is not null)
            {
                mapping[column.Key] = suggestedHeader;
            }
        }

        return mapping;
    }

    public static async Task<LeadImportAnalysis> AnalyzeAsync(
        AppDbContext db,
        Guid tenantId,
        LeadImportSheet sheet,
        IReadOnlyDictionary<string, string> mapping,
        string? duplicateMode,
        CancellationToken cancellationToken)
    {
        var mode = string.Equals(duplicateMode, "update", StringComparison.OrdinalIgnoreCase) ? "update" : "skip";
        var globalIssues = ValidateMapping(sheet.Headers, mapping).ToList();
        if (globalIssues.Count > 0)
        {
            return new LeadImportAnalysis(sheet, mapping, mode, globalIssues, []);
        }

        var courses = await db.Courses.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsActive)
            .ToListAsync(cancellationToken);
        var sources = await db.LeadSources.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsActive)
            .ToListAsync(cancellationToken);
        var stages = await db.LeadStages.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);
        var branches = await db.Branches.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsActive)
            .ToListAsync(cancellationToken);
        var users = await db.Users.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsActive && item.Role != UserRole.Accountant && item.Role != UserRole.ReadOnly)
            .ToListAsync(cancellationToken);
        var tenantDefaults = await db.Tenants.AsNoTracking()
            .Where(item => item.Id == tenantId)
            .Select(item => new { item.DefaultBranchId, item.DefaultAssigneeUserId })
            .FirstAsync(cancellationToken);

        var normalizedPhones = sheet.Rows
            .Select(row => NormalizePhone(GetValue(row, mapping, "phone")))
            .Where(value => value.Length > 0)
            .ToArray();
        var repeatedPhones = normalizedPhones
            .GroupBy(value => value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        var existingLeads = await db.Leads.AsNoTracking()
            .Where(item => item.TenantId == tenantId && normalizedPhones.Contains(item.NormalizedPhone))
            .ToDictionaryAsync(item => item.NormalizedPhone, StringComparer.Ordinal, cancellationToken);

        var courseLookup = BuildUniqueLookup(courses, item => item.Name);
        var sourceLookup = BuildUniqueLookup(sources, item => item.Name);
        var stageLookup = BuildUniqueLookup(stages, item => item.Name);
        var branchLookup = BuildUniqueLookup(branches, item => item.Name);
        var userNameLookup = users.GroupBy(item => NormalizeLookup(item.FullName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var userEmailLookup = users.ToDictionary(item => item.Email, item => item, StringComparer.OrdinalIgnoreCase);
        var defaultStage = stages.FirstOrDefault(item => item.IsDefaultStage) ?? stages.FirstOrDefault();
        var defaultBranch = branches.FirstOrDefault(item => item.Id == tenantDefaults.DefaultBranchId);
        var defaultAssignee = users.FirstOrDefault(item => item.Id == tenantDefaults.DefaultAssigneeUserId);

        var analyzedRows = new List<LeadImportAnalyzedRow>(sheet.Rows.Count);
        foreach (var row in sheet.Rows)
        {
            var issues = new List<LeadImportIssue>();
            var normalizedPhone = NormalizePhone(GetValue(row, mapping, "phone"));
            existingLeads.TryGetValue(normalizedPhone, out var existingLead);

            if (repeatedPhones.Contains(normalizedPhone))
            {
                issues.Add(Error(row.RowNumber, "phone", "This phone number appears more than once in the import file."));
            }

            var studentName = RequiredText(row, mapping, "studentName", "Student name", 160, issues);
            var email = RequiredText(row, mapping, "email", "Email", 240, issues).ToLowerInvariant();
            var phone = RequiredText(row, mapping, "phone", "Phone", 40, issues);
            if (email.Length > 0 && !IsValidEmail(email))
            {
                issues.Add(Error(row.RowNumber, "email", "Enter a valid email address."));
            }
            if (normalizedPhone.Length is < 7 or > 15)
            {
                issues.Add(Error(row.RowNumber, "phone", "Phone must contain 7 to 15 digits."));
            }

            var course = ResolveRequiredLookup(row, mapping, "course", "Course", courseLookup, issues);
            var source = ResolveRequiredLookup(row, mapping, "source", "Lead source", sourceLookup, issues);

            var stageText = GetValue(row, mapping, "stage");
            var stage = stageText.Length > 0
                ? ResolveLookup(row.RowNumber, "stage", "Lead stage", stageText, stageLookup, issues)
                : existingLead is not null
                    ? stages.FirstOrDefault(item => item.Id == existingLead.LeadStageId)
                    : defaultStage;
            if (stage is null)
            {
                issues.Add(Error(row.RowNumber, "stage", "No active default lead stage is configured."));
            }

            var branchText = GetValue(row, mapping, "branch");
            var branch = branchText.Length > 0
                ? ResolveLookup(row.RowNumber, "branch", "Branch", branchText, branchLookup, issues)
                : existingLead is not null
                    ? branches.FirstOrDefault(item => item.Id == existingLead.BranchId)
                    : defaultBranch;

            var assigneeText = GetValue(row, mapping, "assignedTo");
            var assignee = assigneeText.Length > 0
                ? ResolveUser(row.RowNumber, assigneeText, userNameLookup, userEmailLookup, issues)
                : existingLead is not null
                    ? users.FirstOrDefault(item => item.Id == existingLead.AssignedUserId)
                    : defaultAssignee;

            if (branch is not null && assignee?.BranchId is not null && assignee.BranchId != branch.Id)
            {
                issues.Add(Error(row.RowNumber, "assignedTo", "Assigned user must belong to the selected branch."));
            }

            var guardianName = OptionalText(row, mapping, "guardianName", "Guardian name", 160, issues);
            var city = OptionalText(row, mapping, "city", "City", 120, issues);
            if (existingLead is not null)
            {
                guardianName ??= existingLead.GuardianName;
                city ??= existingLead.City;
            }

            var priorityText = GetValue(row, mapping, "priority");
            var priority = priorityText.Length == 0
                ? existingLead?.Priority ?? "Medium"
                : AllowedPriorities.FirstOrDefault(item => string.Equals(item, priorityText, StringComparison.OrdinalIgnoreCase));
            if (priority is null)
            {
                issues.Add(Error(row.RowNumber, "priority", "Priority must be Low, Medium, High, or Urgent."));
                priority = "Medium";
            }

            var statusText = GetValue(row, mapping, "status");
            var status = statusText.Length == 0 ? existingLead?.Status ?? stage?.Name ?? "New Lead" : NormalizeText(statusText);
            if (status.Length > 80)
            {
                issues.Add(Error(row.RowNumber, "status", "Status cannot exceed 80 characters."));
            }

            var followUpText = GetValue(row, mapping, "nextFollowUp");
            var nextFollowUp = existingLead?.NextFollowUpAt;
            if (followUpText.Length > 0)
            {
                if (TryParseIndianDate(followUpText, out var parsedFollowUp))
                {
                    nextFollowUp = parsedFollowUp;
                }
                else
                {
                    issues.Add(Error(row.RowNumber, "nextFollowUp", "Use a valid date such as 2026-07-15 14:30."));
                }
            }

            var action = existingLead is null ? "create" : mode == "update" ? "update" : "skip";
            if (existingLead?.ArchivedAt is not null && action == "update")
            {
                issues.Add(Error(row.RowNumber, "phone", "This phone belongs to an archived lead. Restore it before importing updates."));
            }
            if (existingLead is not null && action == "skip")
            {
                issues.Add(Warning(row.RowNumber, "phone", $"Existing lead {existingLead.LeadNumber} will be skipped."));
            }

            LeadImportPreparedRow? prepared = null;
            if (issues.All(item => item.Severity != "error") && action != "skip" && course is not null && source is not null && stage is not null)
            {
                prepared = new LeadImportPreparedRow(
                    row.RowNumber,
                    action,
                    existingLead?.Id,
                    studentName,
                    guardianName,
                    email,
                    phone,
                    normalizedPhone,
                    city,
                    course.Id,
                    source.Id,
                    stage.Id,
                    branch?.Id,
                    assignee?.Id,
                    status,
                    priority,
                    nextFollowUp);
            }

            analyzedRows.Add(new LeadImportAnalyzedRow(row, action, issues, prepared));
        }

        return new LeadImportAnalysis(sheet, mapping, mode, globalIssues, analyzedRows);
    }

    public static byte[] CreateCsv(IReadOnlyCollection<LeadExportRow> rows)
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, new UTF8Encoding(true), leaveOpen: true))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            foreach (var header in ExportHeaders()) csv.WriteField(header);
            csv.NextRecord();
            foreach (var row in rows)
            {
                foreach (var value in ExportValues(row)) csv.WriteField(SafeSpreadsheetText(value));
                csv.NextRecord();
            }
        }
        return stream.ToArray();
    }

    public static byte[] CreateImportTemplateCsv()
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, new UTF8Encoding(true), leaveOpen: true))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            foreach (var column in Columns) csv.WriteField(column.Label);
            csv.NextRecord();
        }
        return stream.ToArray();
    }

    public static byte[] CreateImportTemplateWorkbook()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Lead Import");
        for (var index = 0; index < Columns.Count; index++)
        {
            var cell = worksheet.Cell(1, index + 1);
            cell.Value = Columns[index].Label;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(Columns[index].Required ? "#DDEBFF" : "#F2F4F7");
        }
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns().AdjustToContents(12, 28);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static byte[] CreateWorkbook(IReadOnlyCollection<LeadExportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Leads");
        var headers = ExportHeaders();
        for (var column = 0; column < headers.Length; column++)
        {
            worksheet.Cell(1, column + 1).Value = headers[column];
            worksheet.Cell(1, column + 1).Style.Font.Bold = true;
            worksheet.Cell(1, column + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E9F0FF");
        }

        var rowNumber = 2;
        foreach (var row in rows)
        {
            var values = ExportValues(row);
            for (var column = 0; column < values.Length; column++)
            {
                var cell = worksheet.Cell(rowNumber, column + 1);
                cell.SetValue(SafeSpreadsheetText(values[column]));
            }
            rowNumber++;
        }

        worksheet.SheetView.FreezeRows(1);
        worksheet.RangeUsed()?.SetAutoFilter();
        worksheet.Columns().AdjustToContents(8, 42);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static LeadImportSheet ReadCsv(byte[] bytes)
    {
        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            DetectDelimiter = true,
            TrimOptions = TrimOptions.Trim,
            IgnoreBlankLines = true,
            BadDataFound = args => throw new LeadImportException($"Malformed CSV data near row {args.Context?.Parser?.Row ?? 0}."),
            HeaderValidated = null,
            MissingFieldFound = null
        };

        using var stream = new MemoryStream(bytes);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
        using var csv = new CsvReader(reader, configuration);
        if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is null)
        {
            throw new LeadImportException("The CSV file must include a header row.");
        }

        var headers = csv.HeaderRecord.Select(item => item?.Trim() ?? string.Empty).ToArray();
        if (headers.Length > MaximumColumns)
        {
            throw new LeadImportException($"The file contains more than {MaximumColumns} columns.");
        }

        var rows = new List<LeadImportRawRow>();
        while (csv.Read())
        {
            var rowNumber = csv.Context.Parser?.Row ?? rows.Count + 2;
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var hasValue = false;
            for (var index = 0; index < headers.Length; index++)
            {
                var value = (csv.GetField(index) ?? string.Empty).Trim();
                EnsureCellLength(value, rowNumber);
                values[headers[index]] = value;
                hasValue |= value.Length > 0;
            }
            if (!hasValue) continue;
            rows.Add(new LeadImportRawRow(rowNumber, values));
            if (rows.Count > MaximumRows)
            {
                throw new LeadImportException($"The file contains more than {MaximumRows} data rows.");
            }
        }

        return new LeadImportSheet(string.Empty, string.Empty, headers, rows);
    }

    private static LeadImportSheet ReadWorkbook(byte[] bytes)
    {
        ValidateWorkbookArchive(bytes);
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault(item => item.RangeUsed() is not null)
            ?? throw new LeadImportException("The workbook does not contain a populated worksheet.");
        var range = worksheet.RangeUsed()!;
        var firstRow = range.FirstRow().RowNumber();
        var firstColumn = range.FirstColumn().ColumnNumber();
        var lastRow = range.LastRow().RowNumber();
        var lastColumn = range.LastColumn().ColumnNumber();
        var columnCount = lastColumn - firstColumn + 1;
        if (columnCount > MaximumColumns)
        {
            throw new LeadImportException($"The worksheet contains more than {MaximumColumns} columns.");
        }
        if (lastRow - firstRow > MaximumRows)
        {
            throw new LeadImportException($"The worksheet contains more than {MaximumRows} data rows.");
        }

        var headers = Enumerable.Range(firstColumn, columnCount)
            .Select(column => worksheet.Cell(firstRow, column).GetString().Trim())
            .ToArray();
        var rows = new List<LeadImportRawRow>();
        for (var rowNumber = firstRow + 1; rowNumber <= lastRow; rowNumber++)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var hasValue = false;
            for (var offset = 0; offset < columnCount; offset++)
            {
                var cell = worksheet.Cell(rowNumber, firstColumn + offset);
                var value = ReadCell(cell);
                EnsureCellLength(value, rowNumber);
                values[headers[offset]] = value;
                hasValue |= value.Length > 0;
            }
            if (hasValue) rows.Add(new LeadImportRawRow(rowNumber, values));
        }

        return new LeadImportSheet(string.Empty, string.Empty, headers, rows);
    }

    private static void ValidateWorkbookArchive(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
        if (archive.Entries.Count > 2000 || archive.Entries.Sum(item => item.Length) > 50L * 1024 * 1024)
        {
            throw new LeadImportException("The workbook expands beyond the allowed processing limit.");
        }
    }

    private static string ReadCell(IXLCell cell)
    {
        if (cell.HasFormula) return string.Empty;
        if (cell.TryGetValue<DateTime>(out var date)) return date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        return cell.GetFormattedString(CultureInfo.InvariantCulture).Trim();
    }

    private static void ValidateHeaders(IReadOnlyList<string> headers)
    {
        if (headers.Count == 0 || headers.All(string.IsNullOrWhiteSpace))
            throw new LeadImportException("The file must include a header row.");
        if (headers.Any(string.IsNullOrWhiteSpace))
            throw new LeadImportException("Every populated column must have a header.");
        var duplicate = headers.GroupBy(NormalizeHeader, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new LeadImportException($"The header '{duplicate.First()}' appears more than once.");
    }

    private static IEnumerable<LeadImportIssue> ValidateMapping(IReadOnlyList<string> headers, IReadOnlyDictionary<string, string> mapping)
    {
        var validKeys = Columns.Select(item => item.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in mapping)
        {
            if (!validKeys.Contains(item.Key))
            {
                yield return Error(0, item.Key, "Unknown import field.");
                continue;
            }
            if (!headers.Contains(item.Value, StringComparer.OrdinalIgnoreCase))
            {
                yield return Error(0, item.Key, "The mapped source column does not exist in this file.");
                continue;
            }
            if (!usedHeaders.Add(item.Value))
            {
                yield return Error(0, item.Key, "A source column cannot be mapped to more than one field.");
            }
        }
        foreach (var column in Columns.Where(item => item.Required && !mapping.ContainsKey(item.Key)))
        {
            yield return Error(0, column.Key, $"Map a source column to {column.Label}.");
        }
    }

    private static Dictionary<string, T> BuildUniqueLookup<T>(IEnumerable<T> values, Func<T, string> nameSelector) where T : class
    {
        return values.GroupBy(item => NormalizeLookup(nameSelector(item)), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);
    }

    private static T? ResolveRequiredLookup<T>(LeadImportRawRow row, IReadOnlyDictionary<string, string> mapping, string field, string label, Dictionary<string, T> lookup, List<LeadImportIssue> issues) where T : class
    {
        var value = GetValue(row, mapping, field);
        if (value.Length == 0)
        {
            issues.Add(Error(row.RowNumber, field, $"{label} is required."));
            return null;
        }
        return ResolveLookup(row.RowNumber, field, label, value, lookup, issues);
    }

    private static T? ResolveLookup<T>(int rowNumber, string field, string label, string value, Dictionary<string, T> lookup, List<LeadImportIssue> issues) where T : class
    {
        if (lookup.TryGetValue(NormalizeLookup(value), out var match)) return match;
        issues.Add(Error(rowNumber, field, $"{label} '{value}' does not match one active tenant record."));
        return null;
    }

    private static AppUser? ResolveUser(int rowNumber, string value, IReadOnlyDictionary<string, AppUser[]> names, IReadOnlyDictionary<string, AppUser> emails, List<LeadImportIssue> issues)
    {
        if (emails.TryGetValue(value.Trim(), out var emailMatch)) return emailMatch;
        if (names.TryGetValue(NormalizeLookup(value), out var nameMatches))
        {
            if (nameMatches.Length == 1) return nameMatches[0];
            issues.Add(Error(rowNumber, "assignedTo", "Multiple active users have this name. Map using the user's email address."));
            return null;
        }
        issues.Add(Error(rowNumber, "assignedTo", $"Assigned user '{value}' does not match an active CRM user."));
        return null;
    }

    private static string RequiredText(LeadImportRawRow row, IReadOnlyDictionary<string, string> mapping, string field, string label, int maximumLength, List<LeadImportIssue> issues)
    {
        var value = GetValue(row, mapping, field);
        if (value.Length == 0) issues.Add(Error(row.RowNumber, field, $"{label} is required."));
        if (value.Length > maximumLength) issues.Add(Error(row.RowNumber, field, $"{label} cannot exceed {maximumLength} characters."));
        return NormalizeText(value);
    }

    private static string? OptionalText(LeadImportRawRow row, IReadOnlyDictionary<string, string> mapping, string field, string label, int maximumLength, List<LeadImportIssue> issues)
    {
        var value = GetValue(row, mapping, field);
        if (value.Length == 0) return null;
        if (value.Length > maximumLength) issues.Add(Error(row.RowNumber, field, $"{label} cannot exceed {maximumLength} characters."));
        return NormalizeText(value);
    }

    private static string GetValue(LeadImportRawRow row, IReadOnlyDictionary<string, string> mapping, string field)
    {
        return mapping.TryGetValue(field, out var header) && row.Values.TryGetValue(header, out var value) ? value.Trim() : string.Empty;
    }

    private static bool TryParseIndianDate(string value, out DateTimeOffset result)
    {
        var formats = new[] { "yyyy-MM-dd", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "dd/MM/yyyy", "dd/MM/yyyy HH:mm", "dd-MM-yyyy", "dd-MM-yyyy HH:mm" };
        if (DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var exact) ||
            DateTime.TryParse(value.Trim(), CultureInfo.GetCultureInfo("en-IN"), DateTimeStyles.AllowWhiteSpaces, out exact))
        {
            result = new DateTimeOffset(DateTime.SpecifyKind(exact, DateTimeKind.Unspecified), IndianClock.Offset);
            return true;
        }
        result = default;
        return false;
    }

    private static string NormalizeHeader(string value) => Regex.Replace(value ?? string.Empty, @"[^A-Za-z0-9]", string.Empty).ToLowerInvariant();
    private static string NormalizeLookup(string value) => NormalizeText(value).ToUpperInvariant();
    private static string NormalizeText(string value) => Regex.Replace(value.Trim(), @"\s+", " ");
    private static string NormalizePhone(string value) => Regex.Replace(value ?? string.Empty, @"\D", string.Empty);
    private static bool IsValidEmail(string value) => Regex.IsMatch(value, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
    private static void EnsureCellLength(string value, int rowNumber)
    {
        if (value.Length > MaximumCellLength) throw new LeadImportException($"A value near row {rowNumber} exceeds {MaximumCellLength} characters.");
    }
    private static LeadImportIssue Error(int row, string field, string message) => new(row, field, message, "error");
    private static LeadImportIssue Warning(int row, string field, string message) => new(row, field, message, "warning");
    private static string SafeSpreadsheetText(string? value) => !string.IsNullOrEmpty(value) && "=+-@".Contains(value[0]) ? $"'{value}" : value ?? string.Empty;
    private static string[] ExportHeaders() => ["Lead ID", "Student Name", "Guardian Name", "Email", "Phone", "City", "Course", "Lead Source", "Stage", "Status", "Priority", "Branch", "Counsellor", "Next Follow-up", "Created At", "Updated At", "Archived At"];
    private static string[] ExportValues(LeadExportRow row) =>
    [
        row.LeadNumber, row.StudentName, row.GuardianName ?? string.Empty, row.Email, row.Phone, row.City ?? string.Empty,
        row.Course, row.Source, row.Stage, row.Status, row.Priority, row.Branch ?? string.Empty, row.AssignedTo ?? string.Empty,
        FormatDate(row.NextFollowUpAt), FormatDate(row.CreatedAt), FormatDate(row.UpdatedAt), FormatDate(row.ArchivedAt)
    ];
    private static string FormatDate(DateTimeOffset? value) => value?.ToOffset(IndianClock.Offset).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
}

public sealed class LeadImportException(string message) : Exception(message);
public sealed record ImportColumnDefinition(string Key, string Label, bool Required, IReadOnlyCollection<string> Aliases);
public sealed record ImportColumnResponse(string Key, string Label, bool Required);
public sealed record LeadImportRawRow(int RowNumber, IReadOnlyDictionary<string, string> Values);
public sealed record LeadImportSheet(string FileName, string Fingerprint, IReadOnlyList<string> Headers, IReadOnlyList<LeadImportRawRow> Rows);
public sealed record LeadImportIssue(int RowNumber, string Field, string Message, string Severity);
public sealed record LeadImportPreparedRow(int RowNumber, string Action, Guid? ExistingLeadId, string StudentName, string? GuardianName, string Email, string Phone, string NormalizedPhone, string? City, Guid CourseId, Guid SourceId, Guid StageId, Guid? BranchId, Guid? AssignedUserId, string Status, string Priority, DateTimeOffset? NextFollowUpAt);
public sealed record LeadImportAnalyzedRow(LeadImportRawRow Source, string Action, IReadOnlyCollection<LeadImportIssue> Issues, LeadImportPreparedRow? Prepared);

public sealed record LeadImportAnalysis(
    LeadImportSheet Sheet,
    IReadOnlyDictionary<string, string> Mapping,
    string DuplicateMode,
    IReadOnlyCollection<LeadImportIssue> GlobalIssues,
    IReadOnlyCollection<LeadImportAnalyzedRow> Rows)
{
    public bool HasErrors => GlobalIssues.Any(item => item.Severity == "error") || Rows.Any(row => row.Issues.Any(item => item.Severity == "error"));
    public IReadOnlyCollection<LeadImportPreparedRow> PreparedRows => Rows.Where(row => row.Prepared is not null).Select(row => row.Prepared!).ToArray();

    public LeadImportPreviewResponse ToResponse()
    {
        var issues = GlobalIssues.Concat(Rows.SelectMany(row => row.Issues)).ToArray();
        return new LeadImportPreviewResponse(
            Sheet.FileName,
            Sheet.Fingerprint,
            Sheet.Headers,
            LeadFileService.GetColumns(),
            Mapping,
            DuplicateMode,
            Sheet.Rows.Count,
            Rows.Count(row => row.Action == "create" && row.Prepared is not null),
            Rows.Count(row => row.Action == "update" && row.Prepared is not null),
            Rows.Count(row => row.Action == "skip"),
            issues.Count(item => item.Severity == "error"),
            issues.Count(item => item.Severity == "warning"),
            issues.Take(500).ToArray(),
            issues.Length > 500,
            Rows.Take(100).Select(row => new LeadImportPreviewRow(row.Source.RowNumber, row.Source.Values, row.Action, row.Issues)).ToArray());
    }
}

public sealed record LeadImportPreviewRow(int RowNumber, IReadOnlyDictionary<string, string> Values, string Action, IReadOnlyCollection<LeadImportIssue> Issues);
public sealed record LeadImportPreviewResponse(string FileName, string Fingerprint, IReadOnlyList<string> Headers, IReadOnlyCollection<ImportColumnResponse> Columns, IReadOnlyDictionary<string, string> Mapping, string DuplicateMode, int TotalRows, int CreateRows, int UpdateRows, int SkipRows, int ErrorCount, int WarningCount, IReadOnlyCollection<LeadImportIssue> Issues, bool IssuesTruncated, IReadOnlyCollection<LeadImportPreviewRow> Rows);
public sealed record LeadImportCommitResponse(int Created, int Updated, int Skipped, int Total, string Message);
public sealed record LeadExportRow(string LeadNumber, string StudentName, string? GuardianName, string Email, string Phone, string? City, string Course, string Source, string Stage, string Status, string Priority, string? Branch, string? AssignedTo, DateTimeOffset? NextFollowUpAt, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? ArchivedAt);
