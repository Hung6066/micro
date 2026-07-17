using System.Collections.Concurrent;
using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace His.Hope.Infrastructure.Logging;

/// <summary>
/// Serilog <see cref="IDestructuringPolicy"/> that automatically redacts
/// Protected Health Information (PHI) field values before they reach the
/// log output.
///
/// HIPAA Context (164.312(b) — Audit Controls, 164.312(c)(1) — Integrity Controls):
///   Audit logs must not expose ePHI values to unauthorized viewers.
///   This policy acts as a safety net — if structured log properties accidentally
///   contain PHI field names, their values are replaced with "[REDACTED-PHI]"
///   before serialization.
///
/// Usage (in Program.cs):
/// <code>
/// builder.Host.UseSerilog((context, config) =>
///     config
///         .ReadFrom.Configuration(context.Configuration)
///         .Destructure.With&lt;PhiDestructuringPolicy&gt;());
/// </code>
///
/// Design Notes:
/// - This policy inspects object properties by name (case-insensitive).
/// - Matching property names are replaced with a redacted sentinel value.
/// - The redaction applies transitively to nested objects.
/// - Property type (string, numeric, date) is irrelevant — the name match
///   determines redaction. This prevents PHI from leaking through unexpected
///   data types.
/// - A thread-safe cache avoids repeated reflection against the same type.
/// </summary>
public class PhiDestructuringPolicy : IDestructuringPolicy
{
    /// <summary>
    /// Sentinel value that replaces any detected PHI field content.
    /// Using a fixed-length sentinel prevents information leakage
    /// through field length analysis.
    /// </summary>
    private const string RedactedMarker = "[REDACTED-PHI]";

    /// <summary>
    /// Known PHI field name patterns (case-insensitive).
    /// When a log object property matches any of these names,
    /// its value is replaced with the redacted marker.
    ///
    /// This list covers the most common PHI identifiers per
    /// HIPAA Safe Harbor (45 CFR 164.514(b)(2)) and common
    /// healthcare domain fields.
    /// </summary>
    private static readonly HashSet<string> PhiFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Patient identifiers
        "PatientName",
        "FirstName",
        "LastName",
        "MiddleName",
        "FullName",

        // Demographics (indirect identifiers)
        "DateOfBirth",
        "DOB",
        "BirthDate",
        "Age",               // Age over 89 is considered PHI
        "Gender",            // Gender identity may be PHI in context
        "Address",
        "StreetAddress",
        "City",
        "State",
        "ZipCode",
        "PostalCode",

        // National identifiers
        "SSN",
        "SocialSecurityNumber",
        "SocialSecurity",
        "NationalId",
        "PassportNumber",
        "DriversLicense",
        "LicenseNumber",

        // Medical identifiers
        "MedicalRecordNumber",
        "MRN",
        "PatientId",         // Use sparingly; may be needed for correlation
        "HealthPlanId",
        "InsuranceId",
        "InsurancePolicyNumber",
        "AccountNumber",
        "BeneficiaryId",
        "ClaimId",

        // Contact
        "PhoneNumber",
        "Phone",
        "MobilePhone",
        "HomePhone",
        "WorkPhone",
        "Email",
        "EmailAddress",

        // Clinical data
        "Diagnosis",
        "DiagnosisDescription",
        "DiagnosisCode",
        "ICDCode",
        "ICD10Code",
        "ProcedureCode",
        "CPTCode",

        "MedicationName",
        "Medication",
        "PrescriptionDrug",
        "RxNorm",

        "LabResultValue",
        "LabResult",
        "TestResult",
        "LabValue",
        "PathologyReport",

        "Vitals",
        "BloodPressure",
        "HeartRate",
        "Temperature",
        "RespiratoryRate",
        "OxygenSaturation",

        // Free-text clinical notes
        "ClinicalNotes",
        "ProviderNotes",
        "NurseNotes",
        "ProgressNotes",
        "ChiefComplaint",
        "HistoryOfPresentIllness",
        "HPI",
        "AssessmentAndPlan",
    };

    /// <summary>
    /// Thread-safe cache of types that have been analyzed.
    /// Maps Type → array of property names that match PHI fields.
    /// Avoids repeated reflection overhead on hot paths.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, string[]> CachedPhiProperties = new();

    /// <summary>
    /// Determines whether this policy can handle the given type.
    /// Returns <c>true</c> for any type — we inspect all objects
    /// for PHI property names.
    /// </summary>
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue result)
    {
        if (value is null)
        {
            result = new ScalarValue(null);
            return true;
        }

        var type = value.GetType();

        // Skip primitive types, strings, and value types — they have no
        // properties we can inspect. Strings themselves are handled by
        // the calling code; our concern is structured objects.
        if (type.IsPrimitive || type == typeof(string) || type.IsValueType)
        {
            result = new ScalarValue(value);
            return true;
        }

        // Check if this type has any PHI properties (from cache or reflection)
        var phiProperties = CachedPhiProperties.GetOrAdd(type, static t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && PhiFieldNames.Contains(p.Name))
                .Select(p => p.Name)
                .ToArray());

        // If no PHI properties found on this type, return false to allow
        // the default destructuring (or the next policy) to handle it.
        if (phiProperties.Length == 0)
        {
            result = null!;
            return false;
        }

        // Build a dictionary of all properties, redacting PHI fields
        var allProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead);

        var destructured = new Dictionary<ScalarValue, LogEventPropertyValue>();

        foreach (var prop in allProperties)
        {
            var propName = prop.Name;
            var propValue = prop.GetValue(value);
            var isPhi = phiProperties.Contains(propName);

            if (isPhi)
            {
                // Redact this property — replace actual value with marker
                destructured[new ScalarValue(propName)] = new ScalarValue(RedactedMarker);
            }
            else
            {
                // Non-PHI property — recursively destructure
                var destructuredValue = propertyValueFactory.CreatePropertyValue(propValue!, true);
                destructured[new ScalarValue(propName)] = destructuredValue;
            }
        }

        result = new DictionaryValue(destructured);
        return true;
    }

    /// <summary>
    /// Checks whether a given property name matches a known PHI field.
    /// This is used for quick checks outside of full destructuring.
    /// </summary>
    public static bool IsPhiFieldName(string propertyName)
    {
        return !string.IsNullOrEmpty(propertyName) && PhiFieldNames.Contains(propertyName);
    }

    /// <summary>
    /// Creates a scalar log event property value with the redacted marker.
    /// Useful for manual redaction in custom logging code.
    /// </summary>
    public static LogEventPropertyValue CreateRedactedValue()
    {
        return new ScalarValue(RedactedMarker);
    }
}
